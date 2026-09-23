<#
.SYNOPSIS
Builds the repository once and runs every catalog test project.

.DESCRIPTION
Prerequisites are checked first: PowerShell 7 (tests start pwsh.exe from PATH), Windows
PowerShell 5.1 on Windows (the customer installer's runtime) and the pinned Chromium that
the built playwright.ps1 provisions for App browser tests.

Skipped tests are listed after the run, with licensed cases called out. -RequireLicense
fails the run unless ASPOSE_CLI_TEST_LICENSE_PATH names an existing license, so a licensed
run cannot silently fall back to skipping its licensed cases.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $NoBuild,

    [switch] $RequireLicense,

    # A test that stays silent this long is reported with its name and a mini dump.
    [string] $HangTimeout = '15m'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$generator = Join-Path $PSScriptRoot 'generate-product-catalog.ps1'
$layoutResolver = Join-Path $PSScriptRoot 'resolve-project-layout.ps1'
$layout = & $layoutResolver  -RepositoryRoot $repoRoot
$solution = $layout.SolutionPath
$executableName = if ($env:OS -eq 'Windows_NT') {
    [string]$layout.Names.ExecutableName
}
else {
    [string]$layout.Identity.commandName
}
$builtExecutable = Join-Path $layout.SourceRoot (
    "Aspose.Cli/bin/$Configuration/net10.0/$executableName")
$env:ASPOSE_CLI_TEST_EXECUTABLE = $builtExecutable

$missing = @()
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $missing += "PowerShell 7 must run this script (current: $($PSVersionTable.PSVersion)). Install it from https://aka.ms/powershell and run: pwsh ./scripts/test.ps1"
}
$pwshName = if ($env:OS -eq 'Windows_NT') { 'pwsh.exe' } else { 'pwsh' }
if ($null -eq (Get-Command $pwshName -CommandType Application -ErrorAction SilentlyContinue)) {
    $missing += "Tests start $pwshName from PATH, but it was not found. Add the PowerShell 7 installation directory to PATH."
}
if ($env:OS -eq 'Windows_NT') {
    $windowsPowerShell = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf)) {
        $missing += "Installer tests need Windows PowerShell 5.1 at $windowsPowerShell."
    }
}
$licensePath = $env:ASPOSE_CLI_TEST_LICENSE_PATH
if ($RequireLicense -and ([string]::IsNullOrWhiteSpace($licensePath) -or -not (Test-Path -LiteralPath $licensePath -PathType Leaf))) {
    $missing += '-RequireLicense needs ASPOSE_CLI_TEST_LICENSE_PATH to name an existing commercial license file.'
}
if ($missing.Count -ne 0) {
    throw "Test prerequisites are missing:$([Environment]::NewLine)- $($missing -join "$([Environment]::NewLine)- ")"
}

& $generator `
    -Check `
    -RepositoryRoot $repoRoot

$testProjects = @(
    Get-ChildItem -LiteralPath $layout.TestRoot -Recurse -Filter '*.csproj' -File |
        Where-Object { $_.BaseName -ne 'Aspose.Cli.TestKit' } |
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
    throw "The built browser test installer is missing: $browserInstaller. Build the solution first (omit -NoBuild)."
}
& $browserInstaller install chromium
if ($LASTEXITCODE -ne 0) {
    throw "The pinned Chromium for App browser tests could not be provisioned (exit code $LASTEXITCODE). It downloads from the Playwright CDN; allow that network access or provision PLAYWRIGHT_BROWSERS_PATH, then retry."
}

function Get-SkippedTests {
    param([Parameter(Mandatory)][string] $ResultsFile)
    [xml] $trx = [IO.File]::ReadAllText($ResultsFile)
    $namespace = [Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    foreach ($result in @($trx.SelectNodes('//t:UnitTestResult[@outcome="NotExecuted"]', $namespace))) {
        $reason = $result.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $namespace)
        if ($null -eq $reason) { $reason = $result.SelectSingleNode('t:Output/t:StdOut', $namespace) }
        [pscustomobject]@{
            Name = [string]$result.testName
            Reason = $(if ($null -eq $reason) { '' } else { $reason.InnerText.Trim() })
        }
    }
}

$failures = @()
$skipped = @()
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
        --blame-hang-timeout $HangTimeout `
        --blame-hang-dump-type mini `
        --logger 'trx;LogFileName=results.trx' `
        --results-directory $resultsDirectory
    $testExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $resultsFile -PathType Leaf)) {
        Write-Warning "Test project did not produce its TRX result: $resultsFile"
    }
    else {
        $skipped += @(Get-SkippedTests $resultsFile | ForEach-Object {
            $_ | Add-Member -NotePropertyName Project -NotePropertyValue $projectName -PassThru
        })
    }
    if ($testExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultsFile -PathType Leaf)) {
        $failures += $project
    }
}

$licensedSkips = @($skipped | Where-Object { $_.Reason -match 'ASPOSE_CLI_TEST_LICENSE_PATH' })
if ($skipped.Count -ne 0) {
    Write-Host "SKIPPED $($skipped.Count) test(s), $($licensedSkips.Count) of them licensed (LicensedFact):"
    foreach ($test in $skipped | Sort-Object Project, Name) {
        Write-Host "  [$($test.Project)] $($test.Name): $($test.Reason)"
    }
    if ($licensedSkips.Count -ne 0) {
        Write-Host 'Licensed cases were not exercised: set ASPOSE_CLI_TEST_LICENSE_PATH (see CONTRIBUTING.md) to run them.'
    }
}
if ($RequireLicense -and $licensedSkips.Count -ne 0) {
    $failures += 'licensed cases were skipped although a license was required'
}

if ($failures.Count -ne 0) {
    throw "Test projects failed:$([Environment]::NewLine)$($failures -join [Environment]::NewLine)"
}

Write-Host "PASS $($testProjects.Count) $($layout.Edition) test projects after one solution build."
Write-Host "Test results: $resultsRoot"
