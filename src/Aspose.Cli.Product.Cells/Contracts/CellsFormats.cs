using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The format table of the cells product: one row per format. A workbook format the engine both
/// opens and saves is read, edited (in its own format) and converted; CSV and TSV are converted
/// from and to, with every text option stated; the rest are written only.
/// </summary>
public static class CellsFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Declare("xlsx", FormatUse.Input | FormatUse.Convert, true, ".xlsx") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xltx", FormatUse.Input | FormatUse.Convert, true, ".xltx") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xlsm", FormatUse.Input | FormatUse.Convert, true, ".xlsm") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xltm", FormatUse.Input | FormatUse.Convert, true, ".xltm") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xlam", FormatUse.Input | FormatUse.Convert, true, ".xlam") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, true, ".xlsb") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, true, ".xls") with { Protectable = true, Recognition = CellsFormatRecognition.CompoundWorkbook },
        FormatDescriptor.Declare("xlt", FormatUse.Input | FormatUse.Convert, true, ".xlt") with { Protectable = true, Recognition = CellsFormatRecognition.CompoundWorkbook },
        FormatDescriptor.Declare("csv", FormatUse.Input | FormatUse.Convert, true, ".csv", ".txt") with
        {
            UnroutedExtensions = [".txt"],
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.All(FileProbePattern.ValidText(), FileProbePattern.TextContains(",")),
                "bounded delimited-text grammar",
                70),
        },
        FormatDescriptor.Declare("tsv", FormatUse.Input | FormatUse.Convert, true, ".tsv", ".txt") with
        {
            UnroutedExtensions = [".txt"],
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.All(FileProbePattern.ValidText(), FileProbePattern.TextContains("\t")),
                "bounded tab-delimited grammar",
                70),
        },
        // Written only: reading an OpenDocument spreadsheet, the engine fetches what its SVG
        // pictures name while it loads, and HTML or MHTML, the images the page names; no engine
        // option or callback stops either (known issue CELLS-SVG-EGRESS).
        FormatDescriptor.Declare("ods", FormatUse.Convert, false, ".ods") with { Protectable = true },
        FormatDescriptor.Declare("html", FormatUse.Convert, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Convert, false, ".mhtml"),
        FormatDescriptor.Declare("pdf", FormatUse.Convert, false, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, false, ".xps"),
        FormatDescriptor.Declare("json", FormatUse.Convert, false, ".json"),
        FormatDescriptor.Declare("md", FormatUse.Convert, false, ".md", ".markdown"),
        FormatDescriptor.Declare("png", FormatUse.Render, false, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, false, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, false, ".svg"),
    ];

    /// <summary>The delimited text formats, which only convert reads and writes.</summary>
    internal static IReadOnlyList<string> Delimited { get; } = ["csv", "tsv"];

    /// <summary>The separator a delimited text format has itself, by format id: a tsv input is tab-separated.</summary>
    internal static IReadOnlyDictionary<string, char> FixedSeparators { get; } = new Dictionary<string, char>(StringComparer.Ordinal) { ["tsv"] = '	' };

    /// <summary>The convert formats whose output holds only the sheets <c>--sheet</c> names: the one-sheet formats, and pdf.</summary>
    internal static IReadOnlyList<string> SheetSelected { get; } = ["csv", "tsv", "md", "pdf"];

    /// <summary>
    /// The text format the name of <paramref name="path"/> carries, which only convert reads, with
    /// the text options it states: the first declared of the ones it names, as a .txt input is
    /// csv text; null for a name no text format declares.
    /// </summary>
    internal static FormatDescriptor? TextFormatOf(string path) =>
        Definitions.NamedBy(path).FirstOrDefault(static format => Delimited.Contains(format.Id, StringComparer.Ordinal));

    /// <summary>The convert formats that hold one sheet, which <c>--sheet</c> names.</summary>
    internal static IReadOnlyList<string> OneSheet { get; } = ["csv", "tsv", "md"];

    /// <summary>
    /// The workbook formats the engine both opens and saves: create writes them and edit writes
    /// each back in its own format.
    /// </summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input | FormatUse.Convert) && !Delimited.Contains(format.Id))];
}
