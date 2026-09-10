<#
.SYNOPSIS
Synchronizes every product projection and repository lock file.
#>
[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    Split-Path -Parent $PSScriptRoot
}
else {
    [IO.Path]::GetFullPath($RepositoryRoot)
}
$generator = Join-Path $repoRoot 'scripts/generate-product-catalog.ps1'
$layoutResolver = Join-Path $repoRoot 'scripts/resolve-project-layout.ps1'
if (-not (Test-Path -LiteralPath $layoutResolver -PathType Leaf)) {
    throw "Edition layout resolver does not exist: $layoutResolver"
}
$layout = & $layoutResolver  -RepositoryRoot $repoRoot
$solution = $layout.SolutionPath
$buildArguments = @($layout.BuildArguments)

if (-not (Test-Path -LiteralPath $generator -PathType Leaf)) {
    throw "Internal product reconciler does not exist: $generator"
}

function Invoke-DotNetRestore {
    param([string[]] $Arguments)

    & dotnet restore $solution @buildArguments @Arguments --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }
}

& $generator `
    -RepositoryRoot $repoRoot `
    -OutputRoot $repoRoot
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    throw "Generated solution does not exist: $solution"
}
Invoke-DotNetRestore -Arguments @('--force-evaluate')
& $generator `
    -Check `
    -RepositoryRoot $repoRoot `
    -OutputRoot $repoRoot
Invoke-DotNetRestore -Arguments @('--locked-mode')

Write-Host "$($layout.Edition) product catalog projections and lock files are synchronized."
