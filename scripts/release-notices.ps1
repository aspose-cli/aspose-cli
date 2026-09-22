# Legal files come from the restored archives, not mutable loose cache files.
# The published dependency graph is the package roster, including runtime packs.
function Write-ReleaseNotices {
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $OutputRoot,
        [Parameter(Mandatory)][string] $DependenciesPath,
        [Parameter(Mandatory)][string] $AssetsPath
    )
    $dependencies = Get-Content -LiteralPath $DependenciesPath -Raw | ConvertFrom-Json
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
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
        $archive = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder "$packagePath/$($id.ToLowerInvariant()).$($version.ToLowerInvariant()).nupkg"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $archive = $candidate; break }
        }
        if ($null -eq $archive) { throw "Restored archive is missing for deployed package '$identity'." }
        $cacheMetadata = Get-Content -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($archive)) '.nupkg.metadata') -Raw | ConvertFrom-Json
        $contentHash = [string]$cacheMetadata.contentHash
        if ($library.Value.type -eq 'package' -and
            $contentHash -cne [string]$assets.libraries.PSObject.Properties[$identity].Value.sha512) {
            throw "Restored package content hash differs from the locked assets: $identity"
        }
        # NuGet records the content hash separately from the signed archive hash.
        # Runtime pack versions come from the pinned SDK's published dependency graph.
        $expectedHash = [IO.File]::ReadAllText($archive + '.sha512').Trim()
        $stream = [IO.File]::OpenRead($archive)
        try { $actualHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ([string]::IsNullOrWhiteSpace($expectedHash) -or $actualHash -cne $expectedHash) {
            throw "Restored package hash differs from its restore metadata: $identity"
        }
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
                package = $id; version = $version; contentHash = $contentHash; archiveSha512 = $actualHash
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
