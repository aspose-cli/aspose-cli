using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>
/// Result of <c>slides inspect</c>: the presentation's structure, its slides with their stable
/// ids, and the detail inventories <c>--detail</c> asked for.
/// </summary>
public sealed record PresentationInfoResult() : EngineResultEnvelope("presentation-info", 2)
{
    /// <summary>The kind of document inspected, always <c>presentation</c>.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "presentation";

    /// <summary>The inspected presentation.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    /// <summary>Presentation-level counts and slide geometry.</summary>
    public required PresentationSummary Presentation { get; init; }

    /// <summary>Every slide in presentation order.</summary>
    public required IReadOnlyList<SlideInfo> Slides { get; init; }

    /// <summary>The named sections in presentation order; only with <c>--detail sections</c>.</summary>
    public IReadOnlyList<PresentationSectionInfo>? Sections { get; init; }

    /// <summary>The slide masters ordered by name; only with <c>--detail masters</c>.</summary>
    public IReadOnlyList<PresentationMasterInfo>? Masters { get; init; }

    /// <summary>The layouts ordered by name; only with <c>--detail layouts</c>.</summary>
    public IReadOnlyList<PresentationLayoutInfo>? Layouts { get; init; }

    /// <summary>
    /// The embedded images, then audio, then video, at most the first 100; only with
    /// <c>--detail media</c>. A <c>LIST_TRUNCATED</c> warning discloses a longer list, which
    /// <c>slides extract --what media</c> exports in full.
    /// </summary>
    public IReadOnlyList<PresentationMediaInfo>? Media { get; init; }

    /// <summary>Speaker-note presence and size for every slide; only with <c>--detail notes</c>.</summary>
    public IReadOnlyList<PresentationNotesInfo>? Notes { get; init; }

    /// <summary>Every comment ordered by slide, then author; only with <c>--detail comments</c>.</summary>
    public IReadOnlyList<PresentationCommentInfo>? Comments { get; init; }

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
public sealed record PresentationSummary
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
public sealed record SlideInfo
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

    /// <summary>The slide's title text; only with <c>--preview</c>, and omitted when the slide has no title.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// The slide's shape text joined by <c> · </c>, at most 240 characters; only with
    /// <c>--preview</c>, and omitted when the slide has no text.
    /// </summary>
    [MaxLength(240)]
    public string? PreviewText { get; init; }

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
public sealed record PresentationMasterInfo
{
    /// <summary>The master's name.</summary>
    public required string Name { get; init; }

    /// <summary>How many slides use one of this master's layouts.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }
}

/// <summary>One layout and its direct slide usage.</summary>
public sealed record PresentationLayoutInfo
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
public sealed record PresentationMediaInfo
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
public sealed record PresentationNotesInfo
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
public sealed record PresentationCommentInfo
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
public sealed record PresentationSectionInfo
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

/// <summary>Result of <c>slides query slides</c>: a window of slides with their content at the requested scope.</summary>
public sealed record PresentationReadResult() : WindowedResultEnvelope("presentation-read", 2)
{
    /// <summary>The kind of document read, always <c>presentation</c>.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "presentation";

    /// <summary>The presentation read.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    /// <summary>
    /// How much of each slide was projected: <c>text</c> the text blocks, <c>shapes</c> every
    /// shape with its text and geometry, <c>full</c> also text runs, notes and comments.
    /// </summary>
    [AllowedValues(typeof(PresentationReadScopes))]
    public required string Scope { get; init; }

    /// <summary>How many slides the presentation holds, which open slide ranges resolve against.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }

    /// <summary>The returned slides in presentation order; the window tells whether more were selected.</summary>
    public required IReadOnlyList<SlideData> Slides { get; init; }
}

/// <summary>The content of one slide.</summary>
public sealed record SlideData
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

    /// <summary>The slide's title text; omitted when the slide has no title.</summary>
    public string? Title { get; init; }

    /// <summary>The text of each shape that has text, in shape order; only at scope <c>text</c>.</summary>
    public IReadOnlyList<string>? Text { get; init; }

    /// <summary>The slide's top-level shapes in z-order; empty at scope <c>text</c>.</summary>
    public required IReadOnlyList<SlideShapeData> Shapes { get; init; }

    /// <summary>The speaker notes; at scope <c>full</c> or with <c>--notes</c>, and omitted when the slide has none.</summary>
    public string? Notes { get; init; }

    /// <summary>The slide's comments; only at scope <c>full</c>.</summary>
    public IReadOnlyList<SlideCommentData>? Comments { get; init; }

    /// <summary>
    /// Whether the shared max-chars budget cut this slide's content. The budget covers title,
    /// text, runs, requested notes and comments, including repeated projections; re-read this
    /// slide with a larger budget for the omitted content.
    /// </summary>
    public required bool ContentTruncated { get; init; }
}

/// <summary>One top-level shape of a slide.</summary>
public sealed record SlideShapeData
{
    /// <summary>A positive identifier persisted within its slide; edit operations accept it as shapeId.</summary>
    [Minimum(1)]
    public required long ShapeId { get; init; }

    /// <summary>The shape's name, which edit operations accept as shapeName; omitted when it has none.</summary>
    public string? ShapeName { get; init; }

    /// <summary>
    /// What the shape is: <c>placeholder</c> for any placeholder, then <c>chart</c>,
    /// <c>table</c>, <c>audio</c>, <c>video</c>, <c>image</c>, <c>group</c>, or <c>shape</c> for
    /// any other shape.
    /// </summary>
    [AllowedValues("placeholder", "chart", "table", "audio", "video", "image", "group", "shape")]
    public required string Type { get; init; }

    /// <summary>
    /// The placeholder role, which set_text and the other shape operations accept as placeholder
    /// when it is one they name; omitted for a shape that is not a placeholder. Roles outside
    /// the listed ones are the engine's placeholder type in lower case.
    /// </summary>
    [AllowedValues("title", "body", "subtitle", "footer", "date", "slide-number")]
    [OpenEnum("^[a-z]+$")]
    public string? Placeholder { get; init; }

    /// <summary>The shape's alternative text, which screen readers and exported PDFs carry.</summary>
    public string? AltText { get; init; }

    /// <summary>The shape's text; omitted when it has none.</summary>
    public string? Text { get; init; }

    /// <summary>The shape's text runs with their effective formatting; only at scope <c>full</c>, and omitted for a shape without text.</summary>
    public IReadOnlyList<SlideTextRunData>? Runs { get; init; }

    /// <summary>Where the shape is on the slide.</summary>
    public required SlideRect Rect { get; init; }

    // Product-internal review facts. These deliberately remain outside the
    // frozen wire contract while preserving the shape order and effective fill
    // semantics needed for conservative deterministic overlap checks.
    internal int ZOrder { get; init; }
    internal bool HasOpaqueFill { get; init; }

    /// <summary>Where the shape's text is laid out, in slide points; null for a shape without text.</summary>
    internal SlideRect? TextRect { get; init; }

    /// <summary>Whether the shape resizes to fit its text or shrinks its text on overflow, so text beyond its stored frame is not an overflow.</summary>
    internal bool TextAutofits { get; init; }

    /// <summary>The one solid color behind the text of a text shape or chart; null when no single color is known.</summary>
    internal System.Drawing.Color? Backdrop { get; init; }

    /// <summary>The color a chart states for its text; null when the chart style decides it.</summary>
    internal System.Drawing.Color? ChartTextColor { get; init; }

    /// <summary>Whether an evaluation-mode read found the shape to be the watermark text box an evaluation save added.</summary>
    internal bool EvaluationWatermark { get; init; }
}

/// <summary>One text run with its effective formatting.</summary>
public sealed record SlideTextRunData
{
    /// <summary>The run's text.</summary>
    public required string Text { get; init; }

    /// <summary>The font of the run's Latin text.</summary>
    public string? Font { get; init; }

    /// <summary>The font of the run's Chinese, Japanese and Korean text.</summary>
    public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    public double? Size { get; init; }

    /// <summary>Whether the run is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Whether the run is italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>The solid color the text is drawn in, stated or inherited, as #RRGGBB; null for text without a solid fill.</summary>
    [Pattern("^#[0-9A-F]{6}$")]
    public string? Color { get; init; }
}

/// <summary>A slide rectangle in points from the slide's top-left corner.</summary>
public sealed record SlideRect
{
    /// <summary>The left edge in points.</summary>
    public required double X { get; init; }

    /// <summary>The top edge in points.</summary>
    public required double Y { get; init; }

    /// <summary>The width in points.</summary>
    public required double Width { get; init; }

    /// <summary>The height in points.</summary>
    public required double Height { get; init; }
}

/// <summary>One comment on a slide.</summary>
public sealed record SlideCommentData
{
    /// <summary>The name of the comment's author.</summary>
    public required string Author { get; init; }

    /// <summary>The comment's text, cut short when the max-chars budget runs out.</summary>
    public required string Text { get; init; }
}

/// <summary>
/// Result of <c>slides convert</c>: the files a presentation or its selected slides were
/// converted to. PNG and JPEG use the default 192 DPI render geometry and pixel budgets.
/// </summary>
public sealed record SlidesConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The written files: one per slide for an image format, otherwise one, with any companion files the format needs.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<OutputInfo> Outputs { get; init; }

    /// <summary>The <c>--slides</c> selection that was converted, normalized; omitted when every slide was.</summary>
    [PageRange]
    public string? Slides { get; init; }
}

/// <summary>Result of <c>slides render</c>: one image per rendered slide.</summary>
public sealed record SlidesRenderResult() : EngineResultEnvelope("render-result", 2)
{
    /// <summary>The rendered presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The rendered slides in presentation order.</summary>
    [MinItems(1)]
    public required IReadOnlyList<SlideRenderOutput> Outputs { get; init; }

    /// <summary>The raster resolution used; omitted for SVG and when <c>--width</c> set the size.</summary>
    [Minimum(RenderPixelGuard.MinimumDpi)]
    [Maximum(RenderPixelGuard.MaximumDpi)]
    public int? Dpi { get; init; }

    /// <summary>The exact raster width in pixels <c>--width</c> asked for; omitted otherwise.</summary>
    [Minimum(64)]
    [Maximum(20_000)]
    public int? Width { get; init; }
}

/// <summary>One rendered slide image.</summary>
public sealed record SlideRenderOutput
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }

    /// <summary>The stable slide id.</summary>
    [Minimum(1)]
    public required uint SlideId { get; init; }

    /// <summary>The image written for the slide.</summary>
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of <c>slides create</c>: the new presentation and what it was built from.</summary>
public sealed record SlidesCreateResult() : EngineResultEnvelope("create-result", 2)
{
    /// <summary>The created presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required OutputInfo Output { get; init; }

    /// <summary>How many slides the created presentation holds.</summary>
    [Minimum(1)]
    public required int SlideCount { get; init; }

    /// <summary>The template the presentation was built from; omitted without <c>--template</c>.</summary>
    public SourceInfo? Template { get; init; }

    /// <summary>The Markdown outline the slides were filled from; omitted without <c>--from-markdown</c>.</summary>
    public SourceInfo? Markdown { get; init; }
}

/// <summary>Result of <c>slides extract</c>: the files written for the extracted content.</summary>
public sealed record SlidesExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The presentation extracted from.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>What was extracted.</summary>
    [AllowedValues(typeof(PresentationExtractKinds))]
    public required string What { get; init; }

    /// <summary>The written files: media in presentation order, or one text file per slide that has text.</summary>
    public required IReadOnlyList<SlidesExtractedItem> Items { get; init; }
}

/// <summary>One extracted file.</summary>
public sealed record SlidesExtractedItem
{
    /// <summary>Absolute path of the written file.</summary>
    public required string Path { get; init; }

    /// <summary>What the file holds: a media kind, or the speaker notes or text of one slide.</summary>
    [AllowedValues("image", "audio", "video", "notes", "text")]
    public required string Kind { get; init; }

    /// <summary>The file's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>The 1-based slide number of slide text or notes; omitted for media.</summary>
    [Minimum(1)]
    public int? Slide { get; init; }

    /// <summary>The stable slide id of slide text or notes; omitted for media.</summary>
    [Minimum(1)]
    public uint? SlideId { get; init; }

    /// <summary>
    /// The media item's 1-based position among all images, then audio, then video, as inspect
    /// reports it; omitted for text and notes.
    /// </summary>
    [Minimum(1)]
    public int? Index { get; init; }

    /// <summary>The slide's name for slide text or notes; omitted when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>The MIME type of the file; omitted when the presentation states none for a media item.</summary>
    public string? ContentType { get; init; }
}

/// <summary>Result of <c>slides edit</c>: one atomic operation batch, applied or checked.</summary>
public sealed record SlidesEditResult() : EngineResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The edited presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The written presentation; omitted for a dry run.</summary>
    public OutputInfo? Output { get; init; }

    /// <summary>Whether the batch was only checked, without writing anything.</summary>
    public required bool DryRun { get; init; }

    /// <summary>The outcome of every operation, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>The backup of the replaced presentation; only for an in-place edit that kept one.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>The stable ids of the slides the batch changed, ascending; omitted when it changed none.</summary>
    [UniqueItems]
    [Minimum(1)]
    public IReadOnlyList<uint>? SlidesTouched { get; init; }

    /// <summary>Whether an operation failed.</summary>
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static op => op.Status == "failed");
}

/// <summary>Result of <c>slides search</c>: the matches in shape text and speaker notes.</summary>
public sealed record SlidesSearchResult() : WindowedResultEnvelope("search-result", 2)
{
    /// <summary>The searched presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Source { get; init; }

    /// <summary>The searched pattern as given.</summary>
    public required string Pattern { get; init; }

    /// <summary>Where the search looked.</summary>
    [AllowedValues(typeof(PresentationSearchScopes))]
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

    /// <summary>The match with up to 100 characters of context on each side.</summary>
    public required string Text { get; init; }

    /// <summary>Where the match starts in the shape's or the notes' text, as a 0-based character offset.</summary>
    [Minimum(0)]
    public required int Start { get; init; }

    /// <summary>How many characters the match spans.</summary>
    [Minimum(1)]
    public required int Length { get; init; }
}
