using Aspose.Cli.Sdk.IO;

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
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Declare("xlsx", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".xlsx") with { Protectable = true },
        FormatDescriptor.Declare("xltx", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".xltx") with { Protectable = true },
        FormatDescriptor.Declare("xlsm", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".xlsm") with { Protectable = true },
        FormatDescriptor.Declare("xltm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".xltm") with { Protectable = true },
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".xlsb") with { Protectable = true },
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".xls") with { Protectable = true },
        FormatDescriptor.Declare("ods", FormatUse.Input | FormatUse.Convert, 6, 6, null, true, ".ods") with { Protectable = true },
        FormatDescriptor.Declare("csv", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".csv", ".txt")
            with { UnroutedExtensions = [".txt"] },
        FormatDescriptor.Declare("tsv", FormatUse.Input | FormatUse.Convert, 8, 8, null, true, ".tsv", ".txt")
            with { UnroutedExtensions = [".txt"] },
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 9, 9, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 10, 10, null, false, ".mhtml"),
        FormatDescriptor.Declare("pdf", FormatUse.Convert, null, 11, null, false, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, null, 12, null, false, ".xps"),
        FormatDescriptor.Declare("json", FormatUse.Convert, null, 13, null, false, ".json"),
        FormatDescriptor.Declare("md", FormatUse.Convert, null, 14, null, false, ".md", ".markdown"),
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], CellsFormatRecognition.Rules);

    /// <summary>
    /// The formats create and edit write: the workbook formats the engine both loads and saves,
    /// so the output reopens as a workbook that can be read, verified and edited again. pdf, xps,
    /// json and md are renditions it cannot load; convert writes them from one of these.
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
}
