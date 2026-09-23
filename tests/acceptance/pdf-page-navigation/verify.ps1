[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Document,
    [Parameter(Mandatory)][string] $SdkDir,
    [string] $LicensePath = $(if ($env:ASPOSE_PDF_LICENSE_PATH) { $env:ASPOSE_PDF_LICENSE_PATH } else { $env:ASPOSE_LICENSE_PATH }),
    [string] $ResultPath
)

$ErrorActionPreference = 'Stop'
try {
    if ([string]::IsNullOrWhiteSpace($LicensePath)) { throw 'Supply a PDF license through -LicensePath or ASPOSE_PDF_LICENSE_PATH.' }
    $assembly = (Resolve-Path -LiteralPath (Join-Path $SdkDir 'Aspose.PDF.dll')).Path
    [void][Reflection.Assembly]::LoadFrom($assembly)
    $license = [Aspose.Pdf.License]::new()
    $license.SetLicense((Resolve-Path -LiteralPath $LicensePath).Path)
    $pdf = [Aspose.Pdf.Document]::new((Resolve-Path -LiteralPath $Document).Path)
    try {
        $checks = @([pscustomobject]@{Name='Page count';Passed=$pdf.Pages.Count -eq 2;Expected=2;Actual=$pdf.Pages.Count})
        foreach ($spec in @(@{Page=1;Text='Appendix'}, @{Page=2;Text='Approval'})) {
            $absorber = [Aspose.Pdf.Text.TextAbsorber]::new()
            $pdf.Pages[$spec.Page].Accept($absorber)
            $checks += [pscustomobject]@{Name="Page $($spec.Page) content";Passed=$absorber.Text.Trim() -ceq $spec.Text;Expected=$spec.Text;Actual=$absorber.Text.Trim()}
        }
        $outlines = @($pdf.Outlines)
        $checks += [pscustomobject]@{Name='Bookmark count';Passed=$outlines.Count -eq 4;Expected=4;Actual=$outlines.Count}
        foreach ($spec in @(@{Title='Approval';Page=2;Type='FitExplicitDestination'}, @{Title='Appendix';Page=1;Type='FitExplicitDestination'}, @{Title='Appendix-null';Page=1;Type='XYZExplicitDestination'}, @{Title='Appendix-zero';Page=1;Type='XYZExplicitDestination'})) {
            $matched = @($outlines | Where-Object Title -CEQ $spec.Title)
            $destination = if ($matched.Count -eq 1) { $matched[0].Destination } else { $null }
            $page = if ($null -ne $destination) { $destination.PageNumber } else { $null }
            $type = if ($null -ne $destination) { $destination.GetType().Name } else { $null }
            $checks += [pscustomobject]@{Name="$($spec.Title) destination";Passed=$page -eq $spec.Page -and $type -ceq $spec.Type;Expected=@{Page=$spec.Page;Type=$spec.Type};Actual=@{Page=$page;Type=$type}}
        }
        $passed = @($checks | Where-Object { -not $_.Passed }).Count -eq 0
        $result = [pscustomobject]@{Document=[IO.Path]::GetFullPath($Document);NavigationPassed=$passed;NullSemanticsVerified=$false;Checks=$checks}
        $json = $result | ConvertTo-Json -Depth 7
        if ($ResultPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ResultPath), $json, [Text.UTF8Encoding]::new($false)) }
        $json
    } finally { $pdf.Dispose() }
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 2
}
if (-not $passed) { exit 1 }
exit 0
