<#
.SYNOPSIS
Builds the repository once and runs the catalog test projects at one of three scopes.

.DESCRIPTION
Tests marked [Category(Installer)], [Category(Browser)] or [Category(Slow)] are left out of
the fast feedback loop:

- Fast (default) runs every unmarked test.
- Affected adds every test of the projects a change reaches: a change inside a source or test
  project runs the test projects that reference it in full, installer inputs add the installer
  tests, documentation adds nothing, and any other change (build inputs, eng/, scripts/) runs
  everything. Changes are read against the merge base with -Base, including uncommitted and
  untracked files.
- Full runs every test with a required license, including the reproductions of the SDK defects
  in KNOWN-ISSUES.md. Run it before a release and after an SDK update.

Test projects run at the same time, each with its log beside its TRX result. Prerequisites are
checked first: PowerShell 7.4 (tests start pwsh.exe from PATH), and only when the scope needs
them Windows PowerShell 5.1 (the customer installer's runtime), the pinned Chromium that the
built playwright.ps1 provisions, and the license.

Skipped tests are listed after the run, with licensed cases called out, and so is every test
that ran longer than 10 seconds without a category.
#>
[CmdletBinding()]
param(
    [ValidateSet('Fast', 'Affected', 'Full')]
    [string] $Scope = 'Fast',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    # Affected: the branch whose merge base the change is measured from.
    [string] $Base = 'master',

    [switch] $NoBuild,

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

$categories = @('Installer', 'Browser', 'Slow')
$slowTestSeconds = 10
# Inputs of the customer installer tests outside their source file.
$installerInputs = @('install.ps1', 'scripts/install-local.ps1', 'tests/Aspose.Cli.Platform.Tests/Integration/CustomerInstaller')

$testKit = Join-Path $layout.TestRoot 'Aspose.Cli.TestKit/Aspose.Cli.TestKit.csproj'
$testProjects = @(
    Get-ChildItem -LiteralPath $layout.TestRoot -Recurse -Filter '*.csproj' -File |
        Where-Object { $_.FullName -ne (Resolve-Path -LiteralPath $testKit).Path } |
        ForEach-Object FullName
) | Sort-Object
if ($testProjects.Count -eq 0) {
    throw "No test projects were found."
}

function ConvertTo-RepositoryPath {
    param([Parameter(Mandatory)][string] $Path)
    return [IO.Path]::GetRelativePath($repoRoot, $Path).Replace('\', '/')
}

# Directories of a project and every project it references, directly or through others.
function Get-ProjectClosure {
    param([Parameter(Mandatory)][string[]] $Roots)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending = [Collections.Generic.Queue[string]]::new()
    foreach ($root in $Roots) { $pending.Enqueue([IO.Path]::GetFullPath($root)) }
    while ($pending.Count -ne 0) {
        $project = $pending.Dequeue()
        if (-not $seen.Add($project)) { continue }
        [xml] $xml = [IO.File]::ReadAllText($project)
        foreach ($reference in @($xml.SelectNodes('//ProjectReference'))) {
            $pending.Enqueue([IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $project) ([string]$reference.Include))))
        }
    }
    return @($seen | ForEach-Object { (ConvertTo-RepositoryPath (Split-Path -Parent $_)) + '/' })
}

function Test-PathPrefix {
    param([string] $Path, [string[]] $Prefixes)
    foreach ($prefix in $Prefixes) {
        if ($Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Get-ChangedPaths {
    $mergeBase = & git -C $repoRoot merge-base $Base HEAD
    if ($LASTEXITCODE -ne 0) { throw "The merge base with '$Base' could not be found; pass -Base with an existing branch." }
    $tracked = & git -C $repoRoot diff --name-only --no-renames $mergeBase
    if ($LASTEXITCODE -ne 0) { throw "The change since '$Base' could not be listed." }
    $untracked = & git -C $repoRoot ls-files --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Untracked files could not be listed.' }
    return @(@($tracked) + @($untracked) | Where-Object { $_ } | Sort-Object -Unique)
}

# The categories each test project runs besides its unmarked tests.
$included = @{}
foreach ($project in $testProjects) {
    $included[$project] = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
}
switch ($Scope) {
    'Full' {
        foreach ($project in $testProjects) { $included[$project].UnionWith([string[]]$categories) }
    }
    'Affected' {
        $closures = @{}
        foreach ($project in $testProjects) { $closures[$project] = Get-ProjectClosure @($project, $testKit) }
        $projectDirectories = @($closures.Values | ForEach-Object { $_ } | Sort-Object -Unique)
        $changed = Get-ChangedPaths
        foreach ($path in $changed) {
            $installerInput = Test-PathPrefix $path $installerInputs
            if ($installerInput) {
                foreach ($project in $testProjects) { [void]$included[$project].Add('Installer') }
            }
            if (Test-PathPrefix $path $projectDirectories) {
                foreach ($project in $testProjects) {
                    if (Test-PathPrefix $path $closures[$project]) { $included[$project].UnionWith([string[]]@('Browser', 'Slow')) }
                }
            }
            elseif (-not $installerInput -and $path -notlike '*.md' -and -not (Test-PathPrefix $path @('.github/', 'LICENSE'))) {
                foreach ($project in $testProjects) { $included[$project].UnionWith([string[]]$categories) }
            }
        }
        Write-Host "AFFECTED $($changed.Count) changed path(s) since the merge base with $Base."
    }
}

function Get-ProjectFilter {
    param([Parameter(Mandatory)][string] $Project)
    $excluded = @($categories | Where-Object { -not $included[$Project].Contains($_) })
    if ($excluded.Count -eq 0) { return $null }
    return ($excluded | ForEach-Object { "Category!=$_" }) -join '&'
}

$runsInstaller = @($testProjects | Where-Object { $included[$_].Contains('Installer') }).Count -ne 0
$runsBrowser = @($testProjects | Where-Object { $included[$_].Contains('Browser') }).Count -ne 0
$requireLicense = $Scope -eq 'Full'

$missing = @()
if ($PSVersionTable.PSVersion -lt [version]'7.4') {
    $missing += "PowerShell 7.4 or later must run this script (current: $($PSVersionTable.PSVersion)). Install it from https://aka.ms/powershell and run: pwsh ./scripts/test.ps1"
}
$pwshName = if ($env:OS -eq 'Windows_NT') { 'pwsh.exe' } else { 'pwsh' }
if ($null -eq (Get-Command $pwshName -CommandType Application -ErrorAction SilentlyContinue)) {
    $missing += "Tests start $pwshName from PATH, but it was not found. Add the PowerShell 7 installation directory to PATH."
}
if ($runsInstaller -and $env:OS -eq 'Windows_NT') {
    $windowsPowerShell = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf)) {
        $missing += "Installer tests need Windows PowerShell 5.1 at $windowsPowerShell."
    }
}
$licensePath = $env:ASPOSE_CLI_TEST_LICENSE_PATH
if ($requireLicense -and ([string]::IsNullOrWhiteSpace($licensePath) -or -not (Test-Path -LiteralPath $licensePath -PathType Leaf))) {
    $missing += 'The Full scope needs ASPOSE_CLI_TEST_LICENSE_PATH to name an existing commercial license file.'
}
if ($missing.Count -ne 0) {
    throw "Test prerequisites are missing:$([Environment]::NewLine)- $($missing -join "$([Environment]::NewLine)- ")"
}

& $generator `
    -Check `
    -RepositoryRoot $repoRoot

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
    throw "The CLI executable does not exist: $builtExecutable"
}

if ($runsBrowser) {
    $browserInstaller = Join-Path $repoRoot "tests/Aspose.Cli.Platform.Tests/bin/$Configuration/net10.0/playwright.ps1"
    if (-not (Test-Path -LiteralPath $browserInstaller -PathType Leaf)) {
        throw "The built browser test installer is missing: $browserInstaller. Build the solution first (omit -NoBuild)."
    }
    & $browserInstaller install chromium
    if ($LASTEXITCODE -ne 0) {
        throw "The pinned Chromium for App browser tests could not be provisioned (exit code $LASTEXITCODE). It downloads from the Playwright CDN; allow that network access or provision PLAYWRIGHT_BROWSERS_PATH, then retry."
    }
}

# Starts dotnet with its output copied to files, so concurrent projects never interleave.
function Start-Dotnet {
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $LogFile,
        [hashtable] $Environment = @{}
    )
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($name in $Environment.Keys) { $start.Environment[$name] = $Environment[$name] }
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    [IO.Directory]::CreateDirectory((Split-Path -Parent $LogFile)) | Out-Null
    $process = [Diagnostics.Process]::Start($start)
    $output = [IO.File]::Create($LogFile)
    $errors = [IO.File]::Create($LogFile + '.err')
    return [pscustomobject]@{
        Process = $process
        LogFile = $LogFile
        Files = @($output, $errors)
        Copies = @($process.StandardOutput.BaseStream.CopyToAsync($output), $process.StandardError.BaseStream.CopyToAsync($errors))
    }
}

function Complete-Dotnet {
    param([Parameter(Mandatory)] $Run)
    $Run.Process.WaitForExit()
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$Run.Copies)
    foreach ($file in $Run.Files) { $file.Dispose() }
    $errorText = [IO.File]::ReadAllText($Run.LogFile + '.err')
    Remove-Item -LiteralPath ($Run.LogFile + '.err')
    if ($errorText.Length -ne 0) { [IO.File]::AppendAllText($Run.LogFile, $errorText) }
    return $Run.Process.ExitCode
}

function Read-Trx {
    param([Parameter(Mandatory)][string] $ResultsFile)
    [xml] $trx = [IO.File]::ReadAllText($ResultsFile)
    $namespace = [Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    foreach ($result in @($trx.SelectNodes('//t:UnitTestResult', $namespace))) {
        $reason = $result.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $namespace)
        if ($null -eq $reason) { $reason = $result.SelectSingleNode('t:Output/t:StdOut', $namespace) }
        [pscustomobject]@{
            Name = [string]$result.testName
            Outcome = [string]$result.outcome
            Seconds = $(if ($result.duration) { [TimeSpan]::Parse($result.duration).TotalSeconds } else { 0 })
            Reason = $(if ($null -eq $reason) { '' } else { $reason.InnerText.Trim() })
        }
    }
}

$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$resultsRoot = Join-Path $repoRoot "artifacts/TestResults/$runId"
$markedFilter = ($categories | ForEach-Object { "Category=$_" }) -join '|'
# Concurrent projects share the processor: each runs at most its share of 1.5 test threads
# per core, so tests with process-start and I/O budgets are not starved.
$threadsPerProject = [Math]::Max(2, [int][Math]::Ceiling([Environment]::ProcessorCount * 1.5 / $testProjects.Count))
$runs = foreach ($project in $testProjects) {
    $projectName = [IO.Path]::GetFileNameWithoutExtension($project)
    $resultsDirectory = Join-Path $resultsRoot $projectName
    $filter = Get-ProjectFilter $project
    $arguments = @(
        'test', $project,
        '--configuration', $Configuration,
        '--no-build', '--no-restore', '--nologo',
        '--blame-hang-timeout', $HangTimeout,
        '--blame-hang-dump-type', 'mini',
        '--logger', 'trx;LogFileName=results.trx',
        '--results-directory', $resultsDirectory)
    if ($null -ne $filter) { $arguments += @('--filter', $filter) }
    $arguments += @('--', "xUnit.MaxParallelThreads=$threadsPerProject")
    Write-Host "TEST $projectName $(if ($null -eq $filter) { '(all tests)' } else { "($filter)" })"
    $test = Start-Dotnet $arguments (Join-Path $resultsDirectory 'test.log') @{ ASPOSE_CLI_TEST_ARTIFACTS = $resultsDirectory }
    # A project that runs marked tests lists them, so the slow-test report can leave them out.
    $listing = if ($included[$project].Count -eq 0) { $null } else {
        Start-Dotnet @('test', $project, '--configuration', $Configuration, '--no-build', '--no-restore', '--nologo',
            '--list-tests', '--filter', $markedFilter) (Join-Path $resultsDirectory 'marked.log')
    }
    [pscustomobject]@{
        Name = $projectName
        Project = $project
        ResultsFile = Join-Path $resultsDirectory 'results.trx'
        Test = $test
        Listing = $listing
    }
}

$failures = @()
$skipped = @()
$unmarkedSlow = @()
foreach ($run in $runs) {
    $exitCode = Complete-Dotnet $run.Test
    $marked = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if ($null -ne $run.Listing) {
        if ((Complete-Dotnet $run.Listing) -ne 0) { throw "Marked tests of $($run.Name) could not be listed; see $($run.Listing.LogFile)." }
        foreach ($line in [IO.File]::ReadAllLines($run.Listing.LogFile)) {
            if ($line -match '^ {4}(\S.*)$') { [void]$marked.Add($Matches[1].Trim()) }
        }
    }
    $results = @()
    if (Test-Path -LiteralPath $run.ResultsFile -PathType Leaf) { $results = @(Read-Trx $run.ResultsFile) }
    $passed = @($results | Where-Object Outcome -eq 'Passed').Count
    $failed = @($results | Where-Object Outcome -eq 'Failed').Count
    $notExecuted = @($results | Where-Object Outcome -eq 'NotExecuted')
    $elapsed = ($run.Test.Process.ExitTime - $run.Test.Process.StartTime).TotalSeconds
    $state = if ($exitCode -eq 0 -and $results.Count -ne 0) { 'PASS' } else { 'FAIL' }
    Write-Host ('{0} {1}: {2} passed, {3} failed, {4} skipped ({5:n0} s)' -f $state, $run.Name, $passed, $failed, $notExecuted.Count, $elapsed)
    if ($state -eq 'FAIL') {
        Get-Content -LiteralPath $run.Test.LogFile | Write-Host
        if ($results.Count -eq 0) { Write-Warning "Test project did not produce its TRX result: $($run.ResultsFile)" }
        $failures += $run.Project
    }
    $skipped += @($notExecuted | ForEach-Object { $_ | Add-Member -NotePropertyName Project -NotePropertyValue $run.Name -PassThru })
    $unmarkedSlow += @($results | Where-Object { $_.Outcome -eq 'Passed' -and $_.Seconds -gt $slowTestSeconds -and -not $marked.Contains($_.Name) } |
        ForEach-Object { $_ | Add-Member -NotePropertyName Project -NotePropertyValue $run.Name -PassThru })
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
if ($requireLicense -and $licensedSkips.Count -ne 0) {
    $failures += 'licensed cases were skipped although the Full scope requires a license'
}
if ($unmarkedSlow.Count -ne 0) {
    Write-Warning "$($unmarkedSlow.Count) test(s) without a category ran longer than $slowTestSeconds s; speed them up or mark them [Category(TestCategory.Slow)]:"
    foreach ($test in $unmarkedSlow | Sort-Object Seconds -Descending) {
        Write-Host ('  [{0}] {1}: {2:n1} s' -f $test.Project, $test.Name, $test.Seconds)
    }
}

if ($failures.Count -ne 0) {
    throw "$Scope test run failed:$([Environment]::NewLine)$($failures -join [Environment]::NewLine)"
}

Write-Host "PASS $Scope scope: $($testProjects.Count) test projects after one solution build."
Write-Host "Test results: $resultsRoot"
