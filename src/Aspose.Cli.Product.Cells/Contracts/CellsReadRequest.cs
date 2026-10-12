namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells query range</c>.</summary>
public sealed record CellsReadRequest
{
    /// <summary>The workbook to read.</summary>
    public required string Input { get; init; }

    /// <summary>Sheet to project; the active sheet when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>
    /// Window to project. Null means the used range, subject to the cell
    /// budget: over-budget default reads return a summary plus a follow-up
    /// command instead of data, while an explicit over-budget range is an
    /// error — the caller asked for something specific and gets an honest no.
    /// </summary>
    public RangeRef? Range { get; init; }

    /// <summary>
    /// The region of a planned scan that <see cref="Range"/> is one page of, carried by a
    /// generated follow-up command; null for a default read or a complete explicit range.
    /// </summary>
    public RangeRef? Scan { get; init; }

    /// <summary>Projection scope, one of <see cref="CellsReadScopes"/>.</summary>
    public string Scope { get; init; } = CellsReadScopes.Values;

    /// <summary>Maximum number of cells the projection may return.</summary>
    public int MaxCells { get; init; } = 10_000;

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }
}

/// <summary>Accepted values of the read scope.</summary>
public static class CellsReadScopes
{
    /// <summary>Cell values and types only (the default).</summary>
    public const string Values = "values";

    /// <summary>Values plus formulas.</summary>
    public const string Formulas = "formulas";

    /// <summary>Values plus style references and the style pool.</summary>
    public const string Styles = "styles";

    /// <summary>Values, formulas, styles.</summary>
    public const string Full = "full";

    /// <summary>Whether a scope carries formulas.</summary>
    internal static bool IncludesFormulas(string scope) => scope is Formulas or Full;

    /// <summary>Whether a scope carries styles.</summary>
    internal static bool IncludesStyles(string scope) => scope is Styles or Full;
}
