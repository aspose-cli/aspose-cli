namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells read</c>.</summary>
public sealed record CellsReadRequest
{
    /// <summary>The workbook to read.</summary>
    public required string Input { get; init; }

    /// <summary>Sheet to project; the active sheet when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>
    /// Window to project; null means the used range. A read over the cell budget returns its
    /// first window and a follow-up command for the next one: of the used range, or of the data
    /// inside an explicit range.
    /// </summary>
    public RangeRef? Range { get; init; }

    /// <summary>
    /// The region of a planned scan that <see cref="Range"/> is one page of, carried by a
    /// generated follow-up command; null for the first read of a scan.
    /// </summary>
    public RangeRef? Scan { get; init; }

    /// <summary>What each cell carries, one of <see cref="CellsReadContents"/>.</summary>
    public string Content { get; init; } = CellsReadContents.Values;

    /// <summary>Maximum number of cells the projection may return.</summary>
    public int MaxCells { get; init; } = 10_000;

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }
}

/// <summary>Accepted values of <c>cells read --content</c>.</summary>
public static class CellsReadContents
{
    /// <summary>Cell values and types only (the default).</summary>
    public const string Values = "values";

    /// <summary>Values plus formulas.</summary>
    public const string Formulas = "formulas";

    /// <summary>Values plus style references and the style pool.</summary>
    public const string Styles = "styles";

    /// <summary>Values, formulas, styles.</summary>
    public const string Full = "full";

    /// <summary>Whether a content carries formulas.</summary>
    internal static bool IncludesFormulas(string content) => content is Formulas or Full;

    /// <summary>Whether a content carries styles.</summary>
    internal static bool IncludesStyles(string content) => content is Styles or Full;
}
