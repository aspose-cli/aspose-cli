<#
.SYNOPSIS
Fails unless every job that the CI verify job needs has passed.

.DESCRIPTION
-Needs is the needs context of the job as JSON (toJSON(needs)). Every job must have succeeded;
a job named by -MaySkip may also have been skipped by its own condition. A job skipped because
a job it needs failed leaves that job failed, so the run fails either way. verify runs whatever
the result of its jobs, since a skipped required check counts as passed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Needs,

    # The jobs whose own condition may skip them, such as package when no packaging input changed.
    [string[]] $MaySkip = @()
)

$ErrorActionPreference = 'Stop'
$jobs = $Needs | ConvertFrom-Json -AsHashtable
if ($null -eq $jobs -or $jobs.Count -eq 0) {
    throw 'No job results were given.'
}
$failed = @($jobs.Keys | Sort-Object | ForEach-Object {
        $result = [string]$jobs[$_].result
        if ($result -ne 'success' -and -not ($_ -in $MaySkip -and $result -eq 'skipped')) { "$_ ($result)" }
    })
if ($failed.Count -ne 0) {
    throw "These jobs did not pass: $($failed -join ', ')."
}
Write-Host "PASS $(@($jobs.Keys | Sort-Object | ForEach-Object { "$_ ($($jobs[$_].result))" }) -join ', ')."
