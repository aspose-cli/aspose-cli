using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// Result of <c>words search</c>: one window of search hits; its <c>window</c> counts hits
/// and continues with <c>--skip</c>.
/// </summary>
public sealed record WordsSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The text or regular expression searched for.</summary>
    [JsonPropertyOrder(-49)]
    public required string Pattern { get; init; }

    /// <summary>The hits of this window, in document order.</summary>
    [JsonPropertyOrder(-48)]
    public required IReadOnlyList<WordsSearchHit> Hits { get; init; }
}

/// <summary>One search hit: each match is one, so a paragraph that holds two matches gives two.</summary>
public sealed record WordsSearchHit
{
    /// <summary>The body block that holds the hit; absent in headers and footers.</summary>
    [Minimum(1)]
    public int? Block { get; init; }

    /// <summary>The section whose body, header or footer holds the hit.</summary>
    [Minimum(1)]
    public required int Section { get; init; }

    /// <summary>The story that holds the hit.</summary>
    [AllowedValues(typeof(WordsStoryScopes))]
    public required string Scope { get; init; }

    /// <summary>In a header or footer, which one holds the hit; absent elsewhere.</summary>
    [AllowedValues(typeof(WordsHeaderFooterLocations))]
    public string? Location { get; init; }

    /// <summary>In a header or footer, its kind as set_header and set_footer name it; absent elsewhere.</summary>
    [AllowedValues(typeof(WordsHeaderFooterKinds))]
    public string? Kind { get; init; }

    /// <summary>The match itself.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The text of the paragraph, comment or note around the match, up to 40 characters on each
    /// side, with line breaks as spaces and an ellipsis where it is cut.
    /// </summary>
    public required string Context { get; init; }

    /// <summary>The match's 1-based position among the matches of the document.</summary>
    [Minimum(1)]
    public required int Occurrence { get; init; }
}
