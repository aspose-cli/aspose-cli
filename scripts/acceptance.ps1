<#
.SYNOPSIS
Runs the SDK acceptance gates under tests/acceptance, one per known SDK issue.

.DESCRIPTION
Every directory below tests/acceptance is a gate listed in eng/acceptance-gates.json, which
names the gate and binds the arguments of its reproduce.ps1. Each gate reproduces the known issue
that KNOWN-ISSUES.md describes under a "### <id>" heading of the same id, and exits 1 while the
defect is present, 0 once it is gone and 2 when it could not run.

A gate that still reproduces its issue passes the release, because the CLI handles the issue. A
gate that exits 0 blocks the release until the issue, its handling and its gate are deleted, and
a gate that could not run always blocks.

Gates run the built CLI and SDK assemblies with licensed SDKs. Build the solution first and set
ASPOSE_LICENSE_PATH (or the product license variables each gate README names). -Plan only checks
that the gates and the known issues match one to one and prints them.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $KnownIssuesPath,

    [switch] $Plan
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Acceptance gates need PowerShell 7 (pwsh).' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$layout = & (Join-Path $PSScriptRoot 'resolve-project-layout.ps1') -RepositoryRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($KnownIssuesPath)) { $KnownIssuesPath = Join-Path $repoRoot 'KNOWN-ISSUES.md' }
[xml] $buildDefaults = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = $buildDefaults.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
$framework = $buildDefaults.SelectSingleNode('/Project/PropertyGroup/TargetFramework').InnerText
$buildOutput = Join-Path $layout.SourceRoot "Aspose.Cli/bin/$Configuration/$framework"
$executable = Join-Path $buildOutput $layout.Names.ExecutableName
if (-not $Plan -and -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Build the solution before running acceptance gates; the CLI is missing: $executable"
}

function Read-AcceptanceGates {
    $path = Join-Path $repoRoot 'eng/acceptance-gates.json'
    $catalog = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($catalog.schemaVersion -ne 1 -or $catalog.gates -isnot [array]) { throw "Invalid acceptance gate list: $path" }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $directories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($gate in $catalog.gates) {
        if ([string]$gate.id -cnotmatch '^[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*$' -or -not $ids.Add([string]$gate.id)) {
            throw "Acceptance gate id '$($gate.id)' is invalid or repeated."
        }
        $script = Join-Path $repoRoot ([string]$gate.directory) 'reproduce.ps1'
        if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { throw "Acceptance gate '$($gate.id)' has no reproduce.ps1: $script" }
        [void]$directories.Add([IO.Path]::GetFullPath((Join-Path $repoRoot ([string]$gate.directory))))
    }
    # A new gate cannot be forgotten: every acceptance directory must be listed.
    foreach ($directory in @(Get-ChildItem -LiteralPath (Join-Path $layout.TestRoot 'acceptance') -Directory)) {
        if (-not $directories.Contains($directory.FullName)) {
            throw "Acceptance gate directory is not listed in eng/acceptance-gates.json: $($directory.FullName)"
        }
    }
    return @($catalog.gates)
}

function Read-KnownIssues {
    if (-not (Test-Path -LiteralPath $KnownIssuesPath -PathType Leaf)) { throw "The known issues are missing: $KnownIssuesPath" }
    return @(Get-Content -LiteralPath $KnownIssuesPath |
        Where-Object { $_ -cmatch '^### ([A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*)\s*$' } |
        ForEach-Object { $Matches[1] })
}

$gates = Read-AcceptanceGates
$gateIds = @($gates | ForEach-Object { [string]$_.id })
$issueIds = @(Read-KnownIssues)
$ungated = @($issueIds | Where-Object { $_ -cnotin $gateIds })
$unknown = @($gateIds | Where-Object { $_ -cnotin $issueIds })
if ($ungated.Count -ne 0 -or $unknown.Count -ne 0) {
    throw "Acceptance gates and known issues must match one to one. Known issues without a gate: $(if ($ungated.Count) { $ungated -join ', ' } else { 'none' }). Gates without a known issue: $(if ($unknown.Count) { $unknown -join ', ' } else { 'none' })."
}
if ($Plan) {
    [ordered]@{
        schemaVersion = 1
        version = $version
        gates = $gateIds
    } | ConvertTo-Json -Depth 4
    return
}
$runRoot = Join-Path $repoRoot ('artifacts/acceptance/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$shell = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
$results = @()
foreach ($gate in $gates) {
    $output = Join-Path $runRoot (Split-Path -Leaf ([string]$gate.directory))
    $arguments = @()
    foreach ($binding in $gate.arguments.PSObject.Properties) {
        $value = ([string]$binding.Value).Replace('{executable}', $executable).Replace('{buildOutput}', $buildOutput).Replace('{output}', $output)
        $arguments += @("-$($binding.Name)", [IO.Path]::GetFullPath($value))
    }
    Write-Host "GATE $($gate.id)"
    # Each gate loads SDK assemblies, so it runs in its own process.
    & $shell -NoLogo -NoProfile -NonInteractive -File (Join-Path $repoRoot ([string]$gate.directory) 'reproduce.ps1') @arguments
    $exitCode = $LASTEXITCODE
    $outcome = switch ($exitCode) {
        1 { 'present' }
        0 { 'fixed' }
        default { 'error' }
    }
    if ($outcome -eq 'fixed') {
        Write-Warning "The SDK no longer reproduces $($gate.id); delete the known issue, its handling and its gate."
    }
    $results += [pscustomobject][ordered]@{
        id = [string]$gate.id
        exitCode = $exitCode
        outcome = $outcome
        output = $output
    }
    Write-Host "GATE $($gate.id): $outcome (exit $exitCode)"
}
[IO.File]::WriteAllText((Join-Path $runRoot 'summary.json'),
    ([ordered]@{ schemaVersion = 1; version = $version; gates = @($results) } | ConvertTo-Json -Depth 4),
    [Text.UTF8Encoding]::new($false))

$blockers = @($results | Where-Object { $_.outcome -in @('fixed', 'error') })
if ($blockers.Count -ne 0) {
    throw "Release-blocking acceptance gates: $(($blockers | ForEach-Object { "$($_.id) ($($_.outcome))" }) -join ', '). A fixed issue must be deleted with its handling and gate; a gate that could not run always blocks. Evidence: $runRoot"
}
Write-Host "Every acceptance gate for $version reproduces a known issue the CLI handles. Evidence: $runRoot"
