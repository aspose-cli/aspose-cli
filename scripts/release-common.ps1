$script:ArtifactOwnerProductId = 'aspose-cli-build-output'
$script:BuildManifestName = 'ASPOSE-CLI-BUILD.json'

function Invoke-RepositoryGit {
    param(
        [Parameter(Mandatory)][string] $Repository,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    $safeRepository = [IO.Path]::GetFullPath($Repository).Replace('\', '/')
    $output = @(& git -c "safe.directory=$safeRepository" -C $Repository @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "Git failed in '$Repository': git $($Arguments -join ' ')"
    }
    return ($output -join "`n").Trim()
}


function Get-ProjectGitContext {
    param([Parameter(Mandatory)][string] $ProjectRoot)
    $project = [IO.Path]::GetFullPath($ProjectRoot)
    $cursor = [IO.DirectoryInfo]::new($project)
    while ($null -ne $cursor -and -not (Test-Path -LiteralPath (Join-Path $cursor.FullName '.git'))) { $cursor = $cursor.Parent }
    if ($null -eq $cursor) { throw "Project is not in a Git working tree: $project" }
    $gitRoot = $cursor.FullName
    $relative = [IO.Path]::GetRelativePath($gitRoot, $project).Replace('\','/')
    [pscustomobject]@{ ProjectRoot=$project; GitRoot=$gitRoot; Prefix=$(if ($relative -eq '.') { '' } else { $relative + '/' }) }
}

function Get-RepositoryProvenance {
    param([Parameter(Mandatory)][string] $RepositoryRoot)
    $context = Get-ProjectGitContext $RepositoryRoot
    $root = $context.ProjectRoot
    $revision = Invoke-RepositoryGit $context.GitRoot @('rev-parse','--verify','HEAD')
    if ($revision -cnotmatch '^[0-9a-f]{40}$') { throw 'Repository HEAD is not a full commit id.' }
    $scope = if ($context.Prefix) { $context.Prefix.TrimEnd('/') } else { '.' }
    $dirty = @((Invoke-RepositoryGit $context.GitRoot @('status','--porcelain=v1','--untracked-files=all','--ignore-submodules=all','--',$scope)) -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $catalog = Get-Content -LiteralPath (Join-Path $root 'eng/products.json') -Raw | ConvertFrom-Json

    $lockPath = Join-Path $root 'src/Aspose.Cli/packages.lock.json'
    $locked = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $framework = $locked.dependencies.PSObject.Properties | Where-Object Name -NotMatch '/' | Select-Object -First 1
    if ($null -eq $framework) { throw "No target framework found in $lockPath" }
    $engines = @()
    foreach ($product in @($catalog.products | Sort-Object id)) {
        $package = $framework.Value.PSObject.Properties[[string]$product.sdkPackageId].Value
        if ($null -eq $package -or $package.resolved -cne [string]$product.sdkVersion -or [string]::IsNullOrWhiteSpace([string]$package.contentHash)) { throw "Locked SDK does not match product '$($product.id)'." }
        if ([Convert]::FromBase64String([string]$package.contentHash).Length -ne 64) { throw "Invalid SDK content hash: $($product.sdkPackageId)" }
        $engines += [ordered]@{ product=[string]$product.id; packageId=[string]$product.sdkPackageId; version=[string]$package.resolved; contentHash=[string]$package.contentHash }
    }

    return [pscustomobject]@{
        SourceRevision = $revision
        BuildDirty = ($dirty.Count -ne 0)
        EnginePackages = $engines
        DirtyDetails = $dirty
    }
}

function Get-ArtifactRelativePath {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Path
    )

    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    $comparison = if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        [StringComparison]::OrdinalIgnoreCase
    } else {
        [StringComparison]::Ordinal
    }
    if (-not $fullPath.StartsWith($rootPrefix, $comparison)) {
        throw "Artifact path escaped its allowed root: $fullPath"
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Assert-SafeArtifactTree {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $AllowedRoot
    )

    $full = [IO.Path]::GetFullPath($Path)
    [void](Get-ArtifactRelativePath -Root $AllowedRoot -Path $full)
    $cursor = $full
    while (-not [string]::IsNullOrEmpty($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Artifact path traverses a reparse point: $($item.FullName)"
            }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    if (-not (Test-Path -LiteralPath $full -PathType Container)) { return }

    $pending = [Collections.Generic.Queue[IO.DirectoryInfo]]::new()
    $pending.Enqueue([IO.DirectoryInfo](Get-Item -LiteralPath $full -Force))
    while ($pending.Count -ne 0) {
        foreach ($item in $pending.Dequeue().EnumerateFileSystemInfos()) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Artifact tree contains a reparse point and was preserved: $($item.FullName)"
            }
            if ($item -is [IO.DirectoryInfo]) { $pending.Enqueue($item) }
        }
    }
}

function Initialize-OwnedArtifactDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $AllowedRoot,
        [Parameter(Mandatory)][ValidateSet('publish','release')][string] $Kind
    )

    $full = [IO.Path]::GetFullPath($Path)
    if ($full.Length -gt [IO.Path]::GetPathRoot($full).Length) {
        $full = $full.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    }
    Assert-SafeArtifactTree $full $AllowedRoot
    $markerPath = "$full.aspose-owner.json"
    if (Test-Path -LiteralPath $markerPath) {
        if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
            throw "Artifact ownership marker is not a file: $markerPath"
        }
        try { $owner = [IO.File]::ReadAllText($markerPath, [Text.Encoding]::UTF8) | ConvertFrom-Json }
        catch { throw "Artifact ownership marker is invalid: $markerPath" }
        if ([int]$owner.schemaVersion -ne 1 -or
            $owner.productId -cne $script:ArtifactOwnerProductId -or
            $owner.kind -cne $Kind -or
            -not [IO.Path]::GetFullPath([string]$owner.target).Equals($full, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Artifact ownership marker does not own '$full': $markerPath"
        }
    }
    elseif (Test-Path -LiteralPath $full) {
        throw "Artifact output exists without its ownership marker and was preserved: $full"
    }
    else {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
        $owner = [ordered]@{
            schemaVersion = 1
            productId = $script:ArtifactOwnerProductId
            kind = $Kind
            target = $full
        }
        Write-StableJson $markerPath $owner
    }

    if (Test-Path -LiteralPath $full -PathType Container) {
        [IO.Directory]::Delete($full, $true)
    }
    elseif (Test-Path -LiteralPath $full) {
        throw "Artifact output is not a directory: $full"
    }
    [IO.Directory]::CreateDirectory($full) | Out-Null
}

function Write-StableJson {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)] $Value
    )

    [IO.File]::WriteAllText(
        $Path,
        ($Value | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
}

function Read-BuildManifest {
    param([Parameter(Mandatory)][string] $Path)

    try { $manifest = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8) | ConvertFrom-Json }
    catch { throw "Build manifest is invalid: $Path" }
    if ([int]$manifest.schemaVersion -ne 1 -or
        $manifest.productId -cne 'aspose-cli' -or
        $manifest.sourceRevision -cnotmatch '^[0-9a-f]{40}$' -or
        $manifest.buildDirty -isnot [bool] -or
        @($manifest.enginePackages).Count -lt 1) {
        throw "Build manifest has invalid provenance fields: $Path"
    }
    return $manifest
}
