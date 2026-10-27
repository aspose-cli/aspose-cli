using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells compare</c>: a structural comparison of two workbooks.
/// A completed comparison succeeds (exit 0) even when the workbooks differ: the verdict is
/// <c>identical</c>, not a failure, which lets a caller (or the eval harness) use compare as
/// a verifier.
/// </summary>
public sealed record CellsCompareResult() : EngineResultEnvelope("compare-result")
{
    /// <summary>The left (baseline) file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Left { get; init; }

    /// <summary>The right (candidate) file.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Right { get; init; }

    /// <summary>True when the two workbooks are equal within the compared scope.</summary>
    [JsonPropertyOrder(-48)]
    public required bool Identical { get; init; }

    /// <summary>Aggregate counts of what differs.</summary>
    [JsonPropertyOrder(-47)]
    public required CellsCompareSummary Summary { get; init; }

    /// <summary>
    /// Per-sheet differences; omitted when the workbooks are identical. The listed cells
    /// stop at the diff budget, which a <c>LIST_TRUNCATED</c> warning discloses.
    /// </summary>
    public IReadOnlyList<CellsSheetDiff>? Sheets { get; init; }
}

/// <summary>Aggregate counts of a workbook comparison.</summary>
public sealed record CellsCompareSummary
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
public sealed record CellsSheetDiff
{
    /// <summary>Sheet name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// One of <c>added</c>, <c>removed</c>, <c>modified</c> or <c>renamed</c>: a sheet both sides
    /// hold under different names, paired by its internal id.
    /// </summary>
    [AllowedValues(typeof(CellsSheetChangeStatuses))]
    public required string Status { get; init; }

    /// <summary>The sheet's name on the left, for a renamed sheet.</summary>
    public string? From { get; init; }

    /// <summary>Differing cells, for a modified or renamed sheet.</summary>
    public IReadOnlyList<CellsCellDiff>? Cells { get; init; }
}

/// <summary>Difference of one cell.</summary>
public sealed record CellsCellDiff
{
    /// <summary>Cell address, e.g. <c>B2</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>The left side; omitted when the cell is empty there.</summary>
    public CellsCellSide? Left { get; init; }

    /// <summary>The right side; omitted when the cell is empty there.</summary>
    public CellsCellSide? Right { get; init; }
}
