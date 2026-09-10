using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells query search</c>: budgeted cell hits for a pattern.
/// Hits are ordered by sheet then row-major, so output is deterministic; the
/// list is capped by the hit budget, which <see cref="Truncated"/> and
/// <see cref="Hint"/> disclose rather than silently dropping matches.
/// </summary>
public sealed record SearchResult() : ResultEnvelope(CellsSchemaIds.SearchResult, 2)
{
    /// <summary>The searched file.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Source { get; init; }

    /// <summary>The pattern that was searched for (echoed back).</summary>
    [JsonPropertyOrder(-49)]
    public required string Pattern { get; init; }

    /// <summary>Matching cells.</summary>
    [JsonPropertyOrder(-48)]
    public required IReadOnlyList<SearchHit> Hits { get; init; }

    /// <summary>True when the hit list was capped by the budget.</summary>
    public bool Truncated { get; init; }

    /// <summary>How to narrow the search; set only when truncated.</summary>
    public string? Hint { get; init; }
}

/// <summary>One matching cell.</summary>
public sealed record SearchHit
{
    /// <summary>Sheet the cell is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>Cell address, e.g. <c>B7</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>Display value of the cell (truncated for long text).</summary>
    public required string Value { get; init; }

    /// <summary>Formula, when the cell has one.</summary>
    public string? Formula { get; init; }
}
