using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells;

/// <summary>Product-owned bounded signatures for generic workbook routing.</summary>
internal static class CellsFormatRecognition
{
    private static readonly FileFormatRecognition CompoundWorkbook =
        FileFormatRecognition.Match(
            FileProbePattern.BytesAt(
                0,
                0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1),
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
