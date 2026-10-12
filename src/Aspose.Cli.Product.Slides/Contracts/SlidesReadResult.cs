using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides read</c>: a window of slides with their content at the requested depth.</summary>
public sealed record SlidesReadResult() : WindowedResultEnvelope("read-result", 2)
{
    /// <summary>The presentation read.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>
    /// How much of each slide was projected: <c>text</c> the text blocks, <c>shapes</c> every
    /// shape with its text and geometry, <c>full</c> also text runs, notes and comments.
    /// </summary>
    [AllowedValues(typeof(SlidesReadContents))]
    public required string Content { get; init; }

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

    /// <summary>The text of each shape that has text, in shape order; only with <c>--content text</c>.</summary>
    public IReadOnlyList<string>? Text { get; init; }

    /// <summary>The slide's top-level shapes in z-order; empty with <c>--content text</c>.</summary>
    public required IReadOnlyList<SlidesShapeData> Shapes { get; init; }

    /// <summary>The speaker notes; with <c>--content full</c> or <c>--notes</c>, and omitted when the slide has none.</summary>
    public SlidesNotesData? Notes { get; init; }

    /// <summary>The slide's comments; only with <c>--content full</c>.</summary>
    public IReadOnlyList<SlidesCommentData>? Comments { get; init; }

    /// <summary>The slide's own background; only with <c>--content full</c>, and omitted when the slide shows its layout's.</summary>
    public SlidesBackgroundData? Background { get; init; }

    /// <summary>
    /// The slide-show transition into the slide, kind <c>none</c> when it has none, as
    /// set_transition writes it; only with <c>--content full</c>.
    /// </summary>
    public SlidesTransitionData? Transition { get; init; }

    /// <summary>What the slide shows in its footer area; only with <c>--content full</c>.</summary>
    public SlidesFooterData? Footer { get; init; }

    /// <summary>
    /// Whether the shared max-chars budget cut this slide's content. The budget covers title,
    /// text, runs, paragraphs, table cells, chart labels, footer, requested notes and comments,
    /// including repeated projections; re-read this
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
    public required string Kind { get; init; }

    /// <summary>
    /// The placeholder role, which set_text and the other shape operations accept as placeholder
    /// when it is one they name; omitted for a shape that is not a placeholder. Roles outside
    /// the listed ones are the engine's placeholder type in lower case.
    /// </summary>
    [AllowedValues(typeof(SlidesPlaceholderRoles))]
    [OpenEnum("^[a-z]+$")]
    public string? Placeholder { get; init; }

    /// <summary>
    /// The geometry of an autoshape, which insert_shape accepts as geometry; omitted for any
    /// other shape and for a geometry no operation draws.
    /// </summary>
    [AllowedValues(typeof(SlidesShapeGeometries))]
    public string? Geometry { get; init; }

    /// <summary>The shape's alternative text, which screen readers and exported PDFs carry.</summary>
    public string? AltText { get; init; }

    /// <summary>The shape's text; omitted when it has none.</summary>
    public string? Text { get; init; }

    /// <summary>The shape's text runs with their effective formatting; only with <c>--content full</c>, and omitted for a shape without text.</summary>
    public IReadOnlyList<SlidesTextRunData>? Runs { get; init; }

    /// <summary>
    /// The paragraphs of a text shape with the list each one belongs to; only with
    /// <c>--content full</c>, and omitted for a shape without text of its own.
    /// </summary>
    public IReadOnlyList<SlidesParagraphData>? Paragraphs { get; init; }

    /// <summary>
    /// The solid fill color, as #RRGGBB: a table's when every cell has the same one; only with
    /// <c>--content full</c>, and omitted without one solid fill.
    /// </summary>
    [Pattern("^#[0-9A-F]{6}$")]
    public string? FillColor { get; init; }

    /// <summary>
    /// The solid outline color, as #RRGGBB: a table's when every cell border has the same one;
    /// only with <c>--content full</c>, and omitted without one solid outline.
    /// </summary>
    [Pattern("^#[0-9A-F]{6}$")]
    public string? Line { get; init; }

    /// <summary>How many rows a table has; only for a table with <c>--content full</c>.</summary>
    [Minimum(1)]
    public int? RowCount { get; init; }

    /// <summary>How many columns a table has; only for a table with <c>--content full</c>.</summary>
    [Minimum(1)]
    public int? ColumnCount { get; init; }

    /// <summary>A table's cell text by row; only for a table with <c>--content full</c>.</summary>
    public IReadOnlyList<IReadOnlyList<string>>? Values { get; init; }

    /// <summary>A chart's kind, title and data; only for a chart with <c>--content full</c>.</summary>
    public SlidesChartData? Chart { get; init; }

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

    /// <summary>
    /// The Latin font the run states itself, such as a theme font reference like <c>+mn-lt</c>;
    /// omitted when the run takes it from its paragraph, shape or theme.
    /// </summary>
    public string? LatinFont { get; init; }

    /// <summary>The font of the run's Chinese, Japanese and Korean text.</summary>
    public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    public double? FontSize { get; init; }

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

/// <summary>One paragraph of a shape's text.</summary>
public sealed record SlidesParagraphData
{
    /// <summary>The paragraph's text.</summary>
    public required string Text { get; init; }

    /// <summary>A list paragraph's list: bullets or numbers, and its 1-based level; omitted for a paragraph without either.</summary>
    public SlidesListData? List { get; init; }
}

/// <summary>The list a paragraph belongs to, as set_body sets it.</summary>
public sealed record SlidesListData
{
    /// <summary>Whether the paragraph shows a bullet or a number.</summary>
    [AllowedValues("bullet", "number")]
    public required string Kind { get; init; }

    /// <summary>The paragraph's 1-based level in the list.</summary>
    [Minimum(1)]
    public required int Level { get; init; }
}

/// <summary>A chart's kind, title and data.</summary>
public sealed record SlidesChartData
{
    /// <summary>The kind of chart; omitted for a kind no operation creates.</summary>
    [AllowedValues(typeof(SlidesChartKinds))]
    public string? Kind { get; init; }

    /// <summary>The chart's title text; omitted when it shows none of its own.</summary>
    public string? Title { get; init; }

    /// <summary>The category labels, in order.</summary>
    public required IReadOnlyList<string> Categories { get; init; }

    /// <summary>The series, in order.</summary>
    public required IReadOnlyList<SlidesChartSeriesData> Series { get; init; }
}

/// <summary>One chart series.</summary>
public sealed record SlidesChartSeriesData
{
    /// <summary>The series name.</summary>
    public required string Name { get; init; }

    /// <summary>The value of each point, in category order.</summary>
    public required IReadOnlyList<double> Values { get; init; }

    /// <summary>The x value of each point of a scatter series; omitted for other series.</summary>
    public IReadOnlyList<double>? XValues { get; init; }
}

/// <summary>A slide's own background.</summary>
public sealed record SlidesBackgroundData
{
    /// <summary>The solid background color, as #RRGGBB; omitted for a picture or other fill.</summary>
    [Pattern("^#[0-9A-F]{6}$")]
    public string? Color { get; init; }
}

/// <summary>The slide-show transition into a slide.</summary>
public sealed record SlidesTransitionData
{
    /// <summary>The transition; omitted for a kind no operation sets.</summary>
    [AllowedValues(typeof(SlidesTransitionKinds))]
    public string? Kind { get; init; }

    /// <summary>The transition duration in milliseconds; omitted when the slide states none.</summary>
    [Minimum(0)]
    public int? DurationMs { get; init; }
}

/// <summary>What a slide shows in its footer area.</summary>
public sealed record SlidesFooterData
{
    /// <summary>The text of the slide's footer placeholder; omitted when it shows none.</summary>
    public string? Text { get; init; }

    /// <summary>Whether the slide shows its number.</summary>
    public required bool ShowNumber { get; init; }

    /// <summary>Whether the slide shows the date.</summary>
    public required bool ShowDate { get; init; }
}

/// <summary>A slide's speaker notes.</summary>
public sealed record SlidesNotesData
{
    /// <summary>The notes' text, cut short when the max-chars budget runs out.</summary>
    public required string Text { get; init; }
}
