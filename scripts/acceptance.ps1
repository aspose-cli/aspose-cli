<#
.SYNOPSIS
Runs the SDK acceptance gates under tests/acceptance as release blockers.

.DESCRIPTION
Every directory below tests/acceptance is a gate listed in eng/acceptance-gates.json, which
names the gate and binds the arguments of its reproduce.ps1. A gate exits 0 when the desired
behavior holds, 1 when it does not and 2 when it could not run.

A failing gate blocks the release unless KNOWN-ISSUES.md waives it for the version declared in
Directory.Build.props. A gate that could not run always blocks. A waived gate that passes
is reported so that its waiver can be removed. Waivers are rows of the table that follows the
heading "## Release gate waivers":

    | Gate | Version | Tracking | Reason |
    | --- | --- | --- | --- |
    | SLD-003 | 1.0.0 | <upstream issue or ticket> | <why the release may ship with it> |

Gates run the built CLI and SDK assemblies with licensed SDKs. Build the solution first and
set ASPOSE_LICENSE_PATH (or the product license variables each gate README names).
-Plan only validates the gate list and the waivers and prints which gates are waived.
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

function Read-GateWaivers {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string[]] $GateIds)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
    $lines = @(Get-Content -LiteralPath $Path)
    $start = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Trim() -ceq '## Release gate waivers') { $start = $index; break }
    }
    if ($start -lt 0) { return @() }
    $waivers = @()
    $inTable = $false
    for ($index = $start + 1; $index -lt $lines.Count; $index++) {
        $line = $lines[$index].Trim()
        if (-not $line.StartsWith('|')) {
            if ($inTable -or $line.StartsWith('#')) { break }
            continue
        }
        $inTable = $true
        $cells = @($line.Trim('|').Split('|') | ForEach-Object { $_.Trim() })
        if ($cells[0] -ceq 'Gate' -or $cells[0] -match '^:?-{3,}:?$') { continue }
        if ($cells.Count -ne 4) { throw "A release gate waiver needs four cells (Gate | Version | Tracking | Reason): $line" }
        $waiver = [pscustomobject]@{ Gate = $cells[0]; Version = $cells[1]; Tracking = $cells[2]; Reason = $cells[3] }
        if ($waiver.Gate -cnotin $GateIds) { throw "KNOWN-ISSUES.md waives unknown acceptance gate '$($waiver.Gate)'." }
        if ($waiver.Version -cnotmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw "Waiver for '$($waiver.Gate)' names an invalid version '$($waiver.Version)'." }
        if ([string]::IsNullOrWhiteSpace($waiver.Tracking) -or [string]::IsNullOrWhiteSpace($waiver.Reason)) {
            throw "Waiver for '$($waiver.Gate)' needs both a tracking reference and a reason."
        }
        if (@($waivers | Where-Object { $_.Gate -ceq $waiver.Gate -and $_.Version -ceq $waiver.Version }).Count -ne 0) {
            throw "KNOWN-ISSUES.md waives '$($waiver.Gate)' for $($waiver.Version) twice."
        }
        $waivers += $waiver
    }
    return $waivers
}

$gates = Read-AcceptanceGates
$waivers = @(Read-GateWaivers $KnownIssuesPath @($gates | ForEach-Object { [string]$_.id }))
if ($Plan) {
    [ordered]@{
        schemaVersion = 1
        version = $version
        gates = @($gates | ForEach-Object {
            $id = [string]$_.id
            $waiver = @($waivers | Where-Object { $_.Gate -ceq $id -and $_.Version -ceq $version }) | Select-Object -First 1
            [ordered]@{ id = $id; waived = ($null -ne $waiver); tracking = $(if ($null -ne $waiver) { $waiver.Tracking } else { $null }) }
        })
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
    $waiver = @($waivers | Where-Object { $_.Gate -ceq $gate.id -and $_.Version -ceq $version }) | Select-Object -First 1
    $outcome = switch ($exitCode) {
        0 { 'passed' }
        1 { if ($null -ne $waiver) { 'waived' } else { 'failed' } }
        default { 'error' }
    }
    if ($exitCode -eq 0 -and $null -ne $waiver) {
        Write-Warning "Acceptance gate $($gate.id) passes; remove its waiver for $version from KNOWN-ISSUES.md."
    }
    $results += [pscustomobject][ordered]@{
        id = [string]$gate.id
        exitCode = $exitCode
        outcome = $outcome
        tracking = $(if ($null -ne $waiver) { $waiver.Tracking } else { $null })
        output = $output
    }
    Write-Host "GATE $($gate.id): $outcome (exit $exitCode)"
}
[IO.File]::WriteAllText((Join-Path $runRoot 'summary.json'),
    ([ordered]@{ schemaVersion = 1; version = $version; gates = @($results) } | ConvertTo-Json -Depth 4),
    [Text.UTF8Encoding]::new($false))

$blockers = @($results | Where-Object { $_.outcome -in @('failed', 'error') })
if ($blockers.Count -ne 0) {
    throw "Release-blocking acceptance gates: $(($blockers | ForEach-Object { "$($_.id) ($($_.outcome))" }) -join ', '). A failing gate may ship only with a waiver for $version in KNOWN-ISSUES.md; a gate that could not run always blocks. Evidence: $runRoot"
}
Write-Host "Acceptance gates for $version passed or are waived. Evidence: $runRoot"
