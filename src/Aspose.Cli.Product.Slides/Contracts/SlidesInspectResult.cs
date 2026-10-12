using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>
/// Result of <c>slides inspect</c>: the presentation's structure, its slides with their stable
/// ids, and the detail inventories <c>--detail</c> asked for.
/// </summary>
public sealed record SlidesInspectResult() : EngineResultEnvelope("inspect-result", 2)
{
    /// <summary>The inspected presentation.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>Presentation-level counts and slide geometry.</summary>
    public required SlidesPresentationSummary Presentation { get; init; }

    /// <summary>Every slide in presentation order.</summary>
    public required IReadOnlyList<SlidesSlideInfo> Slides { get; init; }

    /// <summary>The named sections in presentation order; only with <c>--detail sections</c>.</summary>
    public IReadOnlyList<SlidesSectionInfo>? Sections { get; init; }

    /// <summary>The slide masters ordered by name; only with <c>--detail masters</c>.</summary>
    public IReadOnlyList<SlidesMasterInfo>? Masters { get; init; }

    /// <summary>The layouts ordered by name; only with <c>--detail layouts</c>.</summary>
    public IReadOnlyList<SlidesLayoutInfo>? Layouts { get; init; }

    /// <summary>
    /// The embedded images, then audio, then video, at most the first 100; only with
    /// <c>--detail media</c>. A <c>LIST_TRUNCATED</c> warning discloses a longer list, which
    /// <c>slides extract --what media</c> exports in full.
    /// </summary>
    public IReadOnlyList<SlidesMediaInfo>? Media { get; init; }

    /// <summary>Speaker-note presence and size for every slide; only with <c>--detail notes</c>.</summary>
    public IReadOnlyList<SlidesNotesInfo>? Notes { get; init; }

    /// <summary>Every comment ordered by slide, then author; only with <c>--detail comments</c>.</summary>
    public IReadOnlyList<SlidesCommentInfo>? Comments { get; init; }

    /// <summary>The distinct font names the presentation uses, in ordinal order; only with <c>--detail fonts</c>.</summary>
    [UniqueItems]
    public IReadOnlyList<string>? Fonts { get; init; }

    /// <summary>
    /// The document properties <c>author</c>, <c>company</c>, <c>keywords</c>, <c>subject</c> and
    /// <c>title</c>, each null when empty; only with <c>--detail properties</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Properties { get; init; }
}

/// <summary>Presentation-level counts and slide geometry.</summary>
public sealed record SlidesPresentationSummary
{
    /// <summary>How many slides the presentation holds.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }

    /// <summary>The slide width in points.</summary>
    [Minimum(0)]
    public required double WidthPoints { get; init; }

    /// <summary>The slide height in points.</summary>
    [Minimum(0)]
    public required double HeightPoints { get; init; }

    /// <summary>Whether slides are wider than tall, taller than wide, or square.</summary>
    [AllowedValues("landscape", "portrait", "square")]
    public required string Orientation { get; init; }

    /// <summary>How many slide masters the presentation holds.</summary>
    [Minimum(0)]
    public required int MasterCount { get; init; }

    /// <summary>How many layouts the presentation holds.</summary>
    [Minimum(0)]
    public required int LayoutCount { get; init; }

    /// <summary>How many named sections the presentation holds.</summary>
    [Minimum(0)]
    public required int SectionCount { get; init; }

    /// <summary>How many comments the presentation holds.</summary>
    [Minimum(0)]
    public required int CommentCount { get; init; }

    /// <summary>How many images, audio and video items the presentation embeds.</summary>
    [Minimum(0)]
    public required int MediaCount { get; init; }

    /// <summary>Whether the presentation carries a VBA macro project.</summary>
    public required bool HasMacros { get; init; }
}

/// <summary>One slide in a structural presentation inventory.</summary>
public sealed record SlidesSlideInfo
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }

    /// <summary>The stable slide id, which edit operations accept as slideId.</summary>
    [Minimum(1)]
    public required uint SlideId { get; init; }

    /// <summary>The slide's name; omitted when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>The name of the slide's layout; omitted when it has none.</summary>
    public string? Layout { get; init; }

    /// <summary>The slide's title text; only with <c>--sample</c>, and omitted when the slide has no title.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// The slide's shape text joined by <c> · </c>, at most 240 characters; only with
    /// <c>--sample</c>, and omitted when the slide has no text.
    /// </summary>
    [MaxLength(PreviewTextLength)]
    public string? PreviewText { get; init; }

    /// <summary>The most characters <see cref="PreviewText"/> holds.</summary>
    public const int PreviewTextLength = 240;

    /// <summary>How many top-level shapes the slide holds.</summary>
    [Minimum(0)]
    public required int ShapeCount { get; init; }

    /// <summary>Whether the slide is hidden in a slide show.</summary>
    public required bool Hidden { get; init; }

    /// <summary>Whether the slide has speaker notes with text.</summary>
    public required bool HasNotes { get; init; }

    /// <summary>How many comments the slide holds.</summary>
    [Minimum(0)]
    public required int CommentCount { get; init; }
}

/// <summary>One slide master and its direct slide usage.</summary>
public sealed record SlidesMasterInfo
{
    /// <summary>The master's name.</summary>
    public required string Name { get; init; }

    /// <summary>How many slides use one of this master's layouts.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }
}

/// <summary>One layout and its direct slide usage.</summary>
public sealed record SlidesLayoutInfo
{
    /// <summary>The layout's name, which add_slide and apply_layout accept as layout.</summary>
    public required string Name { get; init; }

    /// <summary>The name of the master the layout belongs to; omitted when it has none.</summary>
    public required string? Master { get; init; }

    /// <summary>How many slides use this layout.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }
}

/// <summary>One embedded media item.</summary>
public sealed record SlidesMediaInfo
{
    /// <summary>
    /// The 1-based position among all images, then audio, then video, which
    /// <c>slides extract --what media</c> uses in its file names.
    /// </summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The kind of media.</summary>
    [AllowedValues("image", "audio", "video")]
    public required string Type { get; init; }

    /// <summary>The MIME type the presentation states for the item; omitted when it states none.</summary>
    public string? ContentType { get; init; }

    /// <summary>The item's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }
}

/// <summary>Speaker-note presence and size for one slide.</summary>
public sealed record SlidesNotesInfo
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }

    /// <summary>Whether the slide has speaker notes with text.</summary>
    public required bool Present { get; init; }

    /// <summary>How many characters the speaker notes hold; 0 without notes.</summary>
    [Minimum(0)]
    public required int CharacterCount { get; init; }
}

/// <summary>One presentation comment.</summary>
public sealed record SlidesCommentInfo
{
    /// <summary>The 1-based number of the slide the comment is on.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }

    /// <summary>The name of the comment's author.</summary>
    public required string Author { get; init; }

    /// <summary>The comment's text.</summary>
    public required string Text { get; init; }
}

/// <summary>One named presentation section.</summary>
public sealed record SlidesSectionInfo
{
    /// <summary>The section's name.</summary>
    public required string Name { get; init; }

    /// <summary>The section's stable id, a GUID.</summary>
    [Pattern("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    public required string SectionId { get; init; }

    /// <summary>The 1-based number of the section's first slide; 0 when the section holds no slides.</summary>
    [Minimum(0)]
    public required int StartSlide { get; init; }
}
