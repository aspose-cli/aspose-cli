<#
.SYNOPSIS
Fails when a change loosens a gate without the owner's quality-exception approval.

.DESCRIPTION
Compares -Head with its merge base with -Base and reports, as GitHub annotations, each way the
change makes the gates accept more than they did:

- a new entry of tests/Aspose.Cli.Invariants.Tests/known-violations.json, keyed by its case and
  invariant, or wider modes of an entry (omitted modes mean both);
- a case id of tests/Aspose.Cli.Invariants.Tests/generated-cases.txt that the head no longer lists;
- a line the change adds to a rule exemption. Rule exemptions are the C# lines between a
  '// rule-exemptions: begin' and a '// rule-exemptions: end' line. An added line whose text
  the change removes elsewhere is moved or reindented, not added; comments and lines without a
  letter or digit do not count, and the lines a new block encloses unchanged are not added.
  Editing an exemption line counts as adding it, and so does moving one out of its block;
- any marker line the change adds, removes, moves or rewords in a file whose base already has
  markers; a file without markers may gain blocks;
- a C# member the change declares outside those blocks that looks like a rule exemption: its
  name holds Exempt or Homonym, or its type holds Exemption or a Reason element.

Removing an entry, narrowing its modes, listing a new case or deleting an exemption needs no
approval. Without -Approved any of the above fails; with it, each is shown as a notice. A rewording
of a known violation's actual text is always a notice. Whatever the approval, the script fails
when a rule-exemptions block is not closed or nests, or when a file holding one has no owner in
.github/CODEOWNERS.

Only committed history is read.

    scripts/require-approval.ps1 -Base origin/master
    scripts/require-approval.ps1 -Base origin/master -Head HEAD -Approved
#>
[CmdletBinding()]
param(
    # The ref whose merge base with -Head the change is measured from.
    [Parameter(Mandatory)][string] $Base,

    # The last commit of the change.
    [string] $Head = 'HEAD',

    # Whether the pull request carries the owner's 'quality-exception' label.
    [switch] $Approved,

    # The git repository to read.
    [string] $Repository = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$knownViolations = 'tests/Aspose.Cli.Invariants.Tests/known-violations.json'
$generatedCases = 'tests/Aspose.Cli.Invariants.Tests/generated-cases.txt'
$beginMarker = '^\s*// rule-exemptions: begin\s*$'
$endMarker = '^\s*// rule-exemptions: end\s*$'
$label = 'quality-exception'

function Invoke-Git {
    $output = & git -C $Repository @args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
    $output
}

# The lines of a file at a commit, or $null where the commit has no such file.
function Get-Lines([string] $Commit, [string] $Path) {
    $text = & git -C $Repository show "${Commit}:$Path" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    @($text | ForEach-Object { $_.TrimEnd("`r") })
}

$baseCommit = Invoke-Git merge-base $Base $Head
$headCommit = Invoke-Git rev-parse $Head
$approvals = [System.Collections.Generic.List[string]]::new()
$broken = [System.Collections.Generic.List[string]]::new()

function Add-Approval([string] $Message) { $approvals.Add($Message) }

# ----- known violations: a new key or wider modes ------------------------------------------------

function Get-Violations([string] $Commit) {
    $lines = Get-Lines $Commit $knownViolations
    $entries = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    if ($null -ne $lines) {
        foreach ($entry in @((($lines -join "`n") | ConvertFrom-Json).violations)) {
            $entries["$($entry.case) / $($entry.invariant)"] = $entry
        }
    }
    $entries
}

$was = Get-Violations $baseCommit
$now = Get-Violations $headCommit
foreach ($key in $now.Keys) {
    $entry = $now[$key]
    $before = $was[$key]
    if ($null -eq $before) {
        Add-Approval "$knownViolations added $key"
        continue
    }
    if ($before.actual -cne $entry.actual) {
        Write-Host "::notice::$knownViolations reworded ${key}: $($entry.actual)"
    }
    if ($null -ne $before.modes -and ($null -eq $entry.modes -or @($entry.modes | Where-Object { $_ -cnotin $before.modes }).Count -gt 0)) {
        Add-Approval "$knownViolations widened the modes of $key"
    }
}

# ----- generated cases: a case the base listed and the head does not -----------------------------

$baseCases = [System.Collections.Generic.HashSet[string]]::new([string[]]@(Get-Lines $baseCommit $generatedCases | Where-Object { $_ }), [StringComparer]::Ordinal)
$headCases = [System.Collections.Generic.HashSet[string]]::new([string[]]@(Get-Lines $headCommit $generatedCases | Where-Object { $_ }), [StringComparer]::Ordinal)
foreach ($case in @($baseCases | Where-Object { -not $headCases.Contains($_) } | Sort-Object -CaseSensitive)) {
    Add-Approval "$generatedCases dropped $case"
}

# ----- rule exemptions ---------------------------------------------------------------------------

# The C# files of a commit that hold a marker, and for each the line numbers inside its blocks;
# a malformed block is added to $Problems unless it is $null.
function Get-ExemptionBlocks([string] $Commit, $Problems) {
    $blocks = [ordered]@{}
    $candidates = @(& git -C $Repository grep -l -F 'rule-exemptions:' $Commit -- '*.cs' 2>$null)
    foreach ($candidate in $candidates) {
        $path = $candidate.Substring($Commit.Length + 1)
        $lines = @(Get-Lines $Commit $path)
        $inside = [System.Collections.Generic.List[int]]::new()
        $markers = 0
        $open = 0
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($lines[$index] -cmatch $beginMarker) {
                $markers++
                if ($open -ne 0) {
                    if ($null -ne $Problems) { $Problems.Add("${path}:$($index + 1) opens a rule-exemptions block inside the one opened on line $open.") }
                }
                $open = $index + 1
            }
            elseif ($lines[$index] -cmatch $endMarker) {
                $markers++
                if ($open -eq 0) {
                    if ($null -ne $Problems) { $Problems.Add("${path}:$($index + 1) closes a rule-exemptions block that is not open.") }
                }
                $open = 0
            }
            elseif ($open -ne 0) {
                $inside.Add($index + 1)
            }
        }
        if ($open -ne 0) {
            if ($null -ne $Problems) { $Problems.Add("${path}:$open opens a rule-exemptions block that is never closed.") }
        }
        if ($markers -gt 0) {
            $blocks[$path] = [pscustomobject]@{ Lines = $lines; Inside = $inside }
        }
    }
    $blocks
}

# The text of an exemption line that counts, or $null for a comment or punctuation alone.
function Get-ExemptionText([string] $Line) {
    $text = $Line.Trim()
    if ($text.StartsWith('//') -or $text -notmatch '[\p{L}\p{Nd}]') {
        return $null
    }
    $text
}

$headBlocks = Get-ExemptionBlocks $headCommit $broken
$baseBlocks = Get-ExemptionBlocks $baseCommit $null

function Test-Inside($Blocks, [string] $Path, [int] $Number) {
    $null -ne $Path -and $Blocks.Contains($Path) -and $Blocks[$Path].Inside.Contains($Number)
}

# The lines the change adds and removes in C# files, with the path and line of each, and the base
# path of each renamed file.
$added = [System.Collections.Generic.List[object]]::new()
$removedLines = [System.Collections.Generic.List[object]]::new()
$renamedFrom = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$from = $null
$oldPath = $null
$path = $null
$oldNumber = 0
$number = 0
foreach ($line in @(Invoke-Git diff -M --unified=0 --no-color --no-ext-diff $baseCommit $headCommit -- '*.cs')) {
    if ($line -cmatch '^diff --git ') {
        $from = $null
        $oldPath = $null
        $path = $null
    }
    elseif ($null -eq $oldPath -and $line -cmatch '^rename from (?<path>.+)$') {
        $from = $Matches['path']
    }
    elseif ($null -eq $oldPath -and $line -cmatch '^rename to (?<path>.+)$') {
        $renamedFrom[$Matches['path']] = $from
    }
    elseif ($null -eq $path -and $line -cmatch '^--- (?:a/(?<path>.+)|/dev/null)$') {
        $oldPath = $Matches['path'] ?? ''
    }
    elseif ($null -eq $path -and $line -cmatch '^\+\+\+ (?:b/(?<path>.+)|/dev/null)$') {
        $path = $Matches['path'] ?? ''
    }
    elseif ($line -cmatch '^@@ -(?<old>\d+)(?:,\d+)? \+(?<new>\d+)') {
        $oldNumber = [int]$Matches['old']
        $number = [int]$Matches['new']
    }
    elseif ($line.StartsWith('-')) {
        $removedLines.Add([pscustomobject]@{ Path = $oldPath; Number = $oldNumber; Text = $line.Substring(1).TrimEnd("`r") })
        $oldNumber++
    }
    elseif ($line.StartsWith('+')) {
        $added.Add([pscustomobject]@{ Path = $path; Number = $number; Text = $line.Substring(1).TrimEnd("`r") })
        $number++
    }
}

# In a file whose base already holds markers, every marker line the change adds, removes, moves or
# rewords redraws what the blocks hold. A file that holds none yet may gain blocks; what they
# enclose is judged below like any other block line.
foreach ($line in $added) {
    if ($line.Text -cmatch $beginMarker -or $line.Text -cmatch $endMarker) {
        $basePath = $renamedFrom.ContainsKey($line.Path) ? $renamedFrom[$line.Path] : $line.Path
        if ($baseBlocks.Contains($basePath)) {
            Add-Approval "$($line.Path):$($line.Number) adds a rule-exemptions marker to a file that has them: $($line.Text.Trim())"
        }
    }
}
$removed = [System.Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
$removedFromBlocks = [System.Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
foreach ($line in $removedLines) {
    if ($line.Text -cmatch $beginMarker -or $line.Text -cmatch $endMarker) {
        Add-Approval "$($line.Path):$($line.Number) removes a rule-exemptions marker: $($line.Text.Trim())"
        continue
    }
    $text = Get-ExemptionText $line.Text
    if ($null -eq $text) {
        continue
    }
    $removed[$text] = $removed.ContainsKey($text) ? $removed[$text] + 1 : 1
    if (Test-Inside $baseBlocks $line.Path $line.Number) {
        $removedFromBlocks[$text] = $removedFromBlocks.ContainsKey($text) ? $removedFromBlocks[$text] + 1 : 1
    }
}

# An added line inside a block counts unless the change removed the same text elsewhere (a move or
# a reindent); lines the change did not touch, such as those a new block encloses, are not added.
# Outside the blocks, a line removed from a block (moved out of it) counts, and so does a member
# declaration that looks like a rule exemption.
$declaration = '^\s*(?:(?:public|private|protected|internal|static|readonly|override|virtual|new)\s+)+(?<type>[^=;{}]+?)\s+(?<name>[A-Za-z_]\w*)\s*(?:\{\s*get;\s*\}\s*)?=(?![=>])'
foreach ($line in $added) {
    $text = Get-ExemptionText $line.Text
    if (Test-Inside $headBlocks $line.Path $line.Number) {
        if ($null -eq $text) {
            continue
        }
        if ($removed.ContainsKey($text) -and $removed[$text] -gt 0) {
            $removed[$text]--
        }
        else {
            Add-Approval "$($line.Path):$($line.Number) adds a rule exemption: $text"
        }
        continue
    }
    if ($null -ne $text -and $removedFromBlocks.ContainsKey($text) -and $removedFromBlocks[$text] -gt 0) {
        $removedFromBlocks[$text]--
        Add-Approval "$($line.Path):$($line.Number) moves a rule exemption out of its block: $text"
    }
    elseif ($line.Text -cmatch $declaration) {
        $name = $Matches['name']
        if ($name -cmatch 'Exempt|Homonym' -or $Matches['type'] -cmatch 'Exemption|\bReason\b') {
            Add-Approval "$($line.Path):$($line.Number) declares $name, which looks like a rule exemption outside a rule-exemptions block"
        }
    }
}
# Every file that holds rule exemptions has an owner.
$owners = @(Get-Lines $headCommit '.github/CODEOWNERS' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') } |
        ForEach-Object { $fields = -split $_; if ($fields.Count -gt 1) { $fields[0] } })
foreach ($path in $headBlocks.Keys) {
    $owned = $false
    foreach ($pattern in $owners) {
        $anchored = $pattern.StartsWith('/')
        $glob = [regex]::Escape($pattern.TrimStart('/').TrimEnd('/')).Replace('\*\*', '.*').Replace('\*', '[^/]*')
        $regex = ($anchored ? '^' : '(^|/)') + $glob + '(/|$)'
        if ($path -cmatch $regex) {
            $owned = $true
            break
        }
    }
    if (-not $owned) {
        $broken.Add("$path holds rule exemptions but has no owner in .github/CODEOWNERS.")
    }
}

# ----- report ------------------------------------------------------------------------------------

foreach ($problem in $broken) {
    Write-Host "::error::$problem"
}
$level = $Approved ? 'notice' : 'error'
foreach ($approval in $approvals) {
    Write-Host "::${level}::$approval"
}
if ($approvals.Count -eq 0) {
    Write-Host 'No known violation or rule exemption was added or widened, and no generated invariant case was dropped.'
}
elseif (-not $Approved) {
    Write-Host "::error::Fix the product instead. Adding or widening a known violation or a rule exemption, or dropping a generated case, takes the owner's approval: the '$label' label on this pull request (AGENTS.md#gates)."
}
if ($broken.Count -gt 0 -or ($approvals.Count -gt 0 -and -not $Approved)) {
    exit 1
}
