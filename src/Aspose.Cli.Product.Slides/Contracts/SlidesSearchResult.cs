using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides search</c>: the matches in shape text and speaker notes.</summary>
public sealed record SlidesSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The searched pattern as given.</summary>
    public required string Pattern { get; init; }

    /// <summary>Where the search looked.</summary>
    [AllowedValues(typeof(SlidesSearchScopes))]
    public required string Scope { get; init; }

    /// <summary>The returned matches in slide order; the window tells whether more exist.</summary>
    public required IReadOnlyList<SlidesSearchHit> Hits { get; init; }
}

/// <summary>One match in a shape's text or a slide's speaker notes.</summary>
public sealed record SlidesSearchHit
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }

    /// <summary>The stable slide id.</summary>
    [Minimum(1)]
    public required uint SlideId { get; init; }

    /// <summary>Whether the match is in a shape's text or in the speaker notes.</summary>
    [AllowedValues("shapes", "notes")]
    public required string Scope { get; init; }

    /// <summary>A positive identifier persisted within the hit slide; omitted for a match in the notes.</summary>
    [Minimum(1)]
    public long? ShapeId { get; init; }

    /// <summary>The name of the matched shape; omitted for the notes and for a shape without a name.</summary>
    public string? ShapeName { get; init; }

    /// <summary>The match itself.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The shape's or the notes' text around the match, up to 40 characters on each side, with
    /// line breaks as spaces and an ellipsis where it is cut.
    /// </summary>
    public required string Context { get; init; }
}
