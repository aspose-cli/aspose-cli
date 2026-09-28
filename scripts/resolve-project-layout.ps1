<#
.SYNOPSIS
Resolves paths and the fixed identity of this independent CLI project.
#>
[CmdletBinding()]
param([string] $RepositoryRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { Split-Path -Parent $PSScriptRoot } else { [IO.Path]::GetFullPath($RepositoryRoot) }
$identityPath = Join-Path $repoRoot 'eng/distribution.json'
$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
$required = @('schemaVersion','id','commandName','displayName','edition','environmentVariablePrefix','skillPrefix','schemaBaseUri','configurationDirectoryName','installDirectory','releaseRepository','solutionName')
if (@(Compare-Object @($identity.PSObject.Properties.Name | Sort-Object) @($required | Sort-Object)).Count -ne 0 -or $identity.schemaVersion -ne 1) { throw "Invalid distribution identity: $identityPath" }
if ($identity.id -cnotmatch '^[a-z][a-z0-9-]*$' -or $identity.commandName -cne $identity.id -or $identity.skillPrefix -cne ($identity.id + '-') -or $identity.environmentVariablePrefix -cne ($identity.id.Replace('-','_').ToUpperInvariant() + '_') -or $identity.schemaBaseUri -cne ("https://schemas.aspose.dev/" + $identity.id + "/v2/")) { throw "Inconsistent distribution identity: $identityPath" }
# Releases are GitHub Releases of this owner/repository.
if ([string]$identity.releaseRepository -cnotmatch '^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?/[A-Za-z0-9._-]+$') { throw "Invalid release repository in: $identityPath" }
foreach ($relative in @($identity.configurationDirectoryName,$identity.installDirectory,$identity.solutionName)) {
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[/\\])\.\.([/\\]|$)') { throw 'Distribution paths must be project-relative and bounded.' }
}
# Every file and directory name derived from the identity is spelled here once; the generator
# projects these names into install.ps1, and the release scripts read them from this layout.
$names = [pscustomobject][ordered]@{
    ExecutableName = [string]$identity.commandName + '.exe'
    DependencyManifestName = [string]$identity.commandName + '.deps.json'
    MarkerName = '.' + [string]$identity.id + '-install.json'
    PayloadManifestName = '.' + [string]$identity.id + '-payload.json'
    BuildManifestName = ([string]$identity.id).ToUpperInvariant() + '-BUILD.json'
    ConfigurationOwnerName = '.' + [string]$identity.id + '-config.json'
    SkillManifestProductId = [string]$identity.id + '-skill'
    ArtifactOwnerProductId = [string]$identity.id + '-build-output'
    WindowsInstallDirectory = ([string]$identity.installDirectory).Replace('/', '\')
}
[pscustomobject][ordered]@{
    Identity = $identity
    Names = $names
    Edition = [string]$identity.edition
    DistributionPath = $identityPath
    CatalogPath = Join-Path $repoRoot 'eng/products.json'
    SourceRoot = Join-Path $repoRoot 'src'
    TestRoot = Join-Path $repoRoot 'tests'
    LauncherProject = Join-Path $repoRoot 'src/Aspose.Cli/Aspose.Cli.csproj'
    SolutionPath = Join-Path $repoRoot $identity.solutionName
}
