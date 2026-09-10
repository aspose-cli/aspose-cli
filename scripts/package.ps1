<#
.SYNOPSIS
Builds and verifies one customer-installable Aspose CLI archive.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $RuntimeIdentifier = 'win-x64',

    [switch] $PrepareOnly,

    [string] $SigningKeyPath,

    [string] $OpenSslPath,

    [string] $AuthenticodeToolPath,

    [string] $AuthenticodeCertificatePath
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

function Get-PayloadRelativePath {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Path
    )

    $fullRoot = [IO.Path]::GetFullPath($Root)
    $rootPrefix = $fullRoot.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Payload file must stay inside the publish directory: $fullPath"
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
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
if ($buildManifest.edition -cne $layout.Slug -or
    $buildManifest.runtimeIdentifier -cne $RuntimeIdentifier -or
    [bool]([bool]$buildManifest.buildDirty -and -not $PrepareOnly)) {
    throw 'Published build manifest does not describe this clean package build.'
}

function Invoke-SigningTool {
    param(
        [Parameter(Mandatory)][string] $Tool,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Purpose
    )
    $output = @(& $Tool @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "$Purpose failed with exit code ${LASTEXITCODE}: $($output -join ' ')"
    }
}

function Get-SigningKeyPath {
    $candidate = if ([string]::IsNullOrWhiteSpace($SigningKeyPath)) {
        $env:ASPOSE_CLI_RELEASE_SIGNING_KEY
    }
    else {
        $SigningKeyPath
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) { return $null }
    $resolved = [IO.Path]::GetFullPath($candidate)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "The requested release signing key does not exist: $resolved"
    }
    return $resolved
}

function Get-OpenSslTool {
    $candidate = if ([string]::IsNullOrWhiteSpace($OpenSslPath)) {
        $env:ASPOSE_CLI_OPENSSL_PATH
    }
    else {
        $OpenSslPath
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $command = Get-Command openssl.exe -ErrorAction SilentlyContinue
        $candidate = if ($null -eq $command) { $null } else { $command.Source }
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        throw 'Release signing requires OpenSSL. Supply -OpenSslPath or ASPOSE_CLI_OPENSSL_PATH; no signing was performed.'
    }
    return $candidate
}

function Get-AuthenticodeInputs {
    param(
        [switch] $Required
    )
    $toolValue = if ([string]::IsNullOrWhiteSpace($AuthenticodeToolPath)) {
        $env:ASPOSE_CLI_AUTHENTICODE_TOOL
    }
    else { $AuthenticodeToolPath }
    $certificateValue = if ([string]::IsNullOrWhiteSpace($AuthenticodeCertificatePath)) {
        $env:ASPOSE_CLI_AUTHENTICODE_CERTIFICATE
    }
    else { $AuthenticodeCertificatePath }
    if ([string]::IsNullOrWhiteSpace($toolValue) -and
        [string]::IsNullOrWhiteSpace($certificateValue) -and -not $Required) {
        return $null
    }
    if ([string]::IsNullOrWhiteSpace($toolValue) -or
        [string]::IsNullOrWhiteSpace($certificateValue)) {
        throw 'Formal customer packaging requires both an Authenticode signing tool and certificate. Supply the parameters or ASPOSE_CLI_AUTHENTICODE_TOOL and ASPOSE_CLI_AUTHENTICODE_CERTIFICATE.'
    }
    $tool = [IO.Path]::GetFullPath($toolValue)
    $certificate = [IO.Path]::GetFullPath($certificateValue)
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Authenticode signing tool was not found: $tool" }
    if (-not (Test-Path -LiteralPath $certificate -PathType Leaf)) { throw "Authenticode certificate was not found: $certificate" }
    return [pscustomobject]@{ Tool = $tool; Certificate = $certificate }
}

function Invoke-ExecutableAuthenticodeHook {
    param(
        [Parameter(Mandatory)][string] $Executable,
        [switch] $Required
    )
    $inputs = Get-AuthenticodeInputs -Required:$Required
    if ($null -eq $inputs) { return }
    Invoke-SigningTool $inputs.Tool @('sign','/fd','SHA256','/f',$inputs.Certificate,$Executable) 'Authenticode signing'
    Invoke-SigningTool $inputs.Tool @('verify','/pa','/all',$Executable) 'Authenticode verification'
}

function Invoke-PowerShellAuthenticodeHook {
    param(
        [Parameter(Mandatory)][string] $Script,
        [switch] $Required
    )
    $inputs = Get-AuthenticodeInputs -Required:$Required
    if ($null -eq $inputs) { return }
    Import-Module `
        (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1') `
        -ErrorAction Stop
    $certificates = @(
        Get-PfxCertificate -FilePath $inputs.Certificate |
            Where-Object HasPrivateKey
    )
    if ($certificates.Count -ne 1) {
        throw 'Authenticode certificate must contain exactly one code-signing certificate with a private key.'
    }
    $signature = Set-AuthenticodeSignature `
        -FilePath $Script `
        -Certificate $certificates[0] `
        -HashAlgorithm SHA256
    if ([string]$signature.Status -cne 'Valid') {
        throw "PowerShell installer Authenticode signing failed: $($signature.StatusMessage)"
    }
}

function Write-PackageSignature {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $SigningKey,
        [Parameter(Mandatory)][string] $OpenSsl
    )
    $checksumPath = Join-Path $Root 'SHA256SUMS'
    $publicDer = Join-Path $Root ('.package-signing-' + [Guid]::NewGuid().ToString('N') + '.der')
    $publicPem = Join-Path $Root ('.package-signing-' + [Guid]::NewGuid().ToString('N') + '.pem')
    $rawSignature = Join-Path $Root ('.package-signing-' + [Guid]::NewGuid().ToString('N') + '.bin')
    try {
        Invoke-SigningTool $OpenSsl @('pkey','-in',$SigningKey,'-pubout','-outform','DER','-out',$publicDer) 'Package public-key extraction'
        Invoke-SigningTool $OpenSsl @('pkey','-in',$SigningKey,'-pubout','-out',$publicPem) 'Package public-key export'
        $keyId = (Get-FileHash -LiteralPath $publicDer -Algorithm SHA256).Hash.ToLowerInvariant()
        Invoke-SigningTool $OpenSsl @('dgst','-sha256','-sign',$SigningKey,'-out',$rawSignature,$checksumPath) 'Package checksum signing'
        Invoke-SigningTool $OpenSsl @('dgst','-sha256','-verify',$publicPem,'-signature',$rawSignature,$checksumPath) 'Package checksum signature verification'
        Write-StableJson (Join-Path $Root 'PACKAGE-SIGNATURE.json') ([ordered]@{
            schemaVersion = 1
            productId = 'aspose-cli'
            algorithm = 'ECDSA-P256-SHA256'
            format = 'rfc3279-der'
            keyId = $keyId
            signedFile = 'SHA256SUMS'
        })
        [IO.File]::WriteAllText(
            (Join-Path $Root 'PACKAGE-SIGNATURE.sig'),
            [Convert]::ToBase64String([IO.File]::ReadAllBytes($rawSignature)) + [Environment]::NewLine,
            [Text.UTF8Encoding]::new($false))
        return [pscustomobject]@{
            KeyId = $keyId
            PublicKeyPem = [IO.File]::ReadAllText($publicPem, [Text.Encoding]::ASCII)
        }
    }
    finally {
        foreach ($temporary in @($publicDer,$publicPem,$rawSignature)) {
            Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        }
    }
}

foreach ($packageFile in @('install.cmd','install.ps1','SHA256SUMS','PACKAGE-SIGNATURE.json','PACKAGE-SIGNATURE.sig')) {
    Remove-Item -LiteralPath (Join-Path $publishRoot $packageFile) -Force -ErrorAction SilentlyContinue
}
$signingKey = $null
$openSsl = $null
if (-not $PrepareOnly) {
    $signingKey = Get-SigningKeyPath
    if ($null -eq $signingKey) {
        throw 'Formal customer packaging requires -SigningKeyPath or ASPOSE_CLI_RELEASE_SIGNING_KEY. Use -PrepareOnly only for local development installation.'
    }
    $openSsl = Get-OpenSslTool
}

Invoke-ExecutableAuthenticodeHook (Join-Path $publishRoot 'aspose-cli.exe') -Required:(-not $PrepareOnly)
$installerNames = if ($PrepareOnly) { @('install.cmd', 'install.ps1') } else { @('install.ps1') }
foreach ($installerName in $installerNames) {
    Copy-Item `
        -LiteralPath (Join-Path $repoRoot $installerName) `
        -Destination (Join-Path $publishRoot $installerName)
}
if (-not $PrepareOnly) {
    Invoke-PowerShellAuthenticodeHook (Join-Path $publishRoot 'install.ps1') -Required
}
$verifiedFiles = @(
    Get-ChildItem -LiteralPath $publishRoot -File -Recurse |
        Where-Object {
            (Get-PayloadRelativePath -Root $publishRoot -Path $_.FullName) -cne 'SHA256SUMS'
        } |
        Sort-Object FullName
)
$payloadFiles = @(
    $verifiedFiles |
        Where-Object {
            (Get-PayloadRelativePath -Root $publishRoot -Path $_.FullName) `
                -notin @('install.cmd', 'install.ps1')
        }
)
$checksumLines = @(
    foreach ($file in $verifiedFiles) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relativePath = Get-PayloadRelativePath -Root $publishRoot -Path $file.FullName
        "$hash  $relativePath"
    }
)
[IO.File]::WriteAllLines(
    (Join-Path $publishRoot 'SHA256SUMS'),
    $checksumLines,
    [Text.UTF8Encoding]::new($false))

$versionText = Invoke-VersionDiscovery (Join-Path $publishRoot 'aspose-cli.exe')
$artifactVersion = $versionText.Trim()
if ($artifactVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw "Published executable returned an invalid artifact version: $artifactVersion"
}
$archiveVersion = ($artifactVersion -replace '[^0-9A-Za-z.-]', '-').Trim('-')

if ($PrepareOnly) {
    Write-Host "Unsigned local development package passed publish, checksum, and launch verification: $publishRoot"
    return
}

$packageTrust = Write-PackageSignature $publishRoot $signingKey $openSsl

$smokeRoot = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot ('artifacts/package-smoke/' + [Guid]::NewGuid().ToString('N'))))
$smokeInstall = Join-Path $smokeRoot 'installed'
$smokeSkills = Join-Path $smokeRoot 'skills'
[IO.Directory]::CreateDirectory($smokeRoot) | Out-Null
$smokeTrustRing = Join-Path $smokeRoot 'release-trust-ring.json'
Write-StableJson $smokeTrustRing ([ordered]@{
    keys = @([ordered]@{ keyId = $packageTrust.KeyId; publicKeyPem = $packageTrust.PublicKeyPem })
})
$previousTrustRing = [Environment]::GetEnvironmentVariable('ASPOSE_CLI_RELEASE_TRUSTED_KEYS', 'Process')
[Environment]::SetEnvironmentVariable('ASPOSE_CLI_RELEASE_TRUSTED_KEYS', $smokeTrustRing, 'Process')
try {
    $installerCommand = Join-Path $publishRoot 'install.ps1'
    $powerShell = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) `
        'WindowsPowerShell\v1.0\powershell.exe'
    foreach ($pass in 1..2) {
        & $powerShell `
            -NoLogo `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy AllSigned `
            -File $installerCommand `
            -InstallDirectory $smokeInstall `
            -SkipPath `
            -SkillsRoot $smokeSkills `
            -SkipLicensePrompt `
            -SkipMcp
        if ($LASTEXITCODE -ne 0) {
            throw "Customer installer smoke test pass $pass failed with exit code $LASTEXITCODE."
        }
    }
    foreach ($file in $payloadFiles) {
        $relativePath = Get-PayloadRelativePath -Root $publishRoot -Path $file.FullName
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
    $markerPath = Join-Path $smokeInstall '.aspose-cli-install.json'
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    if ($marker.schemaVersion -ne 2 -or
        $marker.productId -cne 'aspose-cli' -or
        $marker.payloadManifest -cne '.aspose-cli-payload.json') {
        throw 'Customer installer did not publish a valid v2 ownership marker.'
    }
    $payloadManifestPath = Join-Path $smokeInstall $marker.payloadManifest
    $manifestHash = (Get-FileHash -LiteralPath $payloadManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($manifestHash -cne $marker.payloadManifestSha256) {
        throw 'Installed payload manifest does not match the v2 marker hash.'
    }
    $installedSkills = @(Get-ChildItem -LiteralPath $smokeSkills -Directory | Where-Object { $_.Name -in @('aspose-cli-cells','aspose-cli-pdf','aspose-cli-slides','aspose-cli-words') })
    if ($installedSkills.Count -ne 4) {
        throw 'Customer installer did not publish all four bundled Skills to the isolated root.'
    }
}
finally {
    [Environment]::SetEnvironmentVariable('ASPOSE_CLI_RELEASE_TRUSTED_KEYS', $previousTrustRing, 'Process')
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

$archiveName = "aspose-cli-$archiveVersion-$RuntimeIdentifier.zip"
$archivePath = Join-Path $releaseRoot $archiveName
Compress-Archive `
    -Path (Join-Path $publishRoot '*') `
    -DestinationPath $archivePath `
    -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$releaseManifestPath = Join-Path $releaseRoot 'RELEASE-MANIFEST.json'
$signaturePath = 'RELEASE-MANIFEST.sig'
$signature = $null
if ($null -ne $signingKey) {
    $payloadPath = Join-Path $releaseRoot ('.release-signing-' + [Guid]::NewGuid().ToString('N') + '.txt')
    $publicKeyPath = Join-Path $releaseRoot ('.release-signing-' + [Guid]::NewGuid().ToString('N') + '.der')
    $rawSignaturePath = Join-Path $releaseRoot ('.release-signing-' + [Guid]::NewGuid().ToString('N') + '.bin')
    try {
        Invoke-SigningTool $openSsl @('pkey','-in',$signingKey,'-pubout','-outform','DER','-out',$publicKeyPath) 'Release public-key extraction'
        $keyId = (Get-FileHash -LiteralPath $publicKeyPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $engineLines = @($buildManifest.enginePackages | Sort-Object product | ForEach-Object {
            "enginePackages.$($_.product).packageId=$($_.packageId)"
            "enginePackages.$($_.product).version=$($_.version)"
            "enginePackages.$($_.product).contentHash=$($_.contentHash)"
        })
        $signedPayload = @(
            'aspose-cli-release-v1',
            'productId=aspose-cli',
            "edition=$($layout.Slug)",
            "runtimeIdentifier=$RuntimeIdentifier",
            "artifactVersion=$artifactVersion",
            "sourceRevision=$($buildManifest.sourceRevision)",
            'buildDirty=false',
            $engineLines,
            "archive.path=$archiveName",
            "archive.size=$((Get-Item -LiteralPath $archivePath).Length)",
            "archive.sha256=$archiveHash",
            'signature.status=signed',
            'signature.algorithm=ECDSA-P256-SHA256',
            'signature.format=rfc3279-der',
            "signature.keyId=$keyId",
            "signature.path=$signaturePath",
            ''
        ) -join "`n"
        [IO.File]::WriteAllText($payloadPath, $signedPayload, [Text.UTF8Encoding]::new($false))
        Invoke-SigningTool $openSsl @('dgst','-sha256','-sign',$signingKey,'-out',$rawSignaturePath,$payloadPath) 'Release manifest signing'
        Invoke-SigningTool $openSsl @('dgst','-sha256','-verify',$publicKeyPath,'-signature',$rawSignaturePath,$payloadPath) 'Release signature verification'
        [IO.File]::WriteAllText(
            (Join-Path $releaseRoot $signaturePath),
            [Convert]::ToBase64String([IO.File]::ReadAllBytes($rawSignaturePath)) + [Environment]::NewLine,
            [Text.UTF8Encoding]::new($false))
        $signature = [ordered]@{
            status = 'signed'
            algorithm = 'ECDSA-P256-SHA256'
            format = 'rfc3279-der'
            keyId = $keyId
            path = $signaturePath
        }
    }
    finally {
        foreach ($temporary in @($payloadPath, $publicKeyPath, $rawSignaturePath)) {
            Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        }
    }
}
$releaseManifest = [ordered]@{
    schemaVersion = 1
    productId = 'aspose-cli'
    edition = $layout.Slug
    runtimeIdentifier = $RuntimeIdentifier
    artifactVersion = $artifactVersion
    sourceRevision = [string]$buildManifest.sourceRevision
    buildDirty = $false
    enginePackages = @($buildManifest.enginePackages)
    archive = [ordered]@{
        path = $archiveName
        size = (Get-Item -LiteralPath $archivePath).Length
        sha256 = $archiveHash
    }
    signature = $signature
}
Write-StableJson $releaseManifestPath $releaseManifest
[IO.File]::WriteAllText(
    (Join-Path $releaseRoot 'SHA256SUMS'),
    "$archiveHash  $archiveName$([Environment]::NewLine)",
    [Text.UTF8Encoding]::new($false))

Write-Host "Customer archive passed publish, checksum, install, upgrade, and launch verification: $archivePath"
