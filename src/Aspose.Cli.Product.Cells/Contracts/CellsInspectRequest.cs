namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells inspect</c>.</summary>
public sealed record CellsInspectRequest
{
    /// <summary>The workbook to inspect.</summary>
    public required string Input { get; init; }

    /// <summary>Include a small sample of display values per sheet.</summary>
    public bool IncludePreview { get; init; }

    /// <summary>Number of preview rows per sheet.</summary>
    public int PreviewRows { get; init; } = 5;

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

    /// <summary>
    /// The layout of each sheet: frozen panes, outline groups, AutoFilter, print area and
    /// titles, orientation, scaling, header and footer.
    /// </summary>
    public const string Layout = "layout";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Names, Errors, Fonts, Tables, Charts, Pivots, Validation, Layout];
}
