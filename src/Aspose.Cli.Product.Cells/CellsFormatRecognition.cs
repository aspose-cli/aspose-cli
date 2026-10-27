
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Cells;

/// <summary>Bounded signatures that several workbook formats share for routing.</summary>
internal static class CellsFormatRecognition
{
    internal static readonly FileFormatRecognition CompoundWorkbook =
        FileFormatRecognition.Match(
            FileProbePattern.OleCompoundFile(),
            "OLE compound-file signature with extension-qualified format",
            70);

    // A password-encrypted package is an OLE compound file that holds the encrypted ZIP.
    internal static readonly FileFormatRecognition SpreadsheetPackage =
        FileFormatRecognition.FirstOf(
            FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny(
                    "xl/",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml"),
                "spreadsheet package marker"),
            CompoundWorkbook);
}
