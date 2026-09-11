using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells compare</c>: a structural comparison of two workbooks.
/// A completed comparison succeeds (exit 0) — a difference is a successful result
/// reported in <see cref="Identical"/>, not a failure — which lets a caller (or
/// the eval harness) use diff as a verifier.
/// </summary>
public sealed record DiffResult() : ResultEnvelope(CellsSchemaIds.DiffResult, 2)
{
    /// <summary>The left (baseline) file.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Left { get; init; }

    /// <summary>The right (candidate) file.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Right { get; init; }

    /// <summary>True when the two workbooks are equal within the compared scope.</summary>
    [JsonPropertyOrder(-48)]
    public required bool Identical { get; init; }

    /// <summary>Aggregate counts of what differs.</summary>
    [JsonPropertyOrder(-47)]
    public required DiffSummary Summary { get; init; }

    /// <summary>Per-sheet differences; omitted when the workbooks are identical.</summary>
    public IReadOnlyList<SheetDiff>? Sheets { get; init; }

    /// <summary>True when the per-cell list was capped by the diff budget.</summary>
    public bool Truncated { get; init; }
}

/// <summary>Aggregate counts of a <see cref="DiffResult"/>.</summary>
public sealed record DiffSummary
{
    /// <summary>Sheets present only on the right.</summary>
    public required int SheetsAdded { get; init; }

    /// <summary>Sheets present only on the left.</summary>
    public required int SheetsRemoved { get; init; }

    /// <summary>Shared sheets whose cells differ.</summary>
    public required int SheetsModified { get; init; }

    /// <summary>Total differing cells across all shared sheets.</summary>
    public required int CellsDiffering { get; init; }
}

/// <summary>Difference of one sheet.</summary>
public sealed record SheetDiff
{
    /// <summary>Sheet name.</summary>
    public required string Name { get; init; }

    /// <summary>One of <c>added</c>, <c>removed</c> or <c>modified</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Differing cells, for a modified sheet.</summary>
    public IReadOnlyList<CellDiff>? Cells { get; init; }
}

/// <summary>Difference of one cell.</summary>
public sealed record CellDiff
{
    /// <summary>Cell address, e.g. <c>B2</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>The left side; null when the cell is empty there.</summary>
    public CellSide? Left { get; init; }

    /// <summary>The right side; null when the cell is empty there.</summary>
    public CellSide? Right { get; init; }
}

/// <summary>One side of a <see cref="CellDiff"/>.</summary>
public sealed record CellSide
{
    /// <summary>Stored type: empty, number, string, boolean or error. Dates are numbers.</summary>
    public required string T { get; init; }

    /// <summary>Canonical stored value; numbers use raw Excel serials regardless of date formatting.</summary>
    public object? V { get; init; }

    /// <summary>Formula, when present and in scope.</summary>
    public string? F { get; init; }
}
