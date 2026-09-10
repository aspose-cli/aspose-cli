<#
.SYNOPSIS
Builds the repository once and runs every catalog test project.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$generator = Join-Path $PSScriptRoot 'generate-product-catalog.ps1'
$layoutResolver = Join-Path $PSScriptRoot 'resolve-project-layout.ps1'
$layout = & $layoutResolver  -RepositoryRoot $repoRoot
$solution = $layout.SolutionPath
$buildArguments = @($layout.BuildArguments)
$executableName = if ($env:OS -eq 'Windows_NT') {
    'aspose-cli.exe'
}
else {
    'aspose-cli'
}
$builtExecutable = Join-Path $layout.SourceRoot (
    "Aspose.Cli/bin/$Configuration/net10.0/$executableName")
$env:ASPOSE_CLI_TEST_EXECUTABLE = $builtExecutable

& $generator `
    -Check `
    -RepositoryRoot $repoRoot `
    -OutputRoot $repoRoot

$testProjects = @(
    foreach ($testRoot in @($layout.TestRoot)) {
        if (-not (Test-Path -LiteralPath $testRoot -PathType Container)) {
            throw "Edition test root does not exist: $testRoot"
        }
        Get-ChildItem -LiteralPath $testRoot -Recurse -Filter '*.csproj' -File |
            Where-Object { $_.BaseName -ne 'Aspose.Cli.TestKit' -and $_.FullName -notmatch '[/\\]acceptance[/\\]' } |
            ForEach-Object FullName
    }
) | Sort-Object
if ($testProjects.Count -eq 0) {
    throw "No test projects were found for $($layout.Edition)."
}

if (-not $NoBuild) {
    & dotnet restore $solution `
        @buildArguments `
        --locked-mode `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Locked restore failed with exit code $LASTEXITCODE. Run scripts/sync.ps1 after dependency changes."
    }
    & dotnet build $solution `
        @buildArguments `
        --configuration $Configuration `
        --no-restore `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
    throw "$($layout.Edition) CLI executable does not exist: $builtExecutable"
}

$failures = @()
foreach ($project in $testProjects) {
    Write-Host "TEST $project"
    & dotnet test $project `
        --configuration $Configuration `
        --no-build `
        --no-restore `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        $failures += $project
    }
}

if ($failures.Count -ne 0) {
    throw "Test projects failed:$([Environment]::NewLine)$($failures -join [Environment]::NewLine)"
}

Write-Host "PASS $($testProjects.Count) $($layout.Edition) test projects after one solution build."
