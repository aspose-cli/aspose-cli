using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells;

/// <summary>Product-owned bounded signatures for generic workbook routing.</summary>
internal static class CellsFormatRecognition
{
    private static readonly FileProbePattern CompoundFile =
        FileProbePattern.BytesAt(
            0,
            0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1);

    private static readonly FileProbePattern SpreadsheetPackage =
        FileProbePattern.ZipContainsAny(
            "xl/",
            "application/vnd.openxmlformats-officedocument.spreadsheetml");

    internal static IReadOnlyDictionary<string, FileFormatRecognition>
        Rules
    { get; } = new Dictionary<string, FileFormatRecognition>(
        StringComparer.Ordinal)
    {
        ["xlsx"] = FileFormatRecognition.Match(
            SpreadsheetPackage,
            "spreadsheet package marker"),
        ["xlsm"] = FileFormatRecognition.Match(
            SpreadsheetPackage,
            "spreadsheet package marker"),
        ["xlsb"] = FileFormatRecognition.Match(
            SpreadsheetPackage,
            "spreadsheet package marker"),
        ["xls"] = FileFormatRecognition.Match(
            CompoundFile,
            "OLE compound-file signature with extension-qualified format",
            70),
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
