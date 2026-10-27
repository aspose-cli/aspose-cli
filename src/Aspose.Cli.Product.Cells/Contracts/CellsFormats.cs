
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The format vocabulary of the cells product. This list is public contract:
/// ids are stable, and the split between <c>convert</c> (documents) and
/// <c>render</c> (images of a sheet) is intentional.
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
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, true, ".xlsb") with { Protectable = true, Recognition = CellsFormatRecognition.SpreadsheetPackage },
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, true, ".xls") with { Protectable = true, Recognition = CellsFormatRecognition.CompoundWorkbook },
        FormatDescriptor.Declare("ods", FormatUse.Input | FormatUse.Convert, true, ".ods") with
        {
            Protectable = true,
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny("application/vnd.oasis.opendocument.spreadsheet"),
                "OpenDocument spreadsheet mimetype"),
        },
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
        // HTML and MHTML are written only: reading them, the engine opens the images they name by
        // path, past every resource hook, and no option turns that off.
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

    /// <summary>
    /// The formats create and edit write: the workbook formats the engine both loads and saves,
    /// so the output reopens as a workbook that can be read, verified and edited again. html,
    /// mhtml, pdf, xps, json and md are renditions it does not load; convert writes them from one
    /// of these.
    /// </summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input | FormatUse.Convert))];

    /// <summary>
    /// How the commands treat each declared format, one row per format id in declaration order.
    /// A product test keeps the rows in step with <see cref="Definitions"/>.
    /// </summary>
    internal static IReadOnlyList<CellsFormatTraits> Traits { get; } =
    [
        new("xlsx"),
        new("xltx"),
        new("xlsm"),
        new("xltm"),
        new("xlsb"),
        new("xls"),
        new("ods"),
        new("csv") { SheetScope = CellsSheetScope.ActiveSheet, Delimited = true },
        new("tsv") { SheetScope = CellsSheetScope.ActiveSheet, Delimited = true },
        new("html"),
        new("mhtml"),
        new("pdf") { SheetScope = CellsSheetScope.SheetSet },
        new("xps"),
        new("json"),
        new("md") { SheetScope = CellsSheetScope.ActiveSheet },
        new("png"),
        new("jpeg"),
        new("svg"),
    ];

    private static readonly Dictionary<string, CellsFormatTraits> TraitsById =
        Traits.ToDictionary(static traits => traits.Id, StringComparer.Ordinal);

    /// <summary>
    /// Convert formats that hold one worksheet by nature: they export the active sheet, so for
    /// them <c>--sheet</c> is the only way to reach any other one.
    /// </summary>
    public static IReadOnlyList<string> ActiveSheetOnly { get; } =
        [.. Traits.Where(static traits => traits.SheetScope == CellsSheetScope.ActiveSheet).Select(static traits => traits.Id)];

    /// <summary>
    /// Convert formats that can be limited to a single sheet with <c>--sheet</c>: the
    /// <see cref="ActiveSheetOnly"/> formats, then those that hold a set of sheets (pdf). Every
    /// other format always converts the whole workbook.
    /// </summary>
    public static IReadOnlyList<string> SheetScoped { get; } =
        [.. Traits.Where(static traits => traits.SheetScope != CellsSheetScope.Workbook).OrderBy(static traits => traits.SheetScope).Select(static traits => traits.Id)];

    /// <summary>Which sheets a convert output in <paramref name="id"/> holds; the whole workbook for an undeclared id.</summary>
    internal static CellsSheetScope SheetScopeOf(string id) => TraitsById.GetValueOrDefault(id)?.SheetScope ?? CellsSheetScope.Workbook;

    /// <summary>Whether <paramref name="id"/> is a delimited text format (CSV or TSV).</summary>
    internal static bool IsDelimited(string id) => TraitsById.GetValueOrDefault(id)?.Delimited == true;

    /// <summary>The delimited text formats (CSV and TSV), in declaration order.</summary>
    internal static IReadOnlyList<FormatDescriptor> Delimited { get; } =
        [.. Definitions.Where(static format => IsDelimited(format.Id))];
}
