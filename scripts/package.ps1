<#
.SYNOPSIS
Builds, signs and verifies one customer-installable Aspose CLI archive.

.DESCRIPTION
Without a mode switch the script stages and signs on one trusted machine. A release
pipeline separates the two so that building and testing never see signing material:

  -StageOnly    Publishes a clean build and stages the unsigned release payload in
                artifacts/publish/<runtime>. No signing input is read.
  -SignStaged   Signs a payload staged by -StageOnly from this same source revision, without
                rebuilding: Authenticode, package checksums and signature, an AllSigned
                install/update/uninstall smoke test, the archive and its signed manifest.
  -PrepareOnly  Stages an unsigned local development package that also contains install.cmd.

Signing inputs (parameter, or environment variable):
  -SigningKey / ASPOSE_CLI_RELEASE_SIGNING_KEY
      ECDSA P-256 package key: a passphrase-protected PEM file, or an OpenSSL store URI such as
      a PKCS#11 URI of a hardware key (configure its provider through OPENSSL_CONF).
  ASPOSE_CLI_RELEASE_SIGNING_KEY_PASSPHRASE
      Passphrase or PIN. OpenSSL reads it from the environment (-passin env:); it never
      appears on a command line. Unencrypted key files are refused.
  -OpenSslPath / ASPOSE_CLI_OPENSSL_PATH
      OpenSSL 3; otherwise openssl.exe from PATH.
  -AuthenticodeToolPath / ASPOSE_CLI_AUTHENTICODE_TOOL
      signtool.exe.
  -AuthenticodeCertificateThumbprint / ASPOSE_CLI_AUTHENTICODE_CERTIFICATE_THUMBPRINT
      SHA-1 thumbprint of the code-signing certificate in CurrentUser\My or LocalMachine\My.
      Its private key may live in an HSM behind a CSP/KSP; no password is passed to any tool.
  -AuthenticodeTimestampServer / ASPOSE_CLI_AUTHENTICODE_TIMESTAMP_SERVER
      RFC 3161 timestamp URL.
The AllSigned smoke test runs the signed installer, so the signing certificate must be
trusted on the signing machine (its chain trusted, and the certificate itself in the
current user's or machine's Trusted Publishers store).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $RuntimeIdentifier = 'win-x64',

    [switch] $PrepareOnly,

    [switch] $StageOnly,

    [switch] $SignStaged,

    [string] $SigningKey,

    [string] $OpenSslPath,

    [string] $AuthenticodeToolPath,

    [string] $AuthenticodeCertificateThumbprint,

    [string] $AuthenticodeTimestampServer
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'release-common.ps1')
if (@(@($PrepareOnly, $StageOnly, $SignStaged) | Where-Object { $_ }).Count -gt 1) {
    throw '-PrepareOnly, -StageOnly and -SignStaged select different modes; use at most one.'
}
$passphraseVariable = 'ASPOSE_CLI_RELEASE_SIGNING_KEY_PASSPHRASE'

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
if (-not $SignStaged) {
    & (Join-Path $PSScriptRoot 'publish.ps1') `
        -Configuration $Configuration `
        -RuntimeIdentifier $RuntimeIdentifier `
        -RequireClean:(-not $PrepareOnly)
}
elseif (-not (Test-Path -LiteralPath $publishRoot -PathType Container)) {
    throw "-SignStaged needs the payload staged by -StageOnly at $publishRoot."
}

$buildManifestPath = Join-Path $publishRoot $script:BuildManifestName
$buildManifest = Read-BuildManifest $buildManifestPath
if ($buildManifest.edition -cne $layout.Edition -or
    $buildManifest.runtimeIdentifier -cne $RuntimeIdentifier -or
    [bool]([bool]$buildManifest.buildDirty -and -not $PrepareOnly)) {
    throw 'Published build manifest does not describe this clean package build.'
}
if ($SignStaged) {
    # Sign only what this checkout staged: the same source revision, the same installer and
    # nothing that a signing run would itself produce.
    $provenance = Get-RepositoryProvenance -RepositoryRoot $repoRoot
    if ($provenance.BuildDirty -or $buildManifest.sourceRevision -cne $provenance.SourceRevision) {
        throw "The staged payload was built from $($buildManifest.sourceRevision), but this clean checkout is at $($provenance.SourceRevision)."
    }
    foreach ($forbidden in @('install.cmd','SHA256SUMS','PACKAGE-SIGNATURE.json','PACKAGE-SIGNATURE.sig')) {
        if (Test-Path -LiteralPath (Join-Path $publishRoot $forbidden)) { throw "The staged payload unexpectedly contains '$forbidden'." }
    }
    $stagedInstaller = Join-Path $publishRoot 'install.ps1'
    if (-not (Test-Path -LiteralPath $stagedInstaller -PathType Leaf) -or
        (Get-FileHash -LiteralPath $stagedInstaller -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath (Join-Path $repoRoot 'install.ps1') -Algorithm SHA256).Hash) {
        throw 'The staged install.ps1 is missing or differs from this checkout.'
    }
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

function Get-SigningKey {
    $candidate = if ([string]::IsNullOrWhiteSpace($SigningKey)) {
        $env:ASPOSE_CLI_RELEASE_SIGNING_KEY
    }
    else {
        $SigningKey
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        throw 'Formal customer packaging requires -SigningKey or ASPOSE_CLI_RELEASE_SIGNING_KEY. Use -PrepareOnly only for local development installation.'
    }
    # An OpenSSL store URI (for example pkcs11:...) names a key that never leaves its store.
    if ($candidate -match '^[A-Za-z][A-Za-z0-9+.-]+:(?![\\/])') { return $candidate }
    $resolved = [IO.Path]::GetFullPath($candidate)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "The requested release signing key does not exist: $resolved"
    }
    $pem = [IO.File]::ReadAllText($resolved)
    if ($pem -notmatch '-----BEGIN ENCRYPTED PRIVATE KEY-----' -and $pem -notmatch 'Proc-Type: 4,ENCRYPTED') {
        throw "The release signing key '$resolved' is not passphrase-protected. Encrypt it (openssl pkcs8 -topk8 -v2 aes-256-cbc) or use a hardware-backed OpenSSL store URI."
    }
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($passphraseVariable))) {
        throw "The passphrase-protected release signing key needs $passphraseVariable."
    }
    return $resolved
}

# OpenSSL reads the passphrase or PIN from the environment, never from its command line.
function Get-SigningKeyArguments {
    param([Parameter(Mandatory)][string] $Key)
    $arguments = @($Key)
    if (-not [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($passphraseVariable))) {
        $arguments += @('-passin', "env:$passphraseVariable")
    }
    return $arguments
}

function Get-OpenSslTool {
    $candidate = if ([string]::IsNullOrWhiteSpace($OpenSslPath)) {
        $env:ASPOSE_CLI_OPENSSL_PATH
    }
    else {
        $OpenSslPath
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $command = Get-Command openssl.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
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
    $thumbprintValue = if ([string]::IsNullOrWhiteSpace($AuthenticodeCertificateThumbprint)) {
        $env:ASPOSE_CLI_AUTHENTICODE_CERTIFICATE_THUMBPRINT
    }
    else { $AuthenticodeCertificateThumbprint }
    if ([string]::IsNullOrWhiteSpace($toolValue) -and
        [string]::IsNullOrWhiteSpace($thumbprintValue) -and -not $Required) {
        return $null
    }
    if ([string]::IsNullOrWhiteSpace($toolValue) -or
        [string]::IsNullOrWhiteSpace($thumbprintValue)) {
        throw 'Formal customer packaging requires an Authenticode signing tool and a certificate thumbprint. Supply the parameters or ASPOSE_CLI_AUTHENTICODE_TOOL and ASPOSE_CLI_AUTHENTICODE_CERTIFICATE_THUMBPRINT.'
    }
    $tool = [IO.Path]::GetFullPath($toolValue)
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Authenticode signing tool was not found: $tool" }
    # The certificate comes from a Windows certificate store, so a hardware-backed key works
    # through its CSP/KSP and no tool ever receives a password.
    $thumbprint = ($thumbprintValue -replace '[\s‎]', '').ToUpperInvariant()
    if ($thumbprint -cnotmatch '^[0-9A-F]{40}$') { throw 'The Authenticode certificate thumbprint must be 40 hexadecimal characters.' }
    $certificate = $null
    $machineStore = $false
    foreach ($location in @('CurrentUser', 'LocalMachine')) {
        $store = [Security.Cryptography.X509Certificates.X509Store]::new('My', $location)
        try {
            $store.Open([Security.Cryptography.X509Certificates.OpenFlags]'ReadOnly, OpenExistingOnly')
            $found = @($store.Certificates.Find('FindByThumbprint', $thumbprint, $false))
            if ($found.Count -eq 1) { $certificate = $found[0]; $machineStore = $location -ceq 'LocalMachine'; break }
        }
        catch [Security.Cryptography.CryptographicException] { }
        finally { $store.Dispose() }
    }
    if ($null -eq $certificate -or -not $certificate.HasPrivateKey) {
        throw "No code-signing certificate with a private key and thumbprint $thumbprint is in CurrentUser\My or LocalMachine\My."
    }
    $timestamp = if ([string]::IsNullOrWhiteSpace($AuthenticodeTimestampServer)) {
        $env:ASPOSE_CLI_AUTHENTICODE_TIMESTAMP_SERVER
    } else { $AuthenticodeTimestampServer }
    $timestampUri = $null
    if (-not [Uri]::TryCreate($timestamp, [UriKind]::Absolute, [ref]$timestampUri) -or
        $timestampUri.Scheme -notin @('http','https') -or $timestampUri.UserInfo) {
        throw 'Authenticode signing requires a credential-free timestamp server URL. Supply -AuthenticodeTimestampServer or ASPOSE_CLI_AUTHENTICODE_TIMESTAMP_SERVER.'
    }
    return [pscustomobject]@{
        Tool = $tool
        Thumbprint = $thumbprint
        Certificate = $certificate
        MachineStore = $machineStore
        TimestampServer = $timestamp
    }
}

function Assert-TimestampedAuthenticode {
    param([Parameter(Mandatory)][string] $Path)
    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ([string]$signature.Status -cne 'Valid' -or $null -eq $signature.TimeStamperCertificate) {
        throw "Authenticode signature must be valid and timestamped: $Path"
    }
}

function Invoke-ExecutableAuthenticodeHook {
    param(
        [Parameter(Mandatory)][string] $Executable,
        [switch] $Required
    )
    $inputs = Get-AuthenticodeInputs -Required:$Required
    if ($null -eq $inputs) { return }
    $store = if ($inputs.MachineStore) { @('/sm') } else { @() }
    Invoke-SigningTool $inputs.Tool (@('sign','/sha1',$inputs.Thumbprint) + $store + @('/fd','SHA256','/tr',$inputs.TimestampServer,'/td','SHA256',$Executable)) 'Authenticode signing'
    Invoke-SigningTool $inputs.Tool @('verify','/pa','/all',$Executable) 'Authenticode verification'
    Assert-TimestampedAuthenticode $Executable
}

function Invoke-PowerShellAuthenticodeHook {
    param(
        [Parameter(Mandatory)][string] $Script
    )
    $inputs = Get-AuthenticodeInputs -Required
    Import-Module `
        (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1') `
        -ErrorAction Stop
    $signature = Set-AuthenticodeSignature `
        -FilePath $Script `
        -Certificate $inputs.Certificate `
        -HashAlgorithm SHA256 `
        -TimestampServer $inputs.TimestampServer
    if ([string]$signature.Status -cne 'Valid') {
        throw "PowerShell installer Authenticode signing failed: $($signature.StatusMessage)"
    }
    Assert-TimestampedAuthenticode $Script
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
        Invoke-SigningTool $OpenSsl (@('pkey','-in') + (Get-SigningKeyArguments $SigningKey) + @('-pubout','-outform','DER','-out',$publicDer)) 'Package public-key extraction'
        Invoke-SigningTool $OpenSsl (@('pkey','-in') + (Get-SigningKeyArguments $SigningKey) + @('-pubout','-out',$publicPem)) 'Package public-key export'
        $keyId = (Get-FileHash -LiteralPath $publicDer -Algorithm SHA256).Hash.ToLowerInvariant()
        Invoke-SigningTool $OpenSsl (@('dgst','-sha256','-sign') + (Get-SigningKeyArguments $SigningKey) + @('-out',$rawSignature,$checksumPath)) 'Package checksum signing'
        Invoke-SigningTool $OpenSsl @('dgst','-sha256','-verify',$publicPem,'-signature',$rawSignature,$checksumPath) 'Package checksum signature verification'
        Write-StableJson (Join-Path $Root 'PACKAGE-SIGNATURE.json') ([ordered]@{
            schemaVersion = 1
            productId = [string]$layout.Identity.id
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

if (-not $SignStaged) {
    foreach ($packageFile in @('install.cmd','install.ps1','SHA256SUMS','PACKAGE-SIGNATURE.json','PACKAGE-SIGNATURE.sig')) {
        Remove-Item -LiteralPath (Join-Path $publishRoot $packageFile) -Force -ErrorAction SilentlyContinue
    }
    $installerNames = if ($PrepareOnly) { @('install.cmd', 'install.ps1') } else { @('install.ps1') }
    foreach ($installerName in $installerNames) {
        Copy-Item `
            -LiteralPath (Join-Path $repoRoot $installerName) `
            -Destination (Join-Path $publishRoot $installerName)
    }
}
if ($StageOnly) {
    Write-Host "Unsigned release payload staged for signing at $publishRoot"
    return
}
$signingKey = $null
$openSsl = $null
if (-not $PrepareOnly) {
    $signingKey = Get-SigningKey
    $openSsl = Get-OpenSslTool
}

Invoke-ExecutableAuthenticodeHook (Join-Path $publishRoot $layout.Names.ExecutableName) -Required:(-not $PrepareOnly)
if (-not $PrepareOnly) {
    Invoke-PowerShellAuthenticodeHook (Join-Path $publishRoot 'install.ps1')
}
$verifiedFiles = @(
    Get-ChildItem -LiteralPath $publishRoot -File -Recurse |
        Where-Object {
            (Get-ArtifactRelativePath -Root $publishRoot -Path $_.FullName) -cne 'SHA256SUMS'
        } |
        Sort-Object FullName
)
# The signed installer is installed with the payload so an installation can update and
# uninstall itself; the unsigned development entry point is not.
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
$trustRingVariable = [string]$layout.Identity.environmentVariablePrefix + 'RELEASE_TRUSTED_KEYS'
$previousTrustRing = [Environment]::GetEnvironmentVariable($trustRingVariable, 'Process')
[Environment]::SetEnvironmentVariable($trustRingVariable, $smokeTrustRing, 'Process')
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
        @('-InstallDirectory', $smokeInstall, '-SkipPath', '-SkillsRoot', $smokeSkills, '-SkipLicensePrompt', '-SkipMcp'),
        @('-InstallDirectory', $smokeInstall, '-Update'))) {
        & $powerShell `
            -NoLogo `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy AllSigned `
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
    # The installed, signed copy of the installer uninstalls its own installation.
    & $powerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy AllSigned `
        -File (Join-Path $smokeInstall 'install.ps1') -Uninstall
    if ($LASTEXITCODE -ne 0) {
        throw "Customer uninstall smoke test failed with exit code $LASTEXITCODE."
    }
    if ((Test-Path -LiteralPath $smokeInstall) -or @(Get-ChildItem -LiteralPath $smokeSkills -Force).Count -ne 0) {
        throw 'Customer uninstall left installation files or Skills behind.'
    }
}
finally {
    [Environment]::SetEnvironmentVariable($trustRingVariable, $previousTrustRing, 'Process')
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
$releaseManifestPath = Join-Path $releaseRoot 'RELEASE-MANIFEST.json'
$signaturePath = 'RELEASE-MANIFEST.sig'
$payloadPath = Join-Path $releaseRoot ('.release-signing-' + [Guid]::NewGuid().ToString('N') + '.txt')
$publicKeyPath = Join-Path $releaseRoot ('.release-signing-' + [Guid]::NewGuid().ToString('N') + '.der')
$rawSignaturePath = Join-Path $releaseRoot ('.release-signing-' + [Guid]::NewGuid().ToString('N') + '.bin')
try {
    Invoke-SigningTool $openSsl (@('pkey','-in') + (Get-SigningKeyArguments $signingKey) + @('-pubout','-outform','DER','-out',$publicKeyPath)) 'Release public-key extraction'
    $keyId = (Get-FileHash -LiteralPath $publicKeyPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $engineLines = @($buildManifest.enginePackages | Sort-Object product | ForEach-Object {
        "enginePackages.$($_.product).packageId=$($_.packageId)"
        "enginePackages.$($_.product).version=$($_.version)"
        "enginePackages.$($_.product).contentHash=$($_.contentHash)"
    })
    $signedPayload = @(
        [string]$layout.Names.ReleaseSignatureDomain,
        "productId=$($layout.Identity.id)",
        "edition=$($layout.Edition)",
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
    Invoke-SigningTool $openSsl (@('dgst','-sha256','-sign') + (Get-SigningKeyArguments $signingKey) + @('-out',$rawSignaturePath,$payloadPath)) 'Release manifest signing'
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
$releaseManifest = [ordered]@{
    schemaVersion = 1
    productId = [string]$layout.Identity.id
    edition = $layout.Edition
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

Write-Host "Customer archive passed publish, checksum, install, update, uninstall and launch verification: $archivePath"
