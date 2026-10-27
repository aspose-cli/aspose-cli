using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells read</c>: a budgeted projection of one sheet's
/// cell data. The envelope's window counts cells: how many the read returned, how many
/// the read covers, and for a read over the cell budget the ready-to-run command that
/// returns the next page. The file on disk remains the primary artifact — this JSON is a
/// view of it, never a round-trip format.
/// </summary>
public sealed record CellsReadResult() : WindowedResultEnvelope("read-result")
{
    /// <summary>The projected file.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>What each cell carries, as <c>--content</c> chose it.</summary>
    [JsonPropertyOrder(-48)]
    [AllowedValues(typeof(CellsReadContents))]
    public required string Content { get; init; }

    /// <summary>Projection of the requested sheet.</summary>
    public required CellsSheetProjection Sheet { get; init; }

    /// <summary>
    /// Style pool referenced by <c>styleId</c> in cells; present only for the
    /// <c>styles</c> and <c>full</c> contents. Pooling styles keeps repeated
    /// formatting from bloating the payload.
    /// </summary>
    public IReadOnlyDictionary<string, CellsStyleData>? Styles { get; init; }
}

/// <summary>Budgeted cell data of one sheet.</summary>
public sealed record CellsSheetProjection
{
    /// <summary>Sheet name, exactly as shown in Excel.</summary>
    public required string Name { get; init; }

    /// <summary>1-based position in the tab order, as insert_sheet and move_sheet take it.</summary>
    [Minimum(1)]
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
    public IReadOnlyList<IReadOnlyList<CellsCellData>>? Cells { get; init; }
}

/// <summary>One cell in a projection. Empty cells are <c>{"t":"empty"}</c>.</summary>
public sealed record CellsCellData
{
    /// <summary>
    /// The cell value: string, number, boolean, an ISO 8601 string for
    /// date/time values, the error literal (e.g. <c>#DIV/0!</c>) for error
    /// cells, or omitted for empty cells.
    /// </summary>
    [OneOfBy("t", CellsCellValueTypes.String, CellsCellValueTypes.DateTime, CellsCellValueTypes.Error, Type = "string")]
    [OneOfBy("t", CellsCellValueTypes.Number, Type = "number")]
    [OneOfBy("t", CellsCellValueTypes.Boolean, Type = "boolean")]
    [OneOfBy("t", CellsCellValueTypes.Empty)]
    public object? V { get; init; }

    /// <summary>Value type.</summary>
    [AllowedValues(typeof(CellsCellValueTypes))]
    public required string T { get; init; }

    /// <summary>Formula in A1 notation (with leading <c>=</c>); only with formulas content.</summary>
    public string? F { get; init; }

    /// <summary>Key into the style pool; only with styles content.</summary>
    public string? StyleId { get; init; }
}

/// <summary>Accepted values of <see cref="CellsCellData.T"/>.</summary>
public static class CellsCellValueTypes
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
