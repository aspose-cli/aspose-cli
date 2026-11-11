using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides search</c>: the matches in the slides' text, notes, comments, layouts and masters.</summary>
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

/// <summary>One match in a slide's text: a shape's, its notes', a comment's, or its layout's or master's.</summary>
public sealed record SlidesSearchHit
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }

    /// <summary>The stable slide id.</summary>
    [Minimum(1)]
    public required uint SlideId { get; init; }

    /// <summary>
    /// Where the match is: in a shape (its text, alternative text or chart text), in the speaker
    /// notes, in a comment, or outside the placeholders of the layout or master the slide shows.
    /// </summary>
    [AllowedValues("shapes", "notes", "comments", "layouts", "masters")]
    public required string Scope { get; init; }

    /// <summary>A positive identifier persisted within the hit slide, of its own shape that holds the match; omitted outside a shape.</summary>
    [Minimum(1)]
    public long? ShapeId { get; init; }

    /// <summary>The name of the matched shape; omitted outside a shape and for a shape without a name.</summary>
    public string? ShapeName { get; init; }

    /// <summary>The match itself.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The text around the match, up to 40 characters on each side, with
    /// line breaks as spaces and an ellipsis where it is cut.
    /// </summary>
    public required string Context { get; init; }
}
