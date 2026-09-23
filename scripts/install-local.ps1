<#
.SYNOPSIS
Builds and transactionally installs, updates or uninstalls this project's development package.

.DESCRIPTION
-Update rebuilds the package and replays the choices recorded by the existing installation.
-Uninstall removes the development installation without building anything.
#>
[CmdletBinding()]
param(
    [string] $InstallDirectory,
    [switch] $SkipPath,
    [switch] $SkipSkills,
    [string] $SkillsRoot,
    [string] $LicensePath,
    [string] $LicenseProduct,
    [switch] $SkipLicensePrompt,
    [switch] $SkipMcp,
    [switch] $Update,
    [switch] $Uninstall,
    [switch] $RemoveConfiguration
)
$ErrorActionPreference = 'Stop'
if ($env:OS -cne 'Windows_NT') { throw 'Local installation currently supports Windows only.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$layout = & (Join-Path $PSScriptRoot 'resolve-project-layout.ps1') -RepositoryRoot $repoRoot
$activeProductIds = @((Get-Content -LiteralPath $layout.CatalogPath -Raw | ConvertFrom-Json).products | ForEach-Object { [string]$_.id })
if ($PSBoundParameters.ContainsKey('LicenseProduct') -and $LicenseProduct -cnotin $activeProductIds) {
    throw "Unknown license product '$LicenseProduct'. Active products: $($activeProductIds -join ', ')."
}
$installRoot = if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) $layout.Names.WindowsInstallDirectory
} else { [IO.Path]::GetFullPath($InstallDirectory) }
# The installer itself rejects switches that conflict with -Update or -Uninstall.
$parameters = @{
    InstallDirectory = $installRoot
    DevelopmentPackage = $true
}
foreach ($name in @('SkipPath','SkipSkills','SkillsRoot','SkipMcp','LicensePath','LicenseProduct','SkipLicensePrompt','Update','Uninstall','RemoveConfiguration')) {
    if ($PSBoundParameters.ContainsKey($name)) { $parameters[$name] = $PSBoundParameters[$name] }
}
if (-not $Uninstall) {
    & (Join-Path $PSScriptRoot 'package.ps1') -Configuration Release -RuntimeIdentifier win-x64 -PrepareOnly
    $parameters.PackageRoot = Join-Path $repoRoot 'artifacts/publish/win-x64'
}
& (Join-Path $repoRoot 'install.ps1') @parameters
$outcome = if ($Uninstall) { 'uninstalled' } elseif ($Update) { 'updated from this project''s current source' } else { 'installed from this project''s current source' }
Write-Host "$($layout.Identity.displayName) $outcome."
