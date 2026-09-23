# Legal files come from restored archives whose bytes are bound to the committed lock files:
# packages.lock.json pins NuGet packages and eng/runtime-packs.lock.json pins the runtime packs
# of a self-contained publish. The published dependency manifest is the package roster.
function Get-LockedContentHashes {
    param(
        [Parameter(Mandatory)][string] $PackagesLockPath,
        [Parameter(Mandatory)][string] $RuntimePacksLockPath
    )
    $packages = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    $lock = Get-Content -LiteralPath $PackagesLockPath -Raw | ConvertFrom-Json
    foreach ($target in @($lock.dependencies.PSObject.Properties)) {
        foreach ($entry in @($target.Value.PSObject.Properties)) {
            $hash = [string]$entry.Value.contentHash
            if ([string]::IsNullOrWhiteSpace($hash)) { continue }
            $key = "$($entry.Name)/$($entry.Value.resolved)"
            if ($packages.ContainsKey($key) -and $packages[$key] -cne $hash) { throw "packages.lock.json records two content hashes for $key." }
            $packages[$key] = $hash
        }
    }
    $runtimePacks = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($pack in @(Read-RuntimePackLock $RuntimePacksLockPath)) {
        $runtimePacks["$($pack.id)/$($pack.version)"] = [string]$pack.contentHash
    }
    return [pscustomobject]@{ Packages = $packages; RuntimePacks = $runtimePacks }
}

function Write-ReleaseNotices {
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $OutputRoot,
        [Parameter(Mandatory)][string] $DependenciesPath,
        [Parameter(Mandatory)][string] $AssetsPath,
        [Parameter(Mandatory)][string] $PackagesLockPath,
        [Parameter(Mandatory)][string] $RuntimePacksLockPath
    )
    $dependencies = Get-Content -LiteralPath $DependenciesPath -Raw | ConvertFrom-Json
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
    $locked = Get-LockedContentHashes $PackagesLockPath $RuntimePacksLockPath
    $files = [Collections.Generic.List[string]]::new()
    $packages = [Collections.Generic.List[object]]::new()
    Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'LICENSE') -Destination (Join-Path $OutputRoot 'LICENSE')
    $files.Add('LICENSE')
    foreach ($library in @($dependencies.libraries.PSObject.Properties | Sort-Object Name)) {
        if ($library.Value.type -notin @('package', 'runtimepack')) { continue }
        $identity = $library.Name -replace '^runtimepack\.', ''
        if ($identity -cnotmatch '^[A-Za-z0-9_.-]+/[0-9][A-Za-z0-9_.+-]*$') {
            throw "Invalid deployed package identity: $identity"
        }
        $id, $version = $identity.Split('/')
        $packagePath = $identity.ToLowerInvariant()
        $lockName = if ($library.Value.type -eq 'runtimepack') { 'eng/runtime-packs.lock.json' } else { 'packages.lock.json' }
        $expected = $null
        $pins = if ($library.Value.type -eq 'runtimepack') { $locked.RuntimePacks } else { $locked.Packages }
        if (-not $pins.TryGetValue($identity, [ref]$expected)) {
            throw "Deployed package '$identity' is not pinned by $lockName. Run scripts/sync.ps1 and review the lock change."
        }
        $archive = Find-RestoredPackageArchive $assets.packageFolders $id $version
        # The archive bytes, not a sidecar file in the mutable cache, must produce the locked hash.
        $actual = $null
        try { $actual = Get-NuGetContentHash $archive }
        catch { $actual = "unreadable ($($_.Exception.Message))" }
        if ($actual -cne $expected) {
            throw "Restored package hash differs from $lockName for '$identity': expected $expected, got $actual."
        }
        $stream = [IO.File]::OpenRead($archive)
        try { $archiveSha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData($stream)) }
        finally { $stream.Dispose() }
        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            $nuspec = @($zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.Name.EndsWith('.nuspec') })
            if ($nuspec.Count -ne 1) { throw "Package metadata is missing or ambiguous: $identity" }
            $reader = [IO.StreamReader]::new($nuspec[0].Open())
            try { [xml] $metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $legalEntries = @($zip.Entries | Where-Object {
                $_.Length -gt 0 -and $_.Name -match '(?i)(licen[cs]e|notice|copying|copyright|eula)' -and
                [IO.Path]::GetExtension($_.Name) -in @('', '.txt', '.md', '.pdf', '.html', '.htm')
            } | Sort-Object FullName)
            $license = $metadata.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="license"]')
            $copyrightNode = $metadata.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="copyright"]')
            $copyright = if ($null -ne $copyrightNode) { $copyrightNode.InnerText } else { '' }
            $expression = if ($null -ne $license -and $license.GetAttribute('type') -eq 'expression') { $license.InnerText } else { '' }
            $hasLicense = @($legalEntries | Where-Object Name -Match '(?i)(licen[cs]e|copying|eula)').Count -ne 0
            if (-not $hasLicense -and $expression -notin @('MIT', 'Apache-2.0')) {
                throw "No distributable license text is available for '$identity'; review its upstream license before release."
            }
            $records = [Collections.Generic.List[object]]::new()
            foreach ($entry in @($nuspec[0]) + $legalEntries) {
                $relative = "notices/$packagePath/$($entry.FullName)"
                if ($entry.FullName -match '(^|[/\\])\.\.([/\\]|$)|[:\\]' -or $entry.FullName.StartsWith('/') -or $entry.Length -gt 16MB) {
                    throw "Unsafe or oversized legal file in '$identity'."
                }
                $target = Join-Path $OutputRoot $relative
                [void](Get-ArtifactRelativePath $OutputRoot $target)
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                $inputStream = $entry.Open()
                $output = [IO.File]::Create($target)
                try { $inputStream.CopyTo($output) } finally { $output.Dispose(); $inputStream.Dispose() }
                $files.Add($relative)
                $records.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() })
            }
            if (-not $hasLicense) {
                $relative = "notices/$packagePath/LICENSE.txt"
                $target = Join-Path $OutputRoot $relative
                $terms = [IO.File]::ReadAllText((Join-Path $RepositoryRoot "eng/notices/$expression.txt"))
                if ($expression -ceq 'MIT') {
                    if ([string]::IsNullOrWhiteSpace($copyright)) { throw "MIT attribution is missing for '$identity'; review its upstream license before release." }
                    $terms = $copyright + "`n`n" + $terms
                }
                [IO.File]::WriteAllText($target, $terms, [Text.UTF8Encoding]::new($false))
                $files.Add($relative)
                $records.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() })
            }
            $packages.Add([ordered]@{
                package = $id; version = $version; contentHash = $expected; archiveSha512 = $archiveSha512
                copyright = $copyright
                licenseExpression = $expression; files = @($records)
            })
        }
        finally { $zip.Dispose() }
    }
    Write-StableJson (Join-Path $OutputRoot 'notices/index.json') ([ordered]@{ schemaVersion = 1; packages = @($packages) })
    $files.Add('notices/index.json')
    return $files.ToArray()
}
