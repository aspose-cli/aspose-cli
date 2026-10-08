using System.Runtime.ExceptionServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// Applies one resolved presentation operation. Each operation has its handler in the
/// structural, content, object or style part of this class, and returns the number of items
/// it changed.
/// </summary>
internal sealed partial class SlidesMutationHandlers : ISlidesOpHandler<long>
{
    private readonly InputSource _inputs;
    private readonly SlidesPresentationLoader _loader;
    private readonly Presentation _presentation;
    private readonly ResolvedSlidesOp _target;
    private readonly ISet<uint> _touched;
    private readonly ICollection<Warning> _warnings;
    private readonly bool _evaluation;

    /// <summary>Creates the handlers of one operation.</summary>
    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="loader">Opens the presentations that operations append.</param>
    /// <param name="presentation">The presentation being edited.</param>
    /// <param name="target">The operation with the targets it resolved before the batch started.</param>
    /// <param name="touched">Receives the ids of the slides the operation changes.</param>
    /// <param name="warnings">Receives what the operation reports about its result.</param>
    /// <param name="evaluation">Whether the engine runs in evaluation mode, which cuts text short when it is read.</param>
    internal SlidesMutationHandlers(
        InputSource inputs,
        SlidesPresentationLoader loader,
        Presentation presentation,
        ResolvedSlidesOp target,
        ISet<uint> touched,
        ICollection<Warning> warnings,
        bool evaluation)
    {
        _inputs = inputs;
        _loader = loader;
        _presentation = presentation;
        _target = target;
        _touched = touched;
        _warnings = warnings;
        _evaluation = evaluation;
    }

    /// <summary>The resolved target slide of a slide or shape operation.</summary>
    private ISlide Slide => _target.Slide!;

    /// <summary>The resolved target shape of a shape operation.</summary>
    private IShape Shape => _target.Shape!;

    /// <summary>The resolved target slides of a multi-slide operation.</summary>
    private IReadOnlyList<ISlide> Slides => _target.Slides!;

    /// <summary>
    /// Resolves every operation's targets against the presentation before the first one is
    /// applied, so later operations address what the caller read, not what earlier ones made.
    /// A target that cannot be resolved becomes the operation's failure, raised when the
    /// operation runs so the batch reports it at that operation's position.
    /// </summary>
    internal static IReadOnlyList<ResolvedSlidesOp> ResolveBatch(
        Presentation presentation,
        SlidesOpsBatch batch)
    {
        var resolved = new List<ResolvedSlidesOp>(batch.Ops.Count);
        foreach (SlidesOp op in batch.Ops)
        {
            try
            {
                resolved.Add(Resolve(presentation, op));
            }
            catch (Exception failure) when (failure is CliException or OperationInvalidException)
            {
                resolved.Add(new ResolvedSlidesOp(op, null, null, null, null, null, failure));
            }
        }

        return resolved;
    }

    private static ResolvedSlidesOp Resolve(Presentation presentation, SlidesOp op)
    {
        ISlide? slide = op switch
        {
            SlideTargetOp target => ResolveSlide(presentation, target),
            AddSectionOp value => value.StartSlide <= presentation.Slides.Count
                ? presentation.Slides[value.StartSlide - 1]
                : throw SlideNotFound(value.StartSlide, presentation.Slides.Count),
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
        return new ResolvedSlidesOp(op, slide, shape, slides, layout, shape?.OfficeInteropShapeId);
    }

    /// <summary>
    /// Applies the operation once its targets are still part of the presentation; an
    /// Aspose.Slides failure becomes an engine failure. A file the operation reads is opened
    /// through the SDK, which reports a file it cannot open.
    /// </summary>
    internal long Run()
    {
        if (_target.Failure is not null)
        {
            ExceptionDispatchInfo.Throw(_target.Failure);
        }

        try
        {
            if (_target.Slide is not null && !_presentation.Slides.Contains(_target.Slide)
                || _target.Slides?.Any(slide => !_presentation.Slides.Contains(slide)) == true)
            {
                throw new OperationInvalidException("A targeted slide was deleted by an earlier operation.");
            }
            if (_target.Shape is not null && !_target.Slide!.Shapes.Any(shape => ReferenceEquals(shape, _target.Shape)))
            {
                throw new OperationInvalidException("A targeted shape was deleted by an earlier operation.");
            }

            return _target.Op.Accept(this);
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true)
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
        long? ShapeId,
        Exception? Failure = null);
}
