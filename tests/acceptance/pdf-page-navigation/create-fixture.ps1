[CmdletBinding()]
param([Parameter(Mandatory)][string] $Output)

$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $path) { throw 'Choose a fresh fixture path; existing files are never overwritten.' }
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
# Independent synthetic input authoring only. This is not a PDF repair or production writer.
$approval = 'BT /F1 18 Tf 72 720 Td (Approval) Tj ET'
$appendix = 'BT /F1 18 Tf 72 720 Td (Appendix) Tj ET'
$objects = @(
    '<< /Type /Catalog /Pages 2 0 R /Outlines 8 0 R >>'
    '<< /Type /Pages /Count 2 /Kids [3 0 R 5 0 R] >>'
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 7 0 R >> >> /Contents 4 0 R >>'
    "<< /Length $($approval.Length) >>`nstream`n$approval`nendstream"
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R >>'
    "<< /Length $($appendix.Length) >>`nstream`n$appendix`nendstream"
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>'
    '<< /Type /Outlines /First 9 0 R /Last 12 0 R /Count 4 >>'
    '<< /Title (Approval) /Parent 8 0 R /Next 10 0 R /Dest [3 0 R /Fit] >>'
    '<< /Title (Appendix) /Parent 8 0 R /Prev 9 0 R /Next 11 0 R /Dest [5 0 R /Fit] >>'
    '<< /Title (Appendix-null) /Parent 8 0 R /Prev 10 0 R /Next 12 0 R /Dest [5 0 R /XYZ null null null] >>'
    '<< /Title (Appendix-zero) /Parent 8 0 R /Prev 11 0 R /Dest [5 0 R /XYZ 0 0 0] >>'
)
$buffer = [IO.MemoryStream]::new()
try {
    function WriteAscii([string] $text) { $bytes = [Text.Encoding]::ASCII.GetBytes($text); $buffer.Write($bytes, 0, $bytes.Length) }
    WriteAscii "%PDF-1.7`n"
    $offsets = [Collections.Generic.List[long]]::new()
    for ($i = 0; $i -lt $objects.Count; $i++) {
        $offsets.Add($buffer.Position)
        WriteAscii "$($i + 1) 0 obj`n$($objects[$i])`nendobj`n"
    }
    $xref = $buffer.Position
    WriteAscii "xref`n0 $($objects.Count + 1)`n0000000000 65535 f `n"
    foreach ($offset in $offsets) { WriteAscii ($offset.ToString('0000000000', [Globalization.CultureInfo]::InvariantCulture) + " 00000 n `n") }
    WriteAscii "trailer`n<< /Size $($objects.Count + 1) /Root 1 0 R >>`nstartxref`n$xref`n%%EOF`n"
    [IO.File]::WriteAllBytes($path, $buffer.ToArray())
} finally { $buffer.Dispose() }
