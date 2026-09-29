<#
.SYNOPSIS
Builds and verifies one installable Aspose CLI release.

.DESCRIPTION
Publishes a clean build, writes the package's per-file SHA256SUMS, runs an install, update and
uninstall smoke test, and writes the GitHub release assets to artifacts/release/<runtime>: the
archive, install.ps1, RELEASE-MANIFEST.json (the archive's name, size and SHA-256, read by the
one-line install and by the update command) and SHA256SUMS.

  -PrepareOnly  Stages a local development package, which also contains install.cmd, and
                stops after its checksum and launch checks.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $RuntimeIdentifier = 'win-x64',

    [switch] $PrepareOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'release-common.ps1')

if ($env:OS -ceq 'Windows_NT') {
    if ($null -eq ('AsposeFilePackage.NativeMethods' -as [type])) {
        Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
namespace AsposeFilePackage {
    public static class NativeMethods {
        [DllImport("kernel32.dll")]
        public static extern uint GetErrorMode();
        [DllImport("kernel32.dll")]
        public static extern uint SetErrorMode(uint mode);
    }
}
'@
    }
    $existingErrorMode = [AsposeFilePackage.NativeMethods]::GetErrorMode()
    [void][AsposeFilePackage.NativeMethods]::SetErrorMode($existingErrorMode -bor 0x00008003)
    $env:DOTNET_EnableCrashReport = '0'
}

function Remove-SmokeDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $AllowedRoot
    )

    foreach ($attempt in 1..10) {
        try {
            Assert-SafeArtifactTree $Path $AllowedRoot
            [IO.Directory]::Delete($Path, $true)
            return
        }
        catch {
            if ($attempt -eq 10) {
                throw
            }
            Start-Sleep -Milliseconds 200
        }
    }
}

function Invoke-VersionDiscovery {
    param([Parameter(Mandatory)][string] $Executable)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.Arguments = '--version'
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.ErrorDialog = $false
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw "Published executable could not start: $Executable" }
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) {
            try { $process.Kill() } catch { }
            throw "Published executable version discovery timed out: $Executable"
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),5000)) {
            throw "Published executable left output pipes open: $Executable"
        }
        if ($process.ExitCode -ne 0) {
            throw "Published executable failed version discovery with exit code $($process.ExitCode): $($stderr.Result)"
        }
        return $stdout.Result.Trim()
    }
    finally { $process.Dispose() }
}

$layout = & (Join-Path $PSScriptRoot 'resolve-project-layout.ps1') `
    -RepositoryRoot $repoRoot
$publishRoot = Join-Path $repoRoot "artifacts/publish/$RuntimeIdentifier"
& (Join-Path $PSScriptRoot 'publish.ps1') `
    -Configuration $Configuration `
    -RuntimeIdentifier $RuntimeIdentifier `
    -RequireClean:(-not $PrepareOnly)

$buildManifestPath = Join-Path $publishRoot $script:BuildManifestName
$buildManifest = Read-BuildManifest $buildManifestPath
if ($buildManifest.runtimeIdentifier -cne $RuntimeIdentifier -or
    [bool]([bool]$buildManifest.buildDirty -and -not $PrepareOnly)) {
    throw 'Published build manifest does not describe this clean package build.'
}
foreach ($packageFile in @('install.cmd','install.ps1','SHA256SUMS')) {
    Remove-Item -LiteralPath (Join-Path $publishRoot $packageFile) -Force -ErrorAction SilentlyContinue
}
$installerNames = if ($PrepareOnly) { @('install.cmd', 'install.ps1') } else { @('install.ps1') }
foreach ($installerName in $installerNames) {
    Copy-Item `
        -LiteralPath (Join-Path $repoRoot $installerName) `
        -Destination (Join-Path $publishRoot $installerName)
}
$verifiedFiles = @(
    Get-ChildItem -LiteralPath $publishRoot -File -Recurse |
        Where-Object {
            (Get-ArtifactRelativePath -Root $publishRoot -Path $_.FullName) -cne 'SHA256SUMS'
        } |
        Sort-Object FullName
)
# The installer is installed with the payload so an installation can update and uninstall
# itself; the development entry point is not.
$payloadFiles = @(
    $verifiedFiles |
        Where-Object {
            (Get-ArtifactRelativePath -Root $publishRoot -Path $_.FullName) -cne 'install.cmd'
        }
)
$checksumLines = @(
    foreach ($file in $verifiedFiles) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relativePath = Get-ArtifactRelativePath -Root $publishRoot -Path $file.FullName
        "$hash  $relativePath"
    }
)
[IO.File]::WriteAllLines(
    (Join-Path $publishRoot 'SHA256SUMS'),
    $checksumLines,
    [Text.UTF8Encoding]::new($false))

$versionText = Invoke-VersionDiscovery (Join-Path $publishRoot $layout.Names.ExecutableName)
$artifactVersion = $versionText.Trim()
if ($artifactVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw "Published executable returned an invalid artifact version: $artifactVersion"
}
$archiveVersion = ($artifactVersion -replace '[^0-9A-Za-z.-]', '-').Trim('-')

if ($PrepareOnly) {
    Write-Host "Local development package passed publish, checksum, and launch verification: $publishRoot"
    return
}

$smokeRoot = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot ('artifacts/package-smoke/' + [Guid]::NewGuid().ToString('N'))))
$smokeInstall = Join-Path $smokeRoot 'installed'
$smokeSkills = Join-Path $smokeRoot 'skills'
[IO.Directory]::CreateDirectory($smokeRoot) | Out-Null
# Keep the smoke installation away from the builder's own configuration.
$configurationVariable = [string]$layout.Identity.environmentVariablePrefix + 'CONFIG_DIR'
$previousConfiguration = [Environment]::GetEnvironmentVariable($configurationVariable, 'Process')
[Environment]::SetEnvironmentVariable($configurationVariable, (Join-Path $smokeRoot 'configuration'), 'Process')
try {
    $installerCommand = Join-Path $publishRoot 'install.ps1'
    $powerShell = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) `
        'WindowsPowerShell\v1.0\powershell.exe'
    # A clean install, then an update that replays the recorded choices.
    foreach ($pass in @(
        @('-InstallDirectory', $smokeInstall, '-SkipPath', '-SkillsRoot', $smokeSkills, '-SkipLicensePrompt'),
        @('-InstallDirectory', $smokeInstall, '-Update'))) {
        & $powerShell `
            -NoLogo `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $installerCommand `
            @pass
        if ($LASTEXITCODE -ne 0) {
            throw "Customer installer smoke test '$($pass -join ' ')' failed with exit code $LASTEXITCODE."
        }
    }
    foreach ($file in $payloadFiles) {
        $relativePath = Get-ArtifactRelativePath -Root $publishRoot -Path $file.FullName
        $installedFile = Join-Path $smokeInstall $relativePath
        if (-not (Test-Path -LiteralPath $installedFile -PathType Leaf)) {
            throw "Customer installer omitted payload file '$relativePath'."
        }
        $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $installedHash = (Get-FileHash -LiteralPath $installedFile -Algorithm SHA256).Hash
        if ($installedHash -cne $sourceHash) {
            throw "Installed payload file '$relativePath' failed checksum verification."
        }
    }
    $markerPath = Join-Path $smokeInstall $layout.Names.MarkerName
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    if ($marker.schemaVersion -ne 3 -or
        $marker.productId -cne [string]$layout.Identity.id -or
        $marker.payloadManifest -cne $layout.Names.PayloadManifestName -or
        $marker.choices.path -or $marker.choices.mcp -or
        $marker.choices.skills -cne 'custom' -or $marker.choices.skillsRoot -cne $smokeSkills) {
        throw 'Customer installer did not publish a valid ownership marker with the recorded choices.'
    }
    $payloadManifestPath = Join-Path $smokeInstall $marker.payloadManifest
    $manifestHash = (Get-FileHash -LiteralPath $payloadManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($manifestHash -cne $marker.payloadManifestSha256) {
        throw 'Installed payload manifest does not match the marker hash.'
    }
    $expectedSkills = @([string]$layout.Identity.skillPrefix + 'platform') + @((Get-Content -LiteralPath $layout.CatalogPath -Raw | ConvertFrom-Json).products |
        ForEach-Object { [string]$layout.Identity.skillPrefix + [string]$_.id })
    $installedSkills = @(Get-ChildItem -LiteralPath $smokeSkills -Directory | ForEach-Object Name)
    $missingSkills = @($expectedSkills | Where-Object { $_ -cnotin $installedSkills })
    $unexpectedSkills = @($installedSkills | Where-Object { $_ -cnotin $expectedSkills })
    if ($missingSkills.Count -ne 0 -or $unexpectedSkills.Count -ne 0) {
        throw "Installed Skills differ from the active catalog. Missing: $($missingSkills -join ', '); unexpected: $($unexpectedSkills -join ', ')."
    }
    # The installed copy of the installer uninstalls its own installation.
    & $powerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $smokeInstall 'install.ps1') -Uninstall
    if ($LASTEXITCODE -ne 0) {
        throw "Customer uninstall smoke test failed with exit code $LASTEXITCODE."
    }
    if ((Test-Path -LiteralPath $smokeInstall) -or @(Get-ChildItem -LiteralPath $smokeSkills -Force).Count -ne 0) {
        throw 'Customer uninstall left installation files or Skills behind.'
    }
}
finally {
    [Environment]::SetEnvironmentVariable($configurationVariable, $previousConfiguration, 'Process')
    if (Test-Path -LiteralPath $smokeRoot -PathType Container) {
        Remove-SmokeDirectory `
            -Path $smokeRoot `
            -AllowedRoot (Join-Path $repoRoot 'artifacts/package-smoke')
    }
}

$releaseRoot = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot "artifacts/release/$RuntimeIdentifier"))
$allowedReleaseRoot = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot "artifacts/release"))
$allowedReleasePrefix = $allowedReleaseRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $releaseRoot.StartsWith($allowedReleasePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output must stay inside $allowedReleaseRoot"
}
Initialize-OwnedArtifactDirectory $releaseRoot $allowedReleaseRoot 'release'

$archiveName = "$($layout.Identity.id)-$archiveVersion-$RuntimeIdentifier.zip"
$archivePath = Join-Path $releaseRoot $archiveName
Compress-Archive `
    -Path (Join-Path $publishRoot '*') `
    -DestinationPath $archivePath `
    -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
# The one-line install downloads install.ps1 on its own, then the archive this manifest names.
Copy-Item -LiteralPath (Join-Path $publishRoot 'install.ps1') -Destination (Join-Path $releaseRoot 'install.ps1')
Write-StableJson (Join-Path $releaseRoot 'RELEASE-MANIFEST.json') ([ordered]@{
    schemaVersion = 1
    productId = [string]$layout.Identity.id
    runtimeIdentifier = $RuntimeIdentifier
    artifactVersion = $artifactVersion
    sourceRevision = [string]$buildManifest.sourceRevision
    archive = [ordered]@{
        path = $archiveName
        size = (Get-Item -LiteralPath $archivePath).Length
        sha256 = $archiveHash
    }
})
$releaseChecksums = @(
    foreach ($asset in @($archiveName, 'install.ps1', 'RELEASE-MANIFEST.json')) {
        "$((Get-FileHash -LiteralPath (Join-Path $releaseRoot $asset) -Algorithm SHA256).Hash.ToLowerInvariant())  $asset"
    }
)
[IO.File]::WriteAllLines((Join-Path $releaseRoot 'SHA256SUMS'), $releaseChecksums, [Text.UTF8Encoding]::new($false))

Write-Host "Release passed publish, checksum, install, update, uninstall and launch verification: $releaseRoot"
