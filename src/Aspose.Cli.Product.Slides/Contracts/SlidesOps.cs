using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>A validated, atomic presentation edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Aspose.Cli.Sdk.Serialization.ProductJsonRoot]
public sealed record SlidesOpsBatch : BoundedOperationEnvelope<SlidesOp>;

/// <summary>Base of every Slides operation.</summary>
[JsonConverter(typeof(Serialization.SlidesOpJsonConverter))]
public abstract record SlidesOp : BoundedOperation
{
    [JsonIgnore]
    public abstract string OpName { get; }
}

/// <summary>A 1-based slide number or stable slide identifier.</summary>
public abstract record SlideTargetOp : SlidesOp
{
    public int? Slide { get; init; }
    public uint? SlideId { get; init; }
}

/// <summary>A slide shape addressed by persistent slide-scoped id, name, or placeholder role.</summary>
public abstract record ShapeTargetOp : SlideTargetOp
{
    public long? Shape { get; init; }
    public string? ShapeName { get; init; }
    public string? Placeholder { get; init; }
}

/// <summary>A rectangle in presentation points from the top-left origin.</summary>
public sealed record SlidesRectInput
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}

/// <summary>One placeholder-aware body paragraph.</summary>
public sealed record SlidesParagraphInput
{
    public required string Text { get; init; }
    public int Level { get; init; }
}

/// <summary>Bounded visual styling for a slide shape.</summary>
public sealed record SlidesShapeStyleInput
{
    public string? Fill { get; init; }
    public string? Line { get; init; }
    public string? Font { get; init; }
    public double? Size { get; init; }
    public string? Color { get; init; }
    public bool? Bold { get; init; }
}

/// <summary>One chart series supplied by an operation.</summary>
public sealed record SlidesChartSeriesInput
{
    public required string Name { get; init; }
    public required IReadOnlyList<double> Values { get; init; }
    public IReadOnlyList<double>? XValues { get; init; }
}

public sealed record AddSlideOp : SlidesOp
{
    public override string OpName => "add_slide";
    public string? Layout { get; init; }
    public int? At { get; init; }
}

public sealed record DeleteSlidesOp : SlidesOp
{
    public override string OpName => "delete_slides";
    public required string Slides { get; init; }
}

public sealed record MoveSlideOp : SlideTargetOp
{
    public override string OpName => "move_slide";
    public required int To { get; init; }
}

public sealed record DuplicateSlideOp : SlideTargetOp
{
    public override string OpName => "duplicate_slide";
    public int? At { get; init; }
}

public sealed record SetSlideHiddenOp : SlidesOp
{
    public override string OpName => "set_slide_hidden";
    public required string Slides { get; init; }
    public required bool Hidden { get; init; }
}

public sealed record ApplyLayoutOp : SlidesOp
{
    public override string OpName => "apply_layout";
    public required string Slides { get; init; }
    public required string Layout { get; init; }
}

public sealed record SetBackgroundOp : SlidesOp
{
    public override string OpName => "set_background";
    public string? Slides { get; init; }
    public string? Color { get; init; }
    public string? ImagePath { get; init; }
}

public sealed record AddSectionOp : SlidesOp
{
    public override string OpName => "add_section";
    public required string Name { get; init; }
    public required int AtSlide { get; init; }
}

public sealed record AppendPresentationOp : SlidesOp
{
    public override string OpName => "append_presentation";
    public required string Path { get; init; }
    public string MasterPolicy { get; init; } = "keep-source";
}

public sealed record SetTitleOp : SlideTargetOp
{
    public override string OpName => "set_title";
    public required string Text { get; init; }
}

public sealed record SetBodyOp : SlideTargetOp
{
    public override string OpName => "set_body";
    public required IReadOnlyList<SlidesParagraphInput> Paragraphs { get; init; }
}

public sealed record SetTextOp : ShapeTargetOp
{
    public override string OpName => "set_text";
    public required string Text { get; init; }
}

public sealed record SlidesReplaceTextOp : SlidesOp
{
    public override string OpName => "replace_text";
    public required string Find { get; init; }
    public required string Replace { get; init; }
    public bool Regex { get; init; }
    public bool MatchCase { get; init; }
    public string Scope { get; init; } = "all";
}

public sealed record SetNotesOp : SlideTargetOp
{
    public override string OpName => "set_notes";
    public required string Text { get; init; }
}

public sealed record SlidesInsertImageOp : SlideTargetOp
{
    public override string OpName => "insert_image";
    public required string Path { get; init; }
    public SlidesRectInput? Rect { get; init; }
}

public sealed record InsertShapeOp : SlideTargetOp
{
    public override string OpName => "insert_shape";
    public required string Kind { get; init; }
    public required SlidesRectInput Rect { get; init; }
    public string? Text { get; init; }
    public SlidesShapeStyleInput? Style { get; init; }
}

public sealed record SlidesInsertTableOp : SlideTargetOp
{
    public override string OpName => "insert_table";
    public required SlidesRectInput Rect { get; init; }
    public required int Rows { get; init; }
    public required int Cols { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Data { get; init; }
}

public sealed record SlidesSetTableCellOp : ShapeTargetOp
{
    public override string OpName => "set_table_cell";
    public required int Row { get; init; }
    public required int Col { get; init; }
    public required string Text { get; init; }
}

public sealed record InsertChartOp : SlideTargetOp
{
    public override string OpName => "insert_chart";
    public required string Kind { get; init; }
    public required SlidesRectInput Rect { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<SlidesChartSeriesInput> Series { get; init; }
    public string? Title { get; init; }
}

public sealed record UpdateChartDataOp : ShapeTargetOp
{
    public override string OpName => "update_chart_data";
    public IReadOnlyList<string>? Categories { get; init; }
    public IReadOnlyList<SlidesChartSeriesInput>? Series { get; init; }
}

public sealed record DeleteShapeOp : ShapeTargetOp
{
    public override string OpName => "delete_shape";
}

public sealed record SetShapeStyleOp : ShapeTargetOp
{
    public override string OpName => "set_shape_style";
    public SlidesShapeStyleInput Style { get; init; } = new();
}

public sealed record SetFooterOp : SlidesOp
{
    public override string OpName => "set_footer";
    public string? Slides { get; init; }
    public string? Text { get; init; }
    public bool? ShowNumber { get; init; }
    public bool? ShowDate { get; init; }
}

public sealed record SetTransitionOp : SlidesOp
{
    public override string OpName => "set_transition";
    public required string Slides { get; init; }
    public string? Kind { get; init; }
    public int? DurationMs { get; init; }
}

public sealed record SlidesSetPropertiesOp : SlidesOp
{
    public override string OpName => "set_properties";
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public string? Keywords { get; init; }
    public string? Company { get; init; }
}

public sealed record SetSlideSizeOp : SlidesOp
{
    public override string OpName => "set_slide_size";
    public required string Size { get; init; }
    public bool ScaleContent { get; init; } = true;
}

/// <summary>Frozen Slides v2 operation registry.</summary>
public static class SlidesOps
{
    public static IReadOnlyDictionary<string, Type> Registry { get; } =
        new SortedDictionary<string, Type>(StringComparer.Ordinal)
        {
            ["add_section"] = typeof(AddSectionOp),
            ["add_slide"] = typeof(AddSlideOp),
            ["append_presentation"] = typeof(AppendPresentationOp),
            ["apply_layout"] = typeof(ApplyLayoutOp),
            ["delete_shape"] = typeof(DeleteShapeOp),
            ["delete_slides"] = typeof(DeleteSlidesOp),
            ["duplicate_slide"] = typeof(DuplicateSlideOp),
            ["insert_chart"] = typeof(InsertChartOp),
            ["insert_image"] = typeof(SlidesInsertImageOp),
            ["insert_shape"] = typeof(InsertShapeOp),
            ["insert_table"] = typeof(SlidesInsertTableOp),
            ["move_slide"] = typeof(MoveSlideOp),
            ["replace_text"] = typeof(SlidesReplaceTextOp),
            ["set_background"] = typeof(SetBackgroundOp),
            ["set_body"] = typeof(SetBodyOp),
            ["set_footer"] = typeof(SetFooterOp),
            ["set_notes"] = typeof(SetNotesOp),
            ["set_properties"] = typeof(SlidesSetPropertiesOp),
            ["set_shape_style"] = typeof(SetShapeStyleOp),
            ["set_slide_hidden"] = typeof(SetSlideHiddenOp),
            ["set_slide_size"] = typeof(SetSlideSizeOp),
            ["set_table_cell"] = typeof(SlidesSetTableCellOp),
            ["set_text"] = typeof(SetTextOp),
            ["set_title"] = typeof(SetTitleOp),
            ["set_transition"] = typeof(SetTransitionOp),
            ["update_chart_data"] = typeof(UpdateChartDataOp),
        };

    public static IReadOnlyList<string> Names { get; } = Registry.Keys.ToArray();
}
