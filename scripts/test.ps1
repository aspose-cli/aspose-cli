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
    Get-ChildItem -LiteralPath $layout.TestRoot -Recurse -Filter '*.csproj' -File |
        Where-Object { $_.BaseName -ne 'Aspose.Cli.TestKit' -and $_.FullName -notmatch '[/\\]acceptance[/\\]' } |
        ForEach-Object FullName
) | Sort-Object
if ($testProjects.Count -eq 0) {
    throw "No test projects were found for $($layout.Edition)."
}

if (-not $NoBuild) {
    & dotnet restore $solution `
        --locked-mode `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Locked restore failed with exit code $LASTEXITCODE. Run scripts/sync.ps1 after dependency changes."
    }
    & dotnet build $solution `
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

$browserInstaller = Join-Path $repoRoot "tests/Aspose.Cli.Platform.Tests/bin/$Configuration/net10.0/playwright.ps1"
if (-not (Test-Path -LiteralPath $browserInstaller -PathType Leaf)) {
    throw "The built browser test installer is missing: $browserInstaller"
}
& $browserInstaller install chromium
if ($LASTEXITCODE -ne 0) {
    throw "Chromium test setup failed with exit code $LASTEXITCODE."
}

$failures = @()
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$resultsRoot = Join-Path $repoRoot "artifacts/TestResults/$runId"
foreach ($project in $testProjects) {
    $projectName = [IO.Path]::GetFileNameWithoutExtension($project)
    $resultsDirectory = Join-Path $resultsRoot $projectName
    $resultsFile = Join-Path $resultsDirectory 'results.trx'
    $env:ASPOSE_CLI_TEST_ARTIFACTS = $resultsDirectory
    Write-Host "TEST $project"
    & dotnet test $project `
        --configuration $Configuration `
        --no-build `
        --no-restore `
        --nologo `
        --logger 'trx;LogFileName=results.trx' `
        --results-directory $resultsDirectory
    $testExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $resultsFile -PathType Leaf)) {
        Write-Warning "Test project did not produce its TRX result: $resultsFile"
    }
    if ($testExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultsFile -PathType Leaf)) {
        $failures += $project
    }
}

if ($failures.Count -ne 0) {
    throw "Test projects failed:$([Environment]::NewLine)$($failures -join [Environment]::NewLine)"
}

Write-Host "PASS $($testProjects.Count) $($layout.Edition) test projects after one solution build."
Write-Host "Test results: $resultsRoot"
