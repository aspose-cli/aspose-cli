using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells query search</c>: one window of cell hits for a pattern.
/// Hits are ordered by sheet then row-major, so output is deterministic; the envelope's
/// window counts the returned hits and, when more match, carries the command that returns
/// the next ones.
/// </summary>
[AlwaysPresent("window")]
public sealed record SearchResult() : ResultEnvelope("search-result", 2)
{
    /// <summary>The searched file.</summary>
    [JsonPropertyOrder(-50)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Source { get; init; }

    /// <summary>The pattern that was searched for (echoed back).</summary>
    [JsonPropertyOrder(-49)]
    public required string Pattern { get; init; }

    /// <summary>Matching cells.</summary>
    [JsonPropertyOrder(-48)]
    public required IReadOnlyList<SearchHit> Hits { get; init; }
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
