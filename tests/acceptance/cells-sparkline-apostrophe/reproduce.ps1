[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkDir,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $LicensePath = $(if ($env:ASPOSE_CELLS_LICENSE_PATH) { $env:ASPOSE_CELLS_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH })
)

# Exit 0: the one-call sparkline Add accepts every sheet name. Exit 1: it rejects an apostrophe. Exit 2: the check could not run.
$ErrorActionPreference = 'Stop'
try {
    $sdk = (Resolve-Path -LiteralPath $SdkDir).Path
    $assembly = (Resolve-Path -LiteralPath (Join-Path $sdk 'Aspose.Cells.dll')).Path
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; this reproduction does not overwrite files.' }
    [void][IO.Directory]::CreateDirectory($output)
    $cells = [Reflection.Assembly]::LoadFrom($assembly)
    if (-not [string]::IsNullOrWhiteSpace($LicensePath)) {
        $license = [Aspose.Cells.License]::new()
        $license.SetLicense((Resolve-Path -LiteralPath $LicensePath).Path)
    }

    # Document-operation core: the documented one-call Add, with the apostrophe correctly doubled,
    # for data and sparklines on the same sheet and on different sheets.
    $cases = @()
    foreach ($case in @(@{ Data = "O'Brien"; Host = "O'Brien" }, @{ Data = "O'Brien"; Host = 'Dash' }, @{ Data = 'Data'; Host = "O'Brien" })) {
        $workbook = [Aspose.Cells.Workbook]::new()
        $data = $workbook.Worksheets[0]
        $data.Name = $case.Data
        for ($column = 0; $column -lt 3; $column++) { $data.Cells[0, $column].PutValue($column + 1) }
        $sheet = if ($case.Host -ceq $case.Data) { $data } else { $workbook.Worksheets[$workbook.Worksheets.Add()] }
        $sheet.Name = $case.Host
        $range = "'" + $case.Data.Replace("'", "''") + "'!A1:C1"
        $failure = $null
        $stored = $null
        try {
            $group = $sheet.SparklineGroups[$sheet.SparklineGroups.Add([Aspose.Cells.Charts.SparklineType]::Line, $range, $false, [Aspose.Cells.CellArea]::CreateCellArea(4, 4, 4, 4))]
            $stored = $group.Sparklines[0].DataRange
        } catch {
            $failure = $_.Exception.InnerException ?? $_.Exception
        }
        $cases += [ordered]@{
            DataSheet = $case.Data
            HostSheet = $case.Host
            DataRange = $range
            Stored = $stored
            Failure = $(if ($null -ne $failure) { "$($failure.GetType().FullName): $($failure.Message)" } else { $null })
            Rejected = $null -ne $failure -and $failure.Message -like 'Invalid "''"*'
        }
    }

    $result = [ordered]@{
        SdkVersion = $cells.GetName().Version.ToString()
        SdkSha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        Licensed = -not [string]::IsNullOrWhiteSpace($LicensePath)
        Cases = $cases
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}

$rejected = @($cases | Where-Object { $_.Rejected })
if ($rejected.Count -gt 0) {
    [Console]::Error.WriteLine("The one-call SparklineGroups.Add rejected $($rejected.Count) of $($cases.Count) apostrophe case(s): $(($rejected | ForEach-Object { "$($_.HostSheet) <- $($_.DataRange)" }) -join '; ')")
    exit 1
}
$other = @($cases | Where-Object { $null -ne $_.Failure -or $null -eq $_.Stored })
if ($other.Count -gt 0) {
    [Console]::Error.WriteLine("The one-call SparklineGroups.Add failed for another reason: $(($other | ForEach-Object { $_.Failure }) -join '; ')")
    exit 2
}
exit 0
