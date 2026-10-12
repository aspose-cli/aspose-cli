using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides query slides</c>: a window of slides with their content at the requested scope.</summary>
public sealed record SlidesReadResult() : WindowedResultEnvelope("read-result", 2)
{
    /// <summary>The presentation read.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>
    /// How much of each slide was projected: <c>text</c> the text blocks, <c>shapes</c> every
    /// shape with its text and geometry, <c>full</c> also text runs, notes and comments.
    /// </summary>
    [AllowedValues(typeof(SlidesReadScopes))]
    public required string Scope { get; init; }

    /// <summary>How many slides the presentation holds, which open slide ranges resolve against.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }

    /// <summary>The returned slides in presentation order; the window tells whether more were selected.</summary>
    public required IReadOnlyList<SlidesSlideData> Slides { get; init; }
}

/// <summary>The content of one slide.</summary>
public sealed record SlidesSlideData
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
    public required IReadOnlyList<SlidesShapeData> Shapes { get; init; }

    /// <summary>The speaker notes; at scope <c>full</c> or with <c>--notes</c>, and omitted when the slide has none.</summary>
    public string? Notes { get; init; }

    /// <summary>The slide's comments; only at scope <c>full</c>.</summary>
    public IReadOnlyList<SlidesCommentData>? Comments { get; init; }

    /// <summary>
    /// Whether the shared max-chars budget cut this slide's content. The budget covers title,
    /// text, runs, requested notes and comments, including repeated projections; re-read this
    /// slide with a larger budget for the omitted content.
    /// </summary>
    public required bool ContentTruncated { get; init; }
}

/// <summary>One top-level shape of a slide.</summary>
public sealed record SlidesShapeData
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
    public IReadOnlyList<SlidesTextRunData>? Runs { get; init; }

    /// <summary>Where the shape is on the slide.</summary>
    public required SlidesRect Rect { get; init; }

    // Product-internal review facts. These deliberately remain outside the
    // frozen wire contract while preserving the shape order and effective fill
    // semantics needed for conservative deterministic overlap checks.
    internal int ZOrder { get; init; }
    internal bool HasOpaqueFill { get; init; }

    /// <summary>Where the shape's text is laid out, in slide points; null for a shape without text.</summary>
    internal SlidesRect? TextRect { get; init; }

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
public sealed record SlidesTextRunData
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

    /// <summary>The solid color the text is drawn in, stated or inherited, as #RRGGBB; omitted for text without a solid fill.</summary>
    [Pattern("^#[0-9A-F]{6}$")]
    public string? Color { get; init; }
}

/// <summary>A slide rectangle in points from the slide's top-left corner.</summary>
public sealed record SlidesRect
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
public sealed record SlidesCommentData
{
    /// <summary>The name of the comment's author.</summary>
    public required string Author { get; init; }

    /// <summary>The comment's text, cut short when the max-chars budget runs out.</summary>
    public required string Text { get; init; }
}
