# Names derived from eng/distribution.json; the layout resolver is their only author.
$script:ReleaseRepositoryRoot = Split-Path -Parent $PSScriptRoot
$script:ProjectLayout = & (Join-Path $PSScriptRoot 'resolve-project-layout.ps1') -RepositoryRoot $script:ReleaseRepositoryRoot
$script:ArtifactOwnerProductId = $script:ProjectLayout.Names.ArtifactOwnerProductId
$script:BuildManifestName = $script:ProjectLayout.Names.BuildManifestName

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
        $manifest.productId -cne $script:ProjectLayout.Identity.id -or
        $manifest.sourceRevision -cnotmatch '^[0-9a-f]{40}$' -or
        $manifest.buildDirty -isnot [bool] -or
        @($manifest.enginePackages).Count -lt 1) {
        throw "Build manifest has invalid provenance fields: $Path"
    }
    return $manifest
}

# NuGet's own implementation computes a package's content hash: the SHA-512 recorded in
# packages.lock.json, which excludes a repository signature. It ships with the active SDK
# (selected by global.json) and targets .NET 8, so PowerShell 7.4 or later loads it.
function Initialize-NuGetPackaging {
    if ($null -ne ('NuGet.Packaging.PackageArchiveReader' -as [type])) { return }
    Push-Location -LiteralPath $script:ReleaseRepositoryRoot
    try {
        $version = ([string](& dotnet --version | Select-Object -Last 1)).Trim()
        $sdks = @(& dotnet --list-sdks)
    }
    finally { Pop-Location }
    $sdkDirectory = $null
    foreach ($line in $sdks) {
        if ($line -match '^(?<version>\S+) \[(?<root>.+)\]$' -and $Matches.version -ceq $version) {
            $sdkDirectory = Join-Path $Matches.root $version
        }
    }
    if ($null -eq $sdkDirectory) { throw "The active .NET SDK $version was not found by 'dotnet --list-sdks'." }
    foreach ($name in @('NuGet.Common', 'NuGet.Frameworks', 'NuGet.Versioning', 'NuGet.Configuration', 'NuGet.Packaging')) {
        Add-Type -LiteralPath (Join-Path $sdkDirectory "$name.dll")
    }
}

function Get-NuGetContentHash {
    param([Parameter(Mandatory)][string] $Path)
    Initialize-NuGetPackaging
    $reader = [NuGet.Packaging.PackageArchiveReader]::new($Path)
    try { return $reader.GetContentHash([Threading.CancellationToken]::None) }
    finally { $reader.Dispose() }
}

function Find-RestoredPackageArchive {
    param(
        [Parameter(Mandatory)] $PackageFolders,
        [Parameter(Mandatory)][string] $Id,
        [Parameter(Mandatory)][string] $Version
    )
    foreach ($folder in @($PackageFolders.PSObject.Properties.Name)) {
        $candidate = Join-Path $folder "$($Id.ToLowerInvariant())/$($Version.ToLowerInvariant())/$($Id.ToLowerInvariant()).$($Version.ToLowerInvariant()).nupkg"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw "Restored archive is missing for package '$Id/$Version'."
}

# Runtime packs are NuGet downloads that packages.lock.json does not record, and the SDK's patch
# level selects their version. eng/runtime-packs.lock.json pins every pack the launcher restore
# downloads, so a different SDK cannot silently change the shipped runtime.
function Get-RuntimePackLock {
    param([Parameter(Mandatory)][string] $AssetsPath)
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
    $packs = @(
        foreach ($framework in @($assets.project.frameworks.PSObject.Properties)) {
            foreach ($download in @($framework.Value.downloadDependencies)) {
                if ($null -eq $download) { continue }
                if (-not ([string]$download.version -cmatch '^\[(?<version>[^,\]\s]+), ?(?<upper>[^,\]\s]+)\]$') -or $Matches.version -cne $Matches.upper) {
                    throw "Runtime pack '$($download.name)' is not pinned to one version: $($download.version)"
                }
                $version = $Matches.version
                $archive = Find-RestoredPackageArchive $assets.packageFolders ([string]$download.name) $version
                [pscustomobject][ordered]@{ id = [string]$download.name; version = $version; contentHash = Get-NuGetContentHash $archive }
            }
        })
    return [ordered]@{ schemaVersion = 1; runtimePacks = @($packs | Sort-Object id -Unique) }
}

function Read-RuntimePackLock {
    param([Parameter(Mandatory)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "The runtime pack lock is missing: $Path. Run scripts/sync.ps1." }
    $lock = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($lock.schemaVersion -ne 1 -or $null -eq $lock.runtimePacks) { throw "The runtime pack lock is invalid: $Path. Run scripts/sync.ps1." }
    return @($lock.runtimePacks)
}

# Every pack the current restore downloads must be the one the committed lock pins.
function Assert-RuntimePackLock {
    param([Parameter(Mandatory)][string] $AssetsPath, [Parameter(Mandatory)][string] $LockPath)
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
    $downloaded = @(
        foreach ($framework in @($assets.project.frameworks.PSObject.Properties)) {
            foreach ($download in @($framework.Value.downloadDependencies)) {
                if ($null -ne $download) { "$($download.name)/$(([string]$download.version).Trim('[', ']').Split(',')[0].Trim())" }
            }
        }) | Sort-Object -Unique
    $locked = @(Read-RuntimePackLock $LockPath | ForEach-Object { "$($_.id)/$($_.version)" }) | Sort-Object -Unique
    if (@(Compare-Object @($downloaded) @($locked)).Count -ne 0) {
        throw "Restored runtime packs [$($downloaded -join ', ')] differ from eng/runtime-packs.lock.json [$($locked -join ', ')]. The active SDK selects a different runtime; run scripts/sync.ps1 and review the lock change."
    }
}

# The managed assemblies a single-file executable bundles, each with its code form: R2R when it
# carries precompiled code, IL when it carries only IL. Reads the .NET single-file bundle manifest.
function Get-BundledAssemblyCode {
    param([Parameter(Mandatory)][string] $Executable)
    $bytes = [IO.File]::ReadAllBytes($Executable)
    # The bundle marker the host searches for; the manifest offset precedes it.
    $marker = [byte[]](0x8b,0x12,0x02,0xb9,0x6a,0x61,0x20,0x38,0x72,0x7b,0x93,0x02,0x14,0xd7,0xa0,0x32,
        0x13,0xf5,0xb9,0xe6,0xef,0xae,0x33,0x18,0xee,0x3b,0x2d,0xce,0x24,0xb3,0x6a,0xae)
    $position = -1
    $start = 0
    while ($position -lt 0) {
        $candidate = [Array]::IndexOf($bytes, $marker[0], $start)
        if ($candidate -lt 8 -or $candidate -gt $bytes.Length - $marker.Length) {
            throw "Not a single-file bundle: $Executable"
        }
        if ([Linq.Enumerable]::SequenceEqual([ArraySegment[byte]]::new($bytes, $candidate, $marker.Length), $marker)) {
            $position = $candidate
        }
        $start = $candidate + 1
    }
    $reader = [IO.BinaryReader]::new([IO.MemoryStream]::new($bytes, $false))
    $reader.BaseStream.Position = [BitConverter]::ToInt64($bytes, $position - 8)
    $major = $reader.ReadUInt32()
    [void]$reader.ReadUInt32()
    $count = $reader.ReadInt32()
    [void]$reader.ReadString()
    if ($major -lt 6) { throw "Unsupported single-file bundle version $major in $Executable." }
    # The deps.json and runtimeconfig.json locations, then the bundle flags.
    foreach ($field in 1..4) { [void]$reader.ReadInt64() }
    [void]$reader.ReadUInt64()
    foreach ($entry in 1..$count) {
        $offset = $reader.ReadInt64()
        $size = $reader.ReadInt64()
        $compressedSize = $reader.ReadInt64()
        [void]$reader.ReadByte()
        $path = $reader.ReadString()
        if (-not $path.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase)) { continue }
        $content = [IO.MemoryStream]::new()
        if ($compressedSize -gt 0) {
            $inflate = [IO.Compression.DeflateStream]::new(
                [IO.MemoryStream]::new($bytes, [int]$offset, [int]$compressedSize, $false),
                [IO.Compression.CompressionMode]::Decompress)
            $inflate.CopyTo($content)
            $inflate.Dispose()
        }
        else {
            $content.Write($bytes, [int]$offset, [int]$size)
        }
        $content.Position = 0
        $pe = [Reflection.PortableExecutable.PEReader]::new($content)
        try {
            $cor = $pe.PEHeaders.CorHeader
            if ($null -eq $cor) { continue }
            [pscustomobject]@{
                Name = [IO.Path]::GetFileNameWithoutExtension($path)
                Code = $(if ($cor.ManagedNativeHeaderDirectory.Size -gt 0) { 'R2R' } else { 'IL' })
            }
        }
        finally { $pe.Dispose() }
    }
}
