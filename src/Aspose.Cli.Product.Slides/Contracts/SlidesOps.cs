using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>A validated, atomic presentation edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Aspose.Cli.Sdk.Serialization.ProductJsonRoot]
public sealed record SlidesOpsBatch : BoundedOperationEnvelope<SlidesOp>;

/// <summary>Base of every Slides operation.</summary>
[JsonConverter(typeof(Serialization.SlidesOpJsonConverter))]
public abstract record SlidesOp : BoundedOperation;

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
    public string? Layout { get; init; }
    public int? At { get; init; }
}

public sealed record DeleteSlidesOp : SlidesOp
{
    public required string Slides { get; init; }
}

public sealed record MoveSlideOp : SlideTargetOp
{
    public required int To { get; init; }
}

public sealed record DuplicateSlideOp : SlideTargetOp
{
    public int? At { get; init; }
}

public sealed record SetSlideHiddenOp : SlidesOp
{
    public required string Slides { get; init; }
    public required bool Hidden { get; init; }
}

public sealed record ApplyLayoutOp : SlidesOp
{
    public required string Slides { get; init; }
    public required string Layout { get; init; }
}

public sealed record SetBackgroundOp : SlidesOp
{
    public string? Slides { get; init; }
    public string? Color { get; init; }
    public string? ImagePath { get; init; }
}

public sealed record AddSectionOp : SlidesOp
{
    public required string Name { get; init; }
    public required int AtSlide { get; init; }
}

public sealed record AppendPresentationOp : SlidesOp
{
    public required string Path { get; init; }
    public string MasterPolicy { get; init; } = "keep-source";
}

public sealed record SetTitleOp : SlideTargetOp
{
    public required string Text { get; init; }
}

public sealed record SetBodyOp : SlideTargetOp
{
    public required IReadOnlyList<SlidesParagraphInput> Paragraphs { get; init; }
}

public sealed record SetTextOp : ShapeTargetOp
{
    public required string Text { get; init; }
}

public sealed record SlidesReplaceTextOp : SlidesOp
{
    public required string Find { get; init; }
    public required string Replace { get; init; }
    public bool Regex { get; init; }
    public bool MatchCase { get; init; }
    public string Scope { get; init; } = "all";
}

public sealed record SetNotesOp : SlideTargetOp
{
    public required string Text { get; init; }
}

public sealed record SlidesInsertImageOp : SlideTargetOp
{
    public required string Path { get; init; }
    public SlidesRectInput? Rect { get; init; }
}

public sealed record InsertShapeOp : SlideTargetOp
{
    public required string Kind { get; init; }
    public required SlidesRectInput Rect { get; init; }
    public string? Text { get; init; }
    public SlidesShapeStyleInput? Style { get; init; }
}

public sealed record SlidesInsertTableOp : SlideTargetOp
{
    public required SlidesRectInput Rect { get; init; }
    public required int Rows { get; init; }
    public required int Cols { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Data { get; init; }
}

public sealed record SlidesSetTableCellOp : ShapeTargetOp
{
    public required int Row { get; init; }
    public required int Col { get; init; }
    public required string Text { get; init; }
}

public sealed record InsertChartOp : SlideTargetOp
{
    public required string Kind { get; init; }
    public required SlidesRectInput Rect { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<SlidesChartSeriesInput> Series { get; init; }
    public string? Title { get; init; }
}

public sealed record UpdateChartDataOp : ShapeTargetOp
{
    public IReadOnlyList<string>? Categories { get; init; }
    public IReadOnlyList<SlidesChartSeriesInput>? Series { get; init; }
}

public sealed record DeleteShapeOp : ShapeTargetOp;

public sealed record SetShapeStyleOp : ShapeTargetOp
{
    public required SlidesShapeStyleInput Style { get; init; }
}

public sealed record SetFooterOp : SlidesOp
{
    public string? Slides { get; init; }
    public string? Text { get; init; }
    public bool? ShowNumber { get; init; }
    public bool? ShowDate { get; init; }
}

public sealed record SetTransitionOp : SlidesOp
{
    public required string Slides { get; init; }
    public string? Kind { get; init; }
    public int? DurationMs { get; init; }
}

public sealed record SlidesSetPropertiesOp : SlidesOp
{
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public string? Keywords { get; init; }
    public string? Company { get; init; }
}

public sealed record SetSlideSizeOp : SlidesOp
{
    public required string Size { get; init; }
    public bool ScaleContent { get; init; } = true;
}

/// <summary>The Slides operation vocabulary, in published order.</summary>
public static class SlidesOps
{
    public static OperationCatalog<SlidesOp> Catalog { get; } = new OperationCatalog<SlidesOp>(SlidesSchemaIds.Ops, maximumOperations: 256)
        .Add<AddSectionOp>("add_section", SlidesOpRules.AddSection)
        .Add<AddSlideOp>("add_slide", SlidesOpRules.AddSlide)
        .Add<AppendPresentationOp>("append_presentation", SlidesOpRules.AppendPresentation)
        .Add<ApplyLayoutOp>("apply_layout", SlidesOpRules.ApplyLayout)
        .Add<DeleteShapeOp>("delete_shape", SlidesOpRules.DeleteShape)
        .Add<DeleteSlidesOp>("delete_slides", SlidesOpRules.DeleteSlides)
        .Add<DuplicateSlideOp>("duplicate_slide", SlidesOpRules.DuplicateSlide)
        .Add<InsertChartOp>("insert_chart", SlidesOpRules.InsertChart)
        .Add<SlidesInsertImageOp>("insert_image", SlidesOpRules.InsertImage)
        .Add<InsertShapeOp>("insert_shape", SlidesOpRules.InsertShape)
        .Add<SlidesInsertTableOp>("insert_table", SlidesOpRules.InsertTable)
        .Add<MoveSlideOp>("move_slide", SlidesOpRules.MoveSlide)
        .Add<SlidesReplaceTextOp>("replace_text", SlidesOpRules.ReplaceText)
        .Add<SetBackgroundOp>("set_background", SlidesOpRules.SetBackground)
        .Add<SetBodyOp>("set_body", SlidesOpRules.SetBody)
        .Add<SetFooterOp>("set_footer", SlidesOpRules.SetFooter)
        .Add<SetNotesOp>("set_notes", SlidesOpRules.SetNotes)
        .Add<SlidesSetPropertiesOp>("set_properties", SlidesOpRules.SetProperties)
        .Add<SetShapeStyleOp>("set_shape_style", SlidesOpRules.SetShapeStyle)
        .Add<SetSlideHiddenOp>("set_slide_hidden", SlidesOpRules.SetSlideHidden)
        .Add<SetSlideSizeOp>("set_slide_size", SlidesOpRules.SetSlideSize)
        .Add<SlidesSetTableCellOp>("set_table_cell", SlidesOpRules.SetTableCell)
        .Add<SetTextOp>("set_text", SlidesOpRules.SetText)
        .Add<SetTitleOp>("set_title", SlidesOpRules.SetTitle)
        .Add<SetTransitionOp>("set_transition", SlidesOpRules.SetTransition)
        .Add<UpdateChartDataOp>("update_chart_data", SlidesOpRules.UpdateChartData);
}
