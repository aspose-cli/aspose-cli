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
$required = @('schemaVersion','id','commandName','displayName','edition','environmentVariablePrefix','skillPrefix','schemaBaseUri','configurationDirectoryName','installDirectory','solutionName')
if (@(Compare-Object @($identity.PSObject.Properties.Name | Sort-Object) @($required | Sort-Object)).Count -ne 0 -or $identity.schemaVersion -ne 1) { throw "Invalid distribution identity: $identityPath" }
if ($identity.id -cnotmatch '^[a-z][a-z0-9-]*$' -or $identity.commandName -cne $identity.id -or $identity.skillPrefix -cne ($identity.id + '-') -or $identity.environmentVariablePrefix -cne ($identity.id.Replace('-','_').ToUpperInvariant() + '_') -or $identity.schemaBaseUri -cne ("https://schemas.aspose.dev/" + $identity.id + "/v2/")) { throw "Inconsistent distribution identity: $identityPath" }
foreach ($relative in @($identity.configurationDirectoryName,$identity.installDirectory,$identity.solutionName)) {
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[/\\])\.\.([/\\]|$)') { throw 'Distribution paths must be project-relative and bounded.' }
}
$buildArguments = @()
[pscustomobject][ordered]@{
    Identity = $identity
    Edition = [string]$identity.edition
    Slug = [string]$identity.edition
    RepositoryRoot = $repoRoot
    CatalogPath = Join-Path $repoRoot 'eng/products.json'
    GeneratedRoot = Join-Path $repoRoot 'eng/generated'
    SourceRoot = Join-Path $repoRoot 'src'
    TestRoot = Join-Path $repoRoot 'tests'
    PlatformSourceRoot = Join-Path $repoRoot 'src'
    PlatformTestRoot = Join-Path $repoRoot 'tests'
    LauncherProject = Join-Path $repoRoot 'src/Aspose.Cli/Aspose.Cli.csproj'
    SolutionPath = Join-Path $repoRoot $identity.solutionName
    BuildArguments = $buildArguments
}
