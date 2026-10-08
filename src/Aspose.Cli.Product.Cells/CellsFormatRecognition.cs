using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells;

/// <summary>Product-owned bounded signatures for generic workbook routing.</summary>
internal static class CellsFormatRecognition
{
    private static readonly FileFormatRecognition CompoundWorkbook =
        FileFormatRecognition.Match(
            FileProbePattern.OleCompoundFile(),
            "OLE compound-file signature with extension-qualified format",
            70);

    // A password-encrypted package is an OLE compound file that holds the encrypted ZIP.
    private static readonly FileFormatRecognition SpreadsheetPackage =
        FileFormatRecognition.FirstOf(
            FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny(
                    "xl/",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml"),
                "spreadsheet package marker"),
            CompoundWorkbook);

    internal static IReadOnlyDictionary<string, FileFormatRecognition>
        Rules
    { get; } = new Dictionary<string, FileFormatRecognition>(
        StringComparer.Ordinal)
    {
        ["xlsx"] = SpreadsheetPackage,
        ["xltx"] = SpreadsheetPackage,
        ["xlsm"] = SpreadsheetPackage,
        ["xltm"] = SpreadsheetPackage,
        ["xlsb"] = SpreadsheetPackage,
        ["xls"] = CompoundWorkbook,
        ["ods"] = FileFormatRecognition.Match(
            FileProbePattern.ZipContainsAny(
                "application/vnd.oasis.opendocument.spreadsheet"),
            "OpenDocument spreadsheet mimetype"),
        ["csv"] = FileFormatRecognition.Match(
            FileProbePattern.All(
                FileProbePattern.ValidText(),
                FileProbePattern.TextContains(",")),
            "bounded delimited-text grammar",
            70),
        ["tsv"] = FileFormatRecognition.Match(
            FileProbePattern.All(
                FileProbePattern.ValidText(),
                FileProbePattern.TextContains("\t")),
            "bounded tab-delimited grammar",
            70),
    };
}
