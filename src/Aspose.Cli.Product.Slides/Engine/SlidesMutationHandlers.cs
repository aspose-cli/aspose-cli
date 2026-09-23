using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesContentHandlers;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesObjectHandlers;
using static Aspose.Cli.Product.Slides.Engine.SlidesStructuralHandlers;
using static Aspose.Cli.Product.Slides.Engine.SlidesStyleHandlers;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Applies resolved presentation operations without owning batch lifecycle.</summary>
internal static class SlidesMutationHandlers
{
    internal static IReadOnlyList<ResolvedSlidesOp> ResolveBatch(
        Presentation presentation,
        SlidesOpsBatch batch)
    {
        var resolved = new List<ResolvedSlidesOp>(batch.Ops.Count);
        foreach (SlidesOp op in batch.Ops)
        {
            ISlide? slide = op switch
            {
                SlideTargetOp target => ResolveSlide(presentation, target),
                AddSectionOp value => value.AtSlide <= presentation.Slides.Count
                    ? presentation.Slides[value.AtSlide - 1]
                    : throw SlideNotFound(value.AtSlide, presentation.Slides.Count),
                _ => null,
            };
            IShape? shape = op is ShapeTargetOp shapeTarget
                ? ResolveShape(slide!, shapeTarget)
                : null;
            IReadOnlyList<ISlide>? slides = op switch
            {
                DeleteSlidesOp value => ResolveSlides(presentation, value.Slides),
                SetSlideHiddenOp value => ResolveSlides(presentation, value.Slides),
                ApplyLayoutOp value => ResolveSlides(presentation, value.Slides),
                SetBackgroundOp value => ResolveOptionalSlides(presentation, value.Slides),
                SetFooterOp value => ResolveOptionalSlides(presentation, value.Slides),
                SetTransitionOp value => ResolveSlides(presentation, value.Slides),
                _ => null,
            };
            ILayoutSlide? layout = op switch
            {
                AddSlideOp { Layout: not null } value => ResolveLayout(presentation, value.Layout),
                ApplyLayoutOp value => ResolveLayout(presentation, value.Layout),
                _ => null,
            };
            // Detached shapes may no longer expose their SDK identity. Keep the receipt target
            // aligned with the original presentation used to resolve every operation.
            resolved.Add(new ResolvedSlidesOp(op, slide, shape, slides, layout, shape?.OfficeInteropShapeId));
        }

        return resolved;
    }

    internal static long ApplyResolved(
        ResourceBudgetLedger resourceBudgets,
        SlidesPresentationLoader loader,
        Presentation presentation,
        ResolvedSlidesOp item,
        ISet<uint> touched)
    {
        try
        {
            if (item.Slide is not null && !presentation.Slides.Contains(item.Slide)
                || item.Slides?.Any(slide => !presentation.Slides.Contains(slide)) == true)
            {
                throw new OperationInvalidException("A targeted slide was deleted by an earlier operation.");
            }
            if (item.Shape is not null && !item.Slide!.Shapes.Any(shape => ReferenceEquals(shape, item.Shape)))
            {
                throw new OperationInvalidException("A targeted shape was deleted by an earlier operation.");
            }

            return item.Op switch
            {
                AddSlideOp value => AddSlide(presentation, value, item.Layout, touched),
                DeleteSlidesOp => DeleteSlides(presentation, item.Slides!),
                MoveSlideOp value => MoveSlide(presentation, value, item.Slide!, touched),
                DuplicateSlideOp value => DuplicateSlide(presentation, value, item.Slide!, touched),
                SetSlideHiddenOp value => SetHidden(item.Slides!, value.Hidden, touched),
                ApplyLayoutOp => ApplyLayout(item.Slides!, item.Layout!, touched),
                SetBackgroundOp value => SetBackground(resourceBudgets.Inputs, presentation, item.Slides!, value, touched),
                AddSectionOp value => AddSection(presentation, value, item.Slide!),
                AppendPresentationOp value => AppendPresentation(loader, presentation, value, touched),
                SetTitleOp value => SetTitle(item.Slide!, value.Text, touched),
                SetBodyOp value => SetBody(item.Slide!, value.Paragraphs, touched),
                SetTextOp value => SetText(item.Slide!, item.Shape!, value.Text, touched),
                SlidesReplaceTextOp value => ReplaceText(presentation, value, touched),
                SetNotesOp value => SetNotes(item.Slide!, value.Text, touched),
                SlidesInsertImageOp value => InsertImage(resourceBudgets.Inputs, presentation, item.Slide!, value, touched),
                InsertShapeOp value => InsertShape(item.Slide!, value, touched),
                SlidesInsertTableOp value => InsertTable(item.Slide!, value, touched),
                SlidesSetTableCellOp value => SetTableCell(item.Slide!, item.Shape!, value, touched),
                InsertChartOp value => InsertChart(item.Slide!, value, touched),
                UpdateChartDataOp value => UpdateChart(item.Slide!, item.Shape!, value, touched),
                DeleteShapeOp => DeleteShape(item.Slide!, item.Shape!, touched),
                SetShapeStyleOp value => SetShapeStyle(item.Slide!, item.Shape!, value.Style, touched),
                SetFooterOp value => SetFooter(item.Slides!, value, touched),
                SetTransitionOp value => SetTransition(item.Slides!, value, touched),
                SlidesSetPropertiesOp value => SetProperties(presentation, value),
                SetSlideSizeOp value => SetSlideSize(presentation, value, touched),
                _ => throw new InvalidOperationException($"No Slides handler for {item.Op.GetType().Name}."),
            };
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true
            || exception is IOException or UnauthorizedAccessException)
        {
            throw new EngineOpException(exception.Message, exception);
        }
    }

    internal sealed record ResolvedSlidesOp(
        SlidesOp Op,
        ISlide? Slide,
        IShape? Shape,
        IReadOnlyList<ISlide>? Slides,
        ILayoutSlide? Layout,
        long? ShapeId);
}
