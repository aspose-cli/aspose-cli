using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Sdk.Operations.OperationInvalidException;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>A validated, atomic presentation edit batch.</summary>
[ProductJsonRoot]
public sealed record SlidesOpsBatch : BoundedOperationEnvelope<SlidesOp>;

/// <summary>
/// The operation document of <c>slides edit --ops</c>. Each operation's slide and shape targets
/// resolve against the presentation as the earlier operations left it: a slide inserted earlier
/// in the batch can be targeted by its new number, and slide numbers count the slides that
/// earlier moves and deletions left.
/// </summary>
[OperationVocabulary("ops", JsonContext = typeof(SlidesOpsJsonContext), Addresses = new[] { "slide", "slideId", "slides", "shapeId", "shapeName", "placeholder", "at", "path" })]
[JsonConverter(typeof(OperationJsonConverter<SlidesOp>))]
public abstract partial record SlidesOp : BoundedOperation;

/// <summary>An operation on one slide, named by its 1-based number or its stable id.</summary>
[ExactlyOneOf("slide", "slideId")]
public abstract record SlideTargetOp : SlidesOp
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)] public int? Slide { get; init; }

    /// <summary>The stable slide id reported by <c>slides inspect</c> and <c>slides read</c>.</summary>
    [Minimum(1)] public uint? SlideId { get; init; }
}

/// <summary>An operation on one top-level shape of a slide, named by its id, its name or its placeholder role.</summary>
[ExactlyOneOf("shapeId", "shapeName", "placeholder")]
public abstract record ShapeTargetOp : SlideTargetOp
{
    /// <summary>The persistent slide-scoped shapeId returned by <c>slides read</c>; not a shape position.</summary>
    [Minimum(1)] public long? ShapeId { get; init; }

    /// <summary>The shape name, matched case-sensitively.</summary>
    [Pattern(@"\S")] public string? ShapeName { get; init; }

    /// <summary>The placeholder role, as <c>slides read</c> reports it.</summary>
    [AllowedValues(typeof(SlidesPlaceholderRoles))] public string? Placeholder { get; init; }
}

/// <summary>A rectangle in presentation points from the top-left origin.</summary>
public sealed record SlidesRectInput
{
    [Minimum(0)] public required double X { get; init; }

    [Minimum(0)] public required double Y { get; init; }

    [ExclusiveMinimum(0)] public required double Width { get; init; }

    [ExclusiveMinimum(0)] public required double Height { get; init; }
}

/// <summary>
/// The size of the slides in points (72 per inch): a preset, or both the width and the height,
/// each 72 through 7200 points.
/// </summary>
[ExactlyOneOf("preset", "width")]
[DependentRequired("width", "height")]
[DependentRequired("height", "width")]
public sealed record SlidesSlideSizeInput
{
    /// <summary>The preset; omitted when the width and height are given.</summary>
    [AllowedValues(typeof(SlidesSlideSizePresets))] public string? Preset { get; init; }

    /// <summary>The slide width in points, given with the height instead of a preset.</summary>
    [Minimum(SlidesSlideSizePresets.MinimumSide), Maximum(SlidesSlideSizePresets.MaximumSide)] public double? Width { get; init; }

    /// <summary>The slide height in points, given with the width instead of a preset.</summary>
    [Minimum(SlidesSlideSizePresets.MinimumSide), Maximum(SlidesSlideSizePresets.MaximumSide)] public double? Height { get; init; }
}

/// <summary>One body paragraph; its spacing comes from the placeholder's level.</summary>
public sealed record SlidesParagraphInput
{
    [ReadBack("read-result.schema.json#/$defs/slidesParagraphData/properties/text")]
    public required string Text { get; init; }

    /// <summary>
    /// Makes the paragraph a list item at the list's 1-based level, with the bullet the
    /// placeholder defines for that level (a bullet of its own where the level defines none), or
    /// numbered; without it the paragraph is plain text at the top level.
    /// </summary>
    [MistakenFor("bullet", "level", "listLevel")]
    public SlidesListInput? List { get; init; }
}

/// <summary>The list a paragraph belongs to, as a read reports it.</summary>
public sealed record SlidesListInput
{
    /// <summary>Whether the item shows its level's bullet or a number; the bullet when omitted.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesListData/properties/kind")]
    [AllowedValues("bullet", "number")] public string? Kind { get; init; }

    /// <summary>The paragraph's 1-based level in the list.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesListData/properties/level")]
    [Minimum(1), Maximum(9)] public required int Level { get; init; }
}

/// <summary>Visual styling of a slide shape and its text; it must set at least one property.</summary>
[MinProperties(1)]
public sealed record SlidesShapeStyleInput
{
    /// <summary>The solid fill color of the shape.</summary>
    [MistakenFor("fill")]
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/fillColor")]
    [HexColor] public string? FillColor { get; init; }

    /// <summary>The solid outline color of the shape.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/line")]
    [HexColor] public string? Line { get; init; }

    /// <summary>A font name for Latin, East Asian and complex-script text.</summary>
    [Pattern(@"^[^+]", Meaning = "must be a font name; give theme fonts such as \"+mn-lt\" in latinFont and eastAsianFont")]
    [ReadBack("read-result.schema.json#/$defs/slidesTextRunData/properties/font")]
    public string? Font { get; init; }

    /// <summary>
    /// The font of Latin text, in place of font's: a font name, or the theme's heading font
    /// "+mj-lt" or body font "+mn-lt", which returns the text to the template's font.
    /// </summary>
    [Pattern(@"^(?:\+m[jn]-lt$|[^+])", Meaning = "must be a font name, or \"+mj-lt\" or \"+mn-lt\" for the theme's Latin heading or body font")]
    [ReadBack("read-result.schema.json#/$defs/slidesTextRunData/properties/latinFont")]
    public string? LatinFont { get; init; }

    /// <summary>
    /// The font of Chinese, Japanese and Korean text, in place of font's: a font name, or the
    /// theme's heading font "+mj-ea" or body font "+mn-ea", which returns the text to the
    /// template's font.
    /// </summary>
    [Pattern(@"^(?:\+m[jn]-ea$|[^+])", Meaning = "must be a font name, or \"+mj-ea\" or \"+mn-ea\" for the theme's East Asian heading or body font")]
    [ReadBack("read-result.schema.json#/$defs/slidesTextRunData/properties/eastAsianFont")]
    public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    [MistakenFor("size")]
    [ReadBack("read-result.schema.json#/$defs/slidesTextRunData/properties/fontSize")]
    [ExclusiveMinimum(0), Maximum(400)] public double? FontSize { get; init; }

    /// <summary>The text color.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesTextRunData/properties/color")]
    [HexColor] public string? Color { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesTextRunData/properties/bold")]
    public bool? Bold { get; init; }
}

/// <summary>One chart series.</summary>
public sealed record SlidesChartSeriesInput
{
    public required string Name { get; init; }

    [MinItems(1), MaxItems(1000)] public required IReadOnlyList<double> Values { get; init; }

    /// <summary>The x value of each point of a scatter series, one per value.</summary>
    [MinItems(1)] public IReadOnlyList<double>? XValues { get; init; }

    /// <summary>Checks that every series has one value per category when both are given.</summary>
    internal static void RequireOneValuePerCategory(IReadOnlyList<string>? categories, IReadOnlyList<SlidesChartSeriesInput>? series) =>
        Require(categories is null || series is null || series.All(item => item.Values.Count == categories.Count),
            "each series must have one value per category");
}

/// <summary>Inserts an empty slide.</summary>
[Operation("insert_slide")]
[MistakenFor("add_slide")]
public sealed record InsertSlideOp : SlidesOp
{
    /// <summary>The layout name, matched case-insensitively; the first layout when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/slidesSlideInfo/properties/layout")]
    [Pattern(@"\S")] public string? Layout { get; init; }

    /// <summary>The 1-based position of the new slide, at most the slide count plus one; the end when omitted.</summary>
    [Minimum(1)] public int? At { get; init; }
}

/// <summary>Deletes slides; a presentation must keep at least one slide.</summary>
[Operation("delete_slides")]
public sealed record DeleteSlidesOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }
}

/// <summary>Moves one slide to a position; it keeps its slide id.</summary>
[Operation("move_slide")]
public sealed record MoveSlideOp : SlideTargetOp
{
    /// <summary>The 1-based position, at most the slide count.</summary>
    [NotState]
    [Minimum(1)] public required int To { get; init; }
}

/// <summary>Inserts a copy of one slide.</summary>
[Operation("copy_slide")]
[MistakenFor("duplicate_slide")]
public sealed record CopySlideOp : SlideTargetOp
{
    /// <summary>The 1-based position of the copy, at most the slide count plus one; the end when omitted.</summary>
    [Minimum(1)] public int? At { get; init; }
}

/// <summary>Hides or shows slides in a slide show.</summary>
[Operation("set_slide_hidden")]
public sealed record SetSlideHiddenOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/slidesSlideInfo/properties/hidden")]
    public required bool Hidden { get; init; }
}

/// <summary>Sets the layout of slides, which drop their own background to show the layout's.</summary>
[Operation("set_layout")]
[MistakenFor("apply_layout")]
public sealed record SetLayoutOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }

    /// <summary>The layout name, matched case-insensitively.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/slidesSlideInfo/properties/layout")]
    [Pattern(@"\S")] public required string Layout { get; init; }
}

/// <summary>Gives slides their own solid-color or stretched-picture background.</summary>
[Operation("set_background")]
[ExactlyOneOf("color", "path")]
public sealed record SetBackgroundOp : SlidesOp
{
    /// <summary>The slides to change; every slide when omitted.</summary>
    [PageRange] public string? Slides { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesBackgroundData/properties/color")]
    [HexColor] public string? Color { get; init; }

    /// <summary>The picture, relative to the working directory.</summary>
    [MistakenFor("imagePath")]
    [InputPath] public string? Path { get; init; }
}

/// <summary>Inserts a new section that starts at a slide; section names are unique.</summary>
[Operation("insert_section")]
[MistakenFor("add_section")]
public sealed record InsertSectionOp : SlidesOp
{
    [ReadBack("inspect-result.schema.json#/$defs/slidesSectionInfo/properties/name")]
    [Pattern(@"\S")] public required string Name { get; init; }

    /// <summary>The 1-based number of the section's first slide, at most the slide count.</summary>
    [MistakenFor("startSlide")]
    [Minimum(1)] public required int At { get; init; }
}

/// <summary>Inserts every slide of another presentation at the end.</summary>
[Operation("import_slides")]
[MistakenFor("append_presentation")]
public sealed record ImportSlidesOp : SlidesOp
{
    /// <summary>The source presentation, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>
    /// Whether imported slides keep their own masters (keep-source), or take the destination's
    /// first master and show its layouts' backgrounds instead of their own (use-destination).
    /// </summary>
    [MistakenFor("masterPolicy")]
    [NotState]
    [AllowedValues(typeof(FormattingModes))] public string Formatting { get; init; } = FormattingModes.KeepSource;

    /// <summary>The environment variable that holds the source presentation's open password, for an encrypted source.</summary>
    [MistakenFor("passwordEnv")]
    [NotState]
    [SecretEnv] public string? SourcePasswordEnv { get; init; }
}

/// <summary>Sets the text of a slide's title placeholder.</summary>
[Operation("set_title")]
public sealed record SetTitleOp : SlideTargetOp
{
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/text")]
    public required string Text { get; init; }
}

/// <summary>Replaces the paragraphs of a slide's body placeholder.</summary>
[Operation("set_body")]
public sealed record SetBodyOp : SlideTargetOp
{
    [MinItems(1)] public required IReadOnlyList<SlidesParagraphInput> Paragraphs { get; init; }
}

/// <summary>Replaces the text of one shape that has a text frame.</summary>
[Operation("set_text")]
public sealed record SetTextOp : ShapeTargetOp
{
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/text")]
    public required string Text { get; init; }
}

/// <summary>
/// Replaces text within each paragraph of every slide's shapes (including table cells, group
/// children and SmartArt nodes) and speaker notes. A regular expression runs with a one-second
/// timeout and must compile.
/// </summary>
[Operation("replace_text")]
public sealed record SlidesReplaceTextOp : SlidesOp
{
    [NotState]
    [MinLength(1)] public required string Find { get; init; }

    /// <summary>The replacement; with regex it honors .NET substitutions such as $1, and $$ is a literal $.</summary>
    [MistakenFor("replacement", "replaceWith")]
    [NotState]
    public required string Replace { get; init; }

    /// <summary>Whether find is a .NET regular expression rather than literal text.</summary>
    [NotState]
    public bool Regex { get; init; }

    /// <summary>Whether find matches letter case exactly.</summary>
    [MistakenFor("matchCase")]
    [NotState]
    public bool CaseSensitive { get; init; }

    /// <summary>Whether a match counts only when no letter, digit or underscore adjoins it on either side.</summary>
    [NotState]
    public bool WholeWord { get; init; }

    [NotState]
    [AllowedValues(typeof(SlidesSearchScopes))] public string Scope { get; init; } = SlidesSearchScopes.All;

    /// <summary>The most matches replaced, the first ones in slide and shape order; every match when omitted.</summary>
    [NotState]
    [Minimum(1)] public int? MaxReplacementCount { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Regex)
        {
            try
            {
                _ = SafeRegex.Create(Find, CaseSensitive);
            }
            catch (ArgumentException exception)
            {
                throw new OperationInvalidException($"find is not a valid regular expression: {exception.Message}");
            }
        }

        return this;
    }
}

/// <summary>Replaces a slide's speaker-note text.</summary>
[Operation("set_notes")]
public sealed record SetNotesOp : SlideTargetOp
{
    [ReadBack("read-result.schema.json#/$defs/slidesNotesData/properties/text")]
    public required string Text { get; init; }
}

/// <summary>Inserts a picture.</summary>
[Operation("insert_image")]
public sealed record SlidesInsertImageOp : SlideTargetOp
{
    /// <summary>The picture, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>Where the picture goes; when omitted it keeps its aspect ratio, centered in 80% by 75% of the slide.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/rect")]
    public SlidesRectInput? Rect { get; init; }

    /// <summary>The picture's alternative text, which screen readers and exported PDFs carry.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/altText")]
    public string? AltText { get; init; }
}

/// <summary>Inserts a shape.</summary>
[Operation("insert_shape")]
public sealed record InsertShapeOp : SlideTargetOp
{
    /// <summary>The shape's geometry.</summary>
    [MistakenFor("kind", "type", "shapeType")]
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/geometry")]
    [AllowedValues(typeof(SlidesShapeGeometries))] public required string Geometry { get; init; }

    [MistakenFor("box", "bounds")]
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/rect")]
    public required SlidesRectInput Rect { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/text")]
    public string? Text { get; init; }

    public SlidesShapeStyleInput? Style { get; init; }
}

/// <summary>Inserts a table with equal rows and columns; values fill it from the top-left cell.</summary>
[Operation("insert_table")]
public sealed record SlidesInsertTableOp : SlideTargetOp
{
    /// <summary>The most rows a slide table holds, shared by Markdown tables.</summary>
    internal const int MaxRows = 100;

    /// <summary>The most columns a slide table holds, shared by Markdown tables.</summary>
    internal const int MaxCols = 50;

    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/rect")]
    public required SlidesRectInput Rect { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/rowCount")]
    [Minimum(1), Maximum(MaxRows)] public required int RowCount { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/columnCount")]
    [Minimum(1), Maximum(MaxCols)] public required int ColumnCount { get; init; }

    /// <summary>Cell text by row, at most rowCount rows of at most columnCount cells each.</summary>
    [MistakenFor("data")]
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/values")]
    public IReadOnlyList<IReadOnlyList<string>>? Values { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        Require(Values is null || Values.Count <= RowCount && Values.All(row => row.Count <= ColumnCount),
            "values must fit the table: at most rowCount rows of at most columnCount cells");
        return this;
    }
}

/// <summary>Sets the text of one cell of a table shape.</summary>
[Operation("set_table_cell")]
public sealed record SlidesSetTableCellOp : ShapeTargetOp
{
    /// <summary>The 1-based row.</summary>
    [NotState]
    [Minimum(1)] public required int Row { get; init; }

    /// <summary>The 1-based column.</summary>
    [NotState]
    [Minimum(1)] public required int Column { get; init; }

    [ReadBackByPosition]
    public required string Text { get; init; }
}

/// <summary>
/// Inserts a chart. Every series has one value per category, and a scatter series has one x
/// value per value.
/// </summary>
[Operation("insert_chart")]
public sealed record InsertChartOp : SlideTargetOp
{
    [MistakenFor("type", "chartType")]
    [ReadBack("read-result.schema.json#/$defs/slidesChartData/properties/kind")]
    [AllowedValues(typeof(SlidesChartKinds))] public required string Kind { get; init; }

    [MistakenFor("box", "bounds")]
    [ReadBack("read-result.schema.json#/$defs/slidesShapeData/properties/rect")]
    public required SlidesRectInput Rect { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesChartData/properties/categories")]
    [MinItems(1), MaxItems(1000)] public required IReadOnlyList<string> Categories { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesChartData/properties/series")]
    [MinItems(1), MaxItems(50)] public required IReadOnlyList<SlidesChartSeriesInput> Series { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesChartData/properties/title")]
    public string? Title { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        SlidesChartSeriesInput.RequireOneValuePerCategory(Categories, Series);
        Require(Kind != SlidesChartKinds.Scatter || Series.All(static item => item.XValues?.Count == item.Values.Count),
            "every scatter series must have one x value per value");
        return this;
    }
}

/// <summary>
/// Replaces the categories or series of a chart in its embedded workbook. When both are given,
/// every series has one value per category.
/// </summary>
[Operation("set_chart_data")]
[MistakenFor("update_chart_data")]
[AtLeastOneOf("categories", "series")]
public sealed record SetChartDataOp : ShapeTargetOp
{
    [ReadBack("read-result.schema.json#/$defs/slidesChartData/properties/categories")]
    [MinItems(1), MaxItems(1000)] public IReadOnlyList<string>? Categories { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesChartData/properties/series")]
    [MinItems(1), MaxItems(50)] public IReadOnlyList<SlidesChartSeriesInput>? Series { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        SlidesChartSeriesInput.RequireOneValuePerCategory(Categories, Series);
        return this;
    }
}

/// <summary>Deletes one shape.</summary>
[Operation("delete_shape")]
public sealed record DeleteShapeOp : ShapeTargetOp;

/// <summary>
/// Formats one shape and every run of its text. A table's fill, line and text are those of all
/// its cells; a chart's fill and line are its chart area's, and its text includes the title,
/// legend, axes and data labels. A shape without text, such as a picture, refuses text styles.
/// </summary>
[Operation("format_shape")]
[MistakenFor("set_shape_style")]
public sealed record FormatShapeOp : ShapeTargetOp
{
    public required SlidesShapeStyleInput Style { get; init; }
}

/// <summary>
/// Moves or resizes one shape in presentation points from the slide's top-left corner; omitted
/// sides keep their values. It must set at least one of x, y, width and height.
/// </summary>
[Operation("set_shape_bounds")]
[AtLeastOneOf("x", "y", "width", "height")]
public sealed record SetShapeBoundsOp : ShapeTargetOp
{
    [ReadBack("read-result.schema.json#/$defs/slidesRect/properties/x")]
    [Minimum(0)] public double? X { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesRect/properties/y")]
    [Minimum(0)] public double? Y { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesRect/properties/width")]
    [ExclusiveMinimum(0)] public double? Width { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesRect/properties/height")]
    [ExclusiveMinimum(0)] public double? Height { get; init; }
}

/// <summary>
/// Shows footer text, slide numbers or dates through the layout's own placeholders; it must
/// set at least one of text, showNumber and showDate.
/// </summary>
[Operation("set_footer")]
[AtLeastOneOf("text", "showNumber", "showDate")]
public sealed record SetFooterOp : SlidesOp
{
    /// <summary>The slides to change; every slide when omitted.</summary>
    [PageRange] public string? Slides { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesFooterData/properties/text")]
    public string? Text { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesFooterData/properties/showNumber")]
    public bool? ShowNumber { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesFooterData/properties/showDate")]
    public bool? ShowDate { get; init; }
}

/// <summary>Sets the slide-show transition of slides.</summary>
[Operation("set_transition")]
[AtLeastOneOf("kind", "durationMs")]
public sealed record SetTransitionOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }

    [ReadBack("read-result.schema.json#/$defs/slidesTransitionData/properties/kind")]
    [AllowedValues(typeof(SlidesTransitionKinds))] public string? Kind { get; init; }

    /// <summary>The transition duration in milliseconds.</summary>
    [ReadBack("read-result.schema.json#/$defs/slidesTransitionData/properties/durationMs")]
    [Minimum(0), Maximum(60000)] public int? DurationMs { get; init; }
}

/// <summary>Sets document properties; omitted properties keep their values.</summary>
[Operation("set_properties")]
[AtLeastOneOf("title", "author", "subject", "keywords", "company")]
public sealed record SlidesSetPropertiesOp : SlidesOp
{
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Title { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Author { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Subject { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Keywords { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Company { get; init; }
}

/// <summary>Sets the slide size of the whole presentation.</summary>
[Operation("set_slide_size")]
public sealed record SetSlideSizeOp : SlidesOp
{
    /// <summary>The new slide size.</summary>
    [MistakenFor("size")]
    [ReadBack("inspect-result.schema.json#/$defs/slidesPresentationSummary/properties/slideSize")]
    public required SlidesSlideSizeInput SlideSize { get; init; }

    /// <summary>Whether the content is scaled to fit the new size rather than kept at its position.</summary>
    [NotState]
    public bool ScaleContent { get; init; } = true;
}

/// <summary>Source-generated serialization metadata for Slides operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(CopySlideOp))]
[JsonSerializable(typeof(DeleteShapeOp))]
[JsonSerializable(typeof(DeleteSlidesOp))]
[JsonSerializable(typeof(FormatShapeOp))]
[JsonSerializable(typeof(ImportSlidesOp))]
[JsonSerializable(typeof(InsertChartOp))]
[JsonSerializable(typeof(InsertSectionOp))]
[JsonSerializable(typeof(InsertShapeOp))]
[JsonSerializable(typeof(InsertSlideOp))]
[JsonSerializable(typeof(MoveSlideOp))]
[JsonSerializable(typeof(SetBackgroundOp))]
[JsonSerializable(typeof(SetBodyOp))]
[JsonSerializable(typeof(SetChartDataOp))]
[JsonSerializable(typeof(SetFooterOp))]
[JsonSerializable(typeof(SetLayoutOp))]
[JsonSerializable(typeof(SetNotesOp))]
[JsonSerializable(typeof(SetShapeBoundsOp))]
[JsonSerializable(typeof(SetSlideHiddenOp))]
[JsonSerializable(typeof(SetSlideSizeOp))]
[JsonSerializable(typeof(SetTextOp))]
[JsonSerializable(typeof(SetTitleOp))]
[JsonSerializable(typeof(SetTransitionOp))]
[JsonSerializable(typeof(SlidesInsertImageOp))]
[JsonSerializable(typeof(SlidesInsertTableOp))]
[JsonSerializable(typeof(SlidesReplaceTextOp))]
[JsonSerializable(typeof(SlidesSetPropertiesOp))]
[JsonSerializable(typeof(SlidesSetTableCellOp))]
internal sealed partial class SlidesOpsJsonContext : JsonSerializerContext;
