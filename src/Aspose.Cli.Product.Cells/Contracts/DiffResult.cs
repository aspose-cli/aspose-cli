using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells compare</c>: a structural comparison of two workbooks.
/// A completed comparison succeeds (exit 0) even when the workbooks differ: the verdict is
/// <c>identical</c>, not a failure, which lets a caller (or the eval harness) use compare as
/// a verifier.
/// </summary>
public sealed record DiffResult() : ResultEnvelope("diff-result", 2)
{
    /// <summary>The left (baseline) file.</summary>
    [JsonPropertyOrder(-50)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Left { get; init; }

    /// <summary>The right (candidate) file.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Right { get; init; }

    /// <summary>True when the two workbooks are equal within the compared scope.</summary>
    [JsonPropertyOrder(-48)]
    public required bool Identical { get; init; }

    /// <summary>Aggregate counts of what differs.</summary>
    [JsonPropertyOrder(-47)]
    public required DiffSummary Summary { get; init; }

    /// <summary>
    /// Per-sheet differences; omitted when the workbooks are identical. The listed cells
    /// stop at the diff budget, which a <c>LIST_TRUNCATED</c> warning discloses.
    /// </summary>
    public IReadOnlyList<SheetDiff>? Sheets { get; init; }
}

/// <summary>Aggregate counts of a workbook comparison.</summary>
public sealed record DiffSummary
{
    /// <summary>Sheets present only on the right.</summary>
    [Minimum(0)]
    public required int SheetsAdded { get; init; }

    /// <summary>Sheets present only on the left.</summary>
    [Minimum(0)]
    public required int SheetsRemoved { get; init; }

    /// <summary>Sheets the right holds under a new name, paired with the left sheet by their internal sheet id.</summary>
    [Minimum(0)]
    public required int SheetsRenamed { get; init; }

    /// <summary>Shared sheets whose cells differ.</summary>
    [Minimum(0)]
    public required int SheetsModified { get; init; }

    /// <summary>
    /// Total differing cells across all shared and renamed sheets: the true count even when a
    /// <c>LIST_TRUNCATED</c> warning reports that <c>--max-diffs</c> capped the listed cells.
    /// </summary>
    [Minimum(0)]
    public required int CellsDiffering { get; init; }
}

/// <summary>Difference of one sheet.</summary>
public sealed record SheetDiff
{
    /// <summary>Sheet name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// One of <c>added</c>, <c>removed</c>, <c>modified</c> or <c>renamed</c>: a sheet both sides
    /// hold under different names, paired by its internal id.
    /// </summary>
    [AllowedValues(typeof(SheetChangeStatuses))]
    public required string Status { get; init; }

    /// <summary>The sheet's name on the left, for a renamed sheet.</summary>
    public string? From { get; init; }

    /// <summary>Differing cells, for a modified or renamed sheet.</summary>
    public IReadOnlyList<CellDiff>? Cells { get; init; }
}

/// <summary>Accepted values of the status of a changed sheet.</summary>
public static class SheetChangeStatuses
{
    /// <summary>The sheet is present only on the right.</summary>
    public const string Added = "added";

    /// <summary>The sheet is present only on the left.</summary>
    public const string Removed = "removed";

    /// <summary>Both sides hold the sheet under one name and its cells differ.</summary>
    public const string Modified = "modified";

    /// <summary>Both sides hold the sheet under different names, paired by its internal id.</summary>
    public const string Renamed = "renamed";
}

/// <summary>Difference of one cell.</summary>
public sealed record CellDiff
{
    /// <summary>Cell address, e.g. <c>B2</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>The left side; omitted when the cell is empty there.</summary>
    public CellSide? Left { get; init; }

    /// <summary>The right side; omitted when the cell is empty there.</summary>
    public CellSide? Right { get; init; }
}

/// <summary>
/// One side of a changed cell: its canonical stored value and optional formula. Dates are raw
/// numeric serials; an absent empty side is omitted.
/// </summary>
public sealed record CellSide
{
    /// <summary>Stored type: empty, number, string, boolean or error. Dates are numbers.</summary>
    [AllowedValues(typeof(CellSideTypes))]
    public required string T { get; init; }

    /// <summary>
    /// Canonical stored value: a number (raw Excel serials regardless of date formatting), a
    /// string, a boolean or an error literal; omitted for an empty cell.
    /// </summary>
    [OneOfBy("t", CellSideTypes.Empty)]
    [OneOfBy("t", CellSideTypes.Number, Type = "number")]
    [OneOfBy("t", CellSideTypes.String, CellSideTypes.Error, Type = "string")]
    [OneOfBy("t", CellSideTypes.Boolean, Type = "boolean")]
    public object? V { get; init; }

    /// <summary>Formula, when present and in scope.</summary>
    public string? F { get; init; }
}

/// <summary>Stored types of a <see cref="CellSide"/>.</summary>
public static class CellSideTypes
{
    /// <summary>An empty cell.</summary>
    public const string Empty = "empty";

    /// <summary>A number, including a date's serial.</summary>
    public const string Number = "number";

    /// <summary>Text.</summary>
    public const string String = "string";

    /// <summary>A boolean.</summary>
    public const string Boolean = "boolean";

    /// <summary>An error value such as <c>#DIV/0!</c>.</summary>
    public const string Error = "error";
}
