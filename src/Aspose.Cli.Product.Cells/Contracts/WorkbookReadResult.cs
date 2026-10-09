using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells query range</c>: a budgeted projection of one sheet's
/// cell data. The envelope's window counts cells: how many the read returned, how many
/// the read covers, and for a planned scan (a default read over the used range, or a page
/// generated for an explicit range too large for one read) the ready-to-run command that
/// returns the next page. The file on disk remains the primary artifact — this JSON is a
/// view of it, never a round-trip format.
/// </summary>
public sealed record WorkbookReadResult() : ResultEnvelope("workbook-read", 2)
{
    /// <summary>Document kind discriminator; always <c>workbook</c> for cells.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = DocumentKinds.Workbook;

    /// <summary>The projected file.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    /// <summary>The scope that was projected.</summary>
    [JsonPropertyOrder(-48)]
    [AllowedValues(typeof(ReadScopes))]
    public required string Scope { get; init; }

    /// <summary>Projection of the requested sheet.</summary>
    public required SheetProjection Sheet { get; init; }

    /// <summary>
    /// Style pool referenced by <c>styleId</c> in cells; present only for the
    /// <c>styles</c> and <c>full</c> scopes. Pooling styles keeps repeated
    /// formatting from bloating the payload.
    /// </summary>
    public IReadOnlyDictionary<string, StyleData>? Styles { get; init; }
}

/// <summary>Accepted values of the read scope.</summary>
public static class ReadScopes
{
    /// <summary>Cell values and types only (the default).</summary>
    public const string Values = "values";

    /// <summary>Values plus formulas.</summary>
    public const string Formulas = "formulas";

    /// <summary>Values plus style references and the style pool.</summary>
    public const string Styles = "styles";

    /// <summary>Values, formulas, styles.</summary>
    public const string Full = "full";
}

/// <summary>Budgeted cell data of one sheet.</summary>
public sealed record SheetProjection
{
    /// <summary>Sheet name, exactly as shown in Excel.</summary>
    public required string Name { get; init; }

    /// <summary>Zero-based position in the tab order, as add_sheet and move_sheet take it.</summary>
    [Minimum(0)]
    public required int Position { get; init; }

    /// <summary>A1 range covering all data on the sheet; omitted when empty.</summary>
    public string? UsedRange { get; init; }

    /// <summary>
    /// A1 range returned in <c>cells</c>; omitted when no cells were returned, as for
    /// an empty sheet or a default read over the cell budget.
    /// </summary>
    public string? Range { get; init; }

    /// <summary>
    /// Row-major cell matrix covering exactly <c>range</c>. Omitted when the sheet
    /// is empty or a default read over the cell budget returned only a summary.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<CellData>>? Cells { get; init; }
}

/// <summary>One cell in a projection. Empty cells are <c>{"t":"empty"}</c>.</summary>
public sealed record CellData
{
    /// <summary>
    /// The cell value: string, number, boolean, an ISO 8601 string for
    /// date/time values, the error literal (e.g. <c>#DIV/0!</c>) for error
    /// cells, or omitted for empty cells.
    /// </summary>
    [OneOfBy("t", CellValueTypes.String, CellValueTypes.DateTime, CellValueTypes.Error, Type = "string")]
    [OneOfBy("t", CellValueTypes.Number, Type = "number")]
    [OneOfBy("t", CellValueTypes.Boolean, Type = "boolean")]
    [OneOfBy("t", CellValueTypes.Empty)]
    public object? V { get; init; }

    /// <summary>Value type.</summary>
    [AllowedValues(typeof(CellValueTypes))]
    public required string T { get; init; }

    /// <summary>Formula in A1 notation (with leading <c>=</c>); only in formula-bearing scopes.</summary>
    public string? F { get; init; }

    /// <summary>Key into the style pool; only in style-bearing scopes.</summary>
    public string? StyleId { get; init; }
}

/// <summary>Accepted values of <see cref="CellData.T"/>.</summary>
public static class CellValueTypes
{
    /// <summary>Text.</summary>
    public const string String = "string";

    /// <summary>A number.</summary>
    public const string Number = "number";

    /// <summary>A boolean.</summary>
    public const string Boolean = "boolean";

    /// <summary>A date or time, reported as an ISO 8601 string.</summary>
    public const string DateTime = "datetime";

    /// <summary>An error value such as <c>#DIV/0!</c>.</summary>
    public const string Error = "error";

    /// <summary>An empty cell.</summary>
    public const string Empty = "empty";
}
