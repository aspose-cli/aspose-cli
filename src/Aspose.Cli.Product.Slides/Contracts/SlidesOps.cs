using System.Globalization;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Sdk.Operations.OperationInvalidException;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>A validated, atomic presentation edit batch.</summary>
[ProductJsonRoot]
public sealed record SlidesOpsBatch : BoundedOperationEnvelope<SlidesOp>;

/// <summary>
/// The operation document of <c>slides edit --ops</c>. Every slide and shape target resolves
/// against the presentation as it was before the first operation, so content inserted earlier
/// in the batch cannot be targeted, and a target deleted earlier fails the operation.
/// </summary>
[OperationVocabulary(SlidesSchemaIds.Ops, MaximumOperations = 256, JsonContext = typeof(SlidesOpsJsonContext))]
[JsonConverter(typeof(OperationJsonConverter<SlidesOp>))]
public abstract partial record SlidesOp : BoundedOperation;

/// <summary>An operation on one slide, named by its 1-based number or its stable id.</summary>
[ExactlyOneOf("slide", "slideId")]
public abstract record SlideTargetOp : SlidesOp
{
    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)] public int? Slide { get; init; }

    /// <summary>The stable slide id reported by inspect and query slides.</summary>
    [Minimum(1)] public uint? SlideId { get; init; }
}

/// <summary>An operation on one top-level shape of a slide, named by its id, its name or its placeholder role.</summary>
[ExactlyOneOf("shapeId", "shapeName", "placeholder")]
public abstract record ShapeTargetOp : SlideTargetOp
{
    /// <summary>The persistent slide-scoped shapeId returned by query slides; not a shape position.</summary>
    [Minimum(1)] public long? ShapeId { get; init; }

    /// <summary>The shape name, matched case-sensitively.</summary>
    [Pattern(@"\S")] public string? ShapeName { get; init; }

    /// <summary>The placeholder role, as query slides reports it.</summary>
    [AllowedValues("title", "body", "subtitle", "footer")] public string? Placeholder { get; init; }
}

/// <summary>Which slides <c>append_presentation</c> formats with: their own masters or the destination's first master.</summary>
public static class SlidesMasterPolicies
{
    public const string KeepSource = "keep-source";
    public const string UseDest = "use-dest";
}

/// <summary>The shapes <c>insert_shape</c> draws.</summary>
public static class SlidesShapeKinds
{
    public const string Rectangle = "rectangle";
    public const string RoundedRectangle = "rounded-rectangle";
    public const string Ellipse = "ellipse";
    public const string Line = "line";
    public const string Chevron = "chevron";
}

/// <summary>The charts <c>insert_chart</c> creates.</summary>
public static class SlidesChartKinds
{
    public const string Bar = "bar";
    public const string Column = "column";
    public const string Line = "line";
    public const string Pie = "pie";
    public const string Scatter = "scatter";
}

/// <summary>The slide transitions <c>set_transition</c> applies.</summary>
public static class SlidesTransitionKinds
{
    public const string None = "none";
    public const string Fade = "fade";
    public const string Push = "push";
    public const string Wipe = "wipe";
    public const string Split = "split";
    public const string Cover = "cover";
}

/// <summary>A rectangle in presentation points from the top-left origin.</summary>
public sealed record SlidesRectInput
{
    [Minimum(0)] public required double X { get; init; }

    [Minimum(0)] public required double Y { get; init; }

    [ExclusiveMinimum(0)] public required double Width { get; init; }

    [ExclusiveMinimum(0)] public required double Height { get; init; }
}

/// <summary>One body paragraph; its bullets and spacing come from the placeholder's level.</summary>
public sealed record SlidesParagraphInput
{
    public required string Text { get; init; }

    /// <summary>The outline level, 0 for a top-level paragraph.</summary>
    [Minimum(0), Maximum(8)] public int Level { get; init; }

    /// <summary>Whether the paragraph shows its level's bullet; false writes a plain paragraph without one.</summary>
    public bool Bullet { get; init; } = true;
}

/// <summary>Visual styling of a slide shape and its text; it must set at least one property.</summary>
[MinProperties(1)]
public sealed record SlidesShapeStyleInput
{
    [HexColor] public string? Fill { get; init; }

    [HexColor] public string? Line { get; init; }

    /// <summary>A font name, applied to Latin, East Asian and complex-script text.</summary>
    public string? Font { get; init; }

    /// <summary>The font size in points.</summary>
    [ExclusiveMinimum(0), Maximum(400)] public double? Size { get; init; }

    /// <summary>The text color.</summary>
    [HexColor] public string? Color { get; init; }

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

/// <summary>Adds an empty slide.</summary>
[Operation("add_slide")]
public sealed record AddSlideOp : SlidesOp
{
    /// <summary>The layout name, matched case-insensitively; the first layout when omitted.</summary>
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
    [Minimum(1)] public required int To { get; init; }
}

/// <summary>Inserts a copy of one slide.</summary>
[Operation("duplicate_slide")]
public sealed record DuplicateSlideOp : SlideTargetOp
{
    /// <summary>The 1-based position of the copy, at most the slide count plus one; the end when omitted.</summary>
    [Minimum(1)] public int? At { get; init; }
}

/// <summary>Hides or shows slides in a slide show.</summary>
[Operation("set_slide_hidden")]
public sealed record SetSlideHiddenOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }

    public required bool Hidden { get; init; }
}

/// <summary>Applies a layout to slides, which drop their own background to show the layout's.</summary>
[Operation("apply_layout")]
public sealed record ApplyLayoutOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }

    /// <summary>The layout name, matched case-insensitively.</summary>
    [Pattern(@"\S")] public required string Layout { get; init; }
}

/// <summary>Gives slides their own solid-color or stretched-picture background.</summary>
[Operation("set_background")]
[ExactlyOneOf("color", "imagePath")]
public sealed record SetBackgroundOp : SlidesOp
{
    /// <summary>The slides to change; every slide when omitted.</summary>
    [PageRange] public string? Slides { get; init; }

    [HexColor] public string? Color { get; init; }

    /// <summary>The picture, relative to the working directory.</summary>
    [InputPath] public string? ImagePath { get; init; }
}

/// <summary>Starts a new section at a slide; section names are unique.</summary>
[Operation("add_section")]
public sealed record AddSectionOp : SlidesOp
{
    [Pattern(@"\S")] public required string Name { get; init; }

    /// <summary>The 1-based number of the section's first slide.</summary>
    [Minimum(1)] public required int StartSlide { get; init; }
}

/// <summary>Appends every slide of another presentation.</summary>
[Operation("append_presentation")]
public sealed record AppendPresentationOp : SlidesOp
{
    /// <summary>The source presentation, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>
    /// Whether appended slides keep their own masters, or take the destination's first master
    /// and show its layouts' backgrounds instead of their own.
    /// </summary>
    [AllowedValues(typeof(SlidesMasterPolicies))] public string MasterPolicy { get; init; } = SlidesMasterPolicies.KeepSource;
}

/// <summary>Sets the text of a slide's title placeholder.</summary>
[Operation("set_title")]
public sealed record SetTitleOp : SlideTargetOp
{
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
    [MinLength(1)] public required string Find { get; init; }

    /// <summary>The replacement; with regex it honors .NET substitutions such as $1, and $$ is a literal $.</summary>
    public required string Replace { get; init; }

    /// <summary>Whether find is a .NET regular expression rather than literal text.</summary>
    public bool Regex { get; init; }

    public bool MatchCase { get; init; }

    [AllowedValues(typeof(PresentationSearchScopes))] public string Scope { get; init; } = PresentationSearchScopes.All;

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Regex)
        {
            try
            {
                _ = SafeRegex.Create(Find, MatchCase);
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
    public required string Text { get; init; }
}

/// <summary>Inserts a picture.</summary>
[Operation("insert_image")]
public sealed record SlidesInsertImageOp : SlideTargetOp
{
    /// <summary>The picture, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>Where the picture goes; when omitted it keeps its aspect ratio, centered in 80% by 75% of the slide.</summary>
    public SlidesRectInput? Rect { get; init; }

    /// <summary>The picture's alternative text, which screen readers and exported PDFs carry.</summary>
    public string? AltText { get; init; }
}

/// <summary>Inserts a shape.</summary>
[Operation("insert_shape")]
public sealed record InsertShapeOp : SlideTargetOp
{
    [AllowedValues(typeof(SlidesShapeKinds))] public required string Kind { get; init; }

    public required SlidesRectInput Rect { get; init; }

    public string? Text { get; init; }

    public SlidesShapeStyleInput? Style { get; init; }
}

/// <summary>Inserts a table with equal rows and columns; data fills it from the top-left cell.</summary>
[Operation("insert_table")]
public sealed record SlidesInsertTableOp : SlideTargetOp
{
    /// <summary>The most rows a slide table holds, shared by Markdown tables.</summary>
    internal const int MaxRows = 100;

    /// <summary>The most columns a slide table holds, shared by Markdown tables.</summary>
    internal const int MaxCols = 50;

    public required SlidesRectInput Rect { get; init; }

    [Minimum(1), Maximum(MaxRows)] public required int RowCount { get; init; }

    [Minimum(1), Maximum(MaxCols)] public required int ColumnCount { get; init; }

    /// <summary>Cell text by row, at most rowCount rows of at most columnCount cells each.</summary>
    public IReadOnlyList<IReadOnlyList<string>>? Data { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        Require(Data is null || Data.Count <= RowCount && Data.All(row => row.Count <= ColumnCount),
            "data must fit the table: at most rowCount rows of at most columnCount cells");
        return this;
    }
}

/// <summary>Sets the text of one cell of a table shape.</summary>
[Operation("set_table_cell")]
public sealed record SlidesSetTableCellOp : ShapeTargetOp
{
    /// <summary>The 1-based row.</summary>
    [Minimum(1)] public required int Row { get; init; }

    /// <summary>The 1-based column.</summary>
    [Minimum(1)] public required int Col { get; init; }

    public required string Text { get; init; }
}

/// <summary>
/// Inserts a chart. Every series has one value per category, and a scatter series has one x
/// value per value.
/// </summary>
[Operation("insert_chart")]
public sealed record InsertChartOp : SlideTargetOp
{
    [AllowedValues(typeof(SlidesChartKinds))] public required string Kind { get; init; }

    public required SlidesRectInput Rect { get; init; }

    [MinItems(1), MaxItems(1000)] public required IReadOnlyList<string> Categories { get; init; }

    [MinItems(1), MaxItems(50)] public required IReadOnlyList<SlidesChartSeriesInput> Series { get; init; }

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
[Operation("update_chart_data")]
[AtLeastOneOf("categories", "series")]
public sealed record UpdateChartDataOp : ShapeTargetOp
{
    [MinItems(1), MaxItems(1000)] public IReadOnlyList<string>? Categories { get; init; }

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
/// Styles one shape and every run of its text. A table's fill, line and text are those of all
/// its cells; a chart's fill and line are its chart area's, and its text includes the title,
/// legend, axes and data labels. A shape without text, such as a picture, refuses text styles.
/// </summary>
[Operation("set_shape_style")]
public sealed record SetShapeStyleOp : ShapeTargetOp
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
    [Minimum(0)] public double? X { get; init; }

    [Minimum(0)] public double? Y { get; init; }

    [ExclusiveMinimum(0)] public double? Width { get; init; }

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

    public string? Text { get; init; }

    public bool? ShowNumber { get; init; }

    public bool? ShowDate { get; init; }
}

/// <summary>Sets the slide-show transition of slides.</summary>
[Operation("set_transition")]
[AtLeastOneOf("kind", "durationMs")]
public sealed record SetTransitionOp : SlidesOp
{
    [PageRange] public required string Slides { get; init; }

    [AllowedValues(typeof(SlidesTransitionKinds))] public string? Kind { get; init; }

    /// <summary>The transition duration in milliseconds.</summary>
    [Minimum(0), Maximum(60000)] public int? DurationMs { get; init; }
}

/// <summary>Sets document properties; omitted properties keep their values.</summary>
[Operation("set_properties")]
[AtLeastOneOf("title", "author", "subject", "keywords", "company")]
public sealed record SlidesSetPropertiesOp : SlidesOp
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Subject { get; init; }

    public string? Keywords { get; init; }

    public string? Company { get; init; }
}

/// <summary>Sets the slide size of the whole presentation.</summary>
[Operation("set_slide_size")]
public sealed record SetSlideSizeOp : SlidesOp
{
    /// <summary>16x9, 4x3, or WxHpt such as 800x450pt; each side is 72 through 7200 points.</summary>
    [Pattern(@"^(16x9|4x3|[0-9]+(?:\.[0-9]+)?x[0-9]+(?:\.[0-9]+)?pt)$")]
    public required string Size { get; init; }

    /// <summary>Whether the content is scaled to fit the new size rather than kept at its position.</summary>
    public bool ScaleContent { get; init; } = true;

    /// <summary>The sides of a WxHpt size in points; false for a named size.</summary>
    internal bool TryGetPoints(out float width, out float height)
    {
        width = 0;
        height = 0;
        string[] sides = Size.EndsWith("pt", StringComparison.Ordinal) ? Size[..^2].Split('x') : [];
        return sides.Length == 2
            && float.TryParse(sides[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out width)
            && float.TryParse(sides[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out height);
    }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        Require(Size is "16x9" or "4x3"
            || TryGetPoints(out float width, out float height) && width is >= 72 and <= 7200 && height is >= 72 and <= 7200,
            "size sides must be from 72 through 7200 points");
        return this;
    }
}

/// <summary>Source-generated serialization metadata for Slides operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AddSectionOp))]
[JsonSerializable(typeof(AddSlideOp))]
[JsonSerializable(typeof(AppendPresentationOp))]
[JsonSerializable(typeof(ApplyLayoutOp))]
[JsonSerializable(typeof(DeleteShapeOp))]
[JsonSerializable(typeof(DeleteSlidesOp))]
[JsonSerializable(typeof(DuplicateSlideOp))]
[JsonSerializable(typeof(InsertChartOp))]
[JsonSerializable(typeof(InsertShapeOp))]
[JsonSerializable(typeof(MoveSlideOp))]
[JsonSerializable(typeof(SetBackgroundOp))]
[JsonSerializable(typeof(SetBodyOp))]
[JsonSerializable(typeof(SetFooterOp))]
[JsonSerializable(typeof(SetNotesOp))]
[JsonSerializable(typeof(SetShapeBoundsOp))]
[JsonSerializable(typeof(SetShapeStyleOp))]
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
[JsonSerializable(typeof(UpdateChartDataOp))]
internal sealed partial class SlidesOpsJsonContext : JsonSerializerContext;
