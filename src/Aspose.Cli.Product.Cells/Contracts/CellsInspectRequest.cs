namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells inspect</c>.</summary>
public sealed record CellsInspectRequest
{
    /// <summary>The workbook to inspect.</summary>
    public required string Input { get; init; }

    /// <summary>Include a small sample of display values per sheet.</summary>
    public bool IncludePreview { get; init; }

    /// <summary>Number of sample rows per sheet.</summary>
    public int SampleRows { get; init; } = 5;

    /// <summary>Extra detail sections to include (see <c>CellsInspectDetails</c>).</summary>
    public IReadOnlyList<string>? Details { get; init; }

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }
}

/// <summary>Accepted values of <c>cells inspect --detail</c>.</summary>
public static class CellsInspectDetails
{
    /// <summary>Defined names.</summary>
    public const string Names = "names";

    /// <summary>A formula-error scan (cells evaluating to #REF!, #DIV/0!, etc.).</summary>
    public const string Errors = "errors";

    /// <summary>The distinct fonts the workbook uses (a rendering-fidelity check).</summary>
    public const string Fonts = "fonts";

    /// <summary>Tables (list objects) in the workbook.</summary>
    public const string Tables = "tables";

    /// <summary>Charts in the workbook.</summary>
    public const string Charts = "charts";

    /// <summary>Pivot tables in the workbook.</summary>
    public const string Pivots = "pivots";

    /// <summary>Data validations in the workbook.</summary>
    public const string Validation = "validation";

    /// <summary>Conditional-formatting rules in the workbook.</summary>
    public const string ConditionalFormats = "conditional-formats";

    /// <summary>Sparkline groups in the workbook.</summary>
    public const string Sparklines = "sparklines";

    /// <summary>Pictures in the workbook, with their size in points.</summary>
    public const string Images = "images";

    /// <summary>Cell comments (notes) in the workbook.</summary>
    public const string Comments = "comments";

    /// <summary>Hyperlinks in the workbook.</summary>
    public const string Hyperlinks = "hyperlinks";

    /// <summary>Merged ranges in the workbook.</summary>
    public const string Merges = "merges";

    /// <summary>
    /// The layout of each sheet: view, tab color, frozen panes, row heights and column widths,
    /// outline groups, AutoFilter, protection, print area and titles, orientation, scaling,
    /// header and footer.
    /// </summary>
    public const string Layout = "layout";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Names, Errors, Fonts, Tables, Charts, Pivots, Validation, ConditionalFormats, Sparklines, Images, Comments, Hyperlinks, Merges, Layout];
}
