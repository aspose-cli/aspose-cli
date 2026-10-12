using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>The bounded text search of <c>pdf query search</c>.</summary>
public sealed record PdfSearchResult() : WindowedResultEnvelope("search-result", 2)
{
    /// <summary>The PDF searched.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The text or regular expression searched for.</summary>
    public required string Pattern { get; init; }

    /// <summary>Each match in the window, in page order.</summary>
    public required IReadOnlyList<PdfSearchHit> Hits { get; init; }
}

/// <summary>One match of a text search.</summary>
public sealed record PdfSearchHit
{
    /// <summary>The 1-based page of the match.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The matched text.</summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// The page text around the match, up to 40 characters on each side, with line breaks as
    /// spaces and an ellipsis where it is cut; absent when the page's plain text does not show
    /// the hits the engine found, or when the text it pairs with this hit by order differs from
    /// the match (ignoring case).
    /// </summary>
    public string? Context { get; init; }

    /// <summary>Where the match lies on its page.</summary>
    public required PdfRect Rect { get; init; }

    /// <summary>The match's 1-based position among the matches of the document.</summary>
    [Minimum(1)]
    public required int Occurrence { get; init; }
}
