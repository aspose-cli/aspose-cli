<#
.SYNOPSIS
Returns the given repository paths that are documentation or repository metadata.

.DESCRIPTION
The rules are in documentation-paths.json beside this script, which RepositoryInputTests also
reads. Paths are repository-relative with forward slashes.
#>
[CmdletBinding()]
param([string[]] $Path = @())
$ErrorActionPreference = 'Stop'
$rules = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'documentation-paths.json') -Raw | ConvertFrom-Json
foreach ($candidate in $Path) {
    $name = $candidate.Split('/')[-1]
    $match = @($rules.extensions | Where-Object { $candidate.EndsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0 -or
        @($rules.directories | Where-Object { $candidate.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0 -or
        @($rules.namePrefixes | Where-Object { $name.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0 -or
        @($rules.names | Where-Object { $name.Equals($_, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0
    if ($match) { $candidate }
}
