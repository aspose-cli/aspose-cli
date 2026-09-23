<#
.SYNOPSIS
Builds and transactionally installs this project's development package.
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
    [switch] $SkipMcp
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
& (Join-Path $PSScriptRoot 'package.ps1') -Configuration Release -RuntimeIdentifier win-x64 -PrepareOnly
$parameters = @{
    PackageRoot = Join-Path $repoRoot 'artifacts/publish/win-x64'
    InstallDirectory = $installRoot
    DevelopmentPackage = $true
}
if ($SkipPath) { $parameters.SkipPath = $true }
if ($SkipSkills) { $parameters.SkipSkills = $true }
if ($SkillsRoot) { $parameters.SkillsRoot = $SkillsRoot }
if ($SkipMcp) { $parameters.SkipMcp = $true }
if ($LicensePath) { $parameters.LicensePath = $LicensePath }
if ($LicenseProduct) { $parameters.LicenseProduct = $LicenseProduct }
if ($SkipLicensePrompt) { $parameters.SkipLicensePrompt = $true }
& (Join-Path $repoRoot 'install.ps1') @parameters
Write-Host "$($layout.Identity.displayName) installed from this project's current source."
