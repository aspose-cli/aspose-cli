using System.Runtime.ExceptionServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// Applies one resolved presentation operation. Each operation has its handler in the
/// structural, content, object or style part of this class; what it changed the SDK derives
/// from the snapshots around it (<see cref="SlidesSnapshots"/>).
/// </summary>
internal sealed partial class SlidesMutationHandlers : ISlidesOpHandler<ValueTuple>
{
    private readonly InputSource _inputs;
    private readonly SlidesPresentationLoader _loader;
    private readonly Presentation _presentation;
    private readonly ResolvedSlidesOp _target;
    private readonly ICollection<Warning> _warnings;
    private readonly bool _evaluation;
    private readonly Action _readInput;
    private readonly IReadOnlyDictionary<string, Secret>? _secrets;

    /// <summary>Creates the handlers of one operation.</summary>
    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="loader">Opens the presentations that operations append.</param>
    /// <param name="presentation">The presentation being edited.</param>
    /// <param name="target">The operation with the targets it resolved just before it runs.</param>
    /// <param name="warnings">Receives what the operation reports about its result.</param>
    /// <param name="evaluation">Whether the engine runs in evaluation mode, which cuts text short when it is read.</param>
    /// <param name="secrets">The secrets the operations name by environment variable.</param>
    /// <param name="readInput">Tells the write pipeline that the operation read the presentation's text, which discloses text evaluation mode cut short.</param>
    internal SlidesMutationHandlers(
        InputSource inputs,
        SlidesPresentationLoader loader,
        Presentation presentation,
        ResolvedSlidesOp target,
        ICollection<Warning> warnings,
        bool evaluation,
        IReadOnlyDictionary<string, Secret>? secrets,
        Action readInput)
    {
        _inputs = inputs;
        _loader = loader;
        _presentation = presentation;
        _target = target;
        _warnings = warnings;
        _evaluation = evaluation;
        _readInput = readInput;
        _secrets = secrets;
    }

    /// <summary>The resolved target slide of a slide or shape operation.</summary>
    private ISlide Slide => _target.Slide!;

    /// <summary>The resolved target shape of a shape operation.</summary>
    private IShape Shape => _target.Shape!;

    /// <summary>The resolved target slides of a multi-slide operation.</summary>
    private IReadOnlyList<ISlide> Slides => _target.Slides!;

    /// <summary>
    /// Resolves an operation's targets against the presentation as the earlier operations of the
    /// batch left it, just before the operation runs, so it can address a slide an earlier one
    /// inserted. A target that cannot be resolved becomes the operation's failure, raised when
    /// the operation runs so the batch reports it at that operation's position.
    /// </summary>
    internal static ResolvedSlidesOp ResolveTarget(Presentation presentation, SlidesOp op)
    {
        try
        {
            return Resolve(presentation, op);
        }
        catch (Exception failure) when (failure is CliException or OperationInvalidException)
        {
            return new ResolvedSlidesOp(op, null, null, null, null, null, failure);
        }
    }

    private static ResolvedSlidesOp Resolve(Presentation presentation, SlidesOp op)
    {
        ISlide? slide = op switch
        {
            SlideTargetOp target => ResolveSlide(presentation, target),
            InsertSectionOp value => value.At <= presentation.Slides.Count
                ? presentation.Slides[value.At - 1]
                : throw SlideNotFound(value.At, presentation.Slides.Count),
            _ => null,
        };
        IShape? shape = op is ShapeTargetOp shapeTarget
            ? ResolveShape(slide!, shapeTarget)
            : null;
        IReadOnlyList<ISlide>? slides = op switch
        {
            DeleteSlidesOp value => ResolveSlides(presentation, value.Slides),
            SetSlideHiddenOp value => ResolveSlides(presentation, value.Slides),
            SetLayoutOp value => ResolveSlides(presentation, value.Slides),
            SetBackgroundOp value => ResolveOptionalSlides(presentation, value.Slides),
            SetFooterOp value => ResolveOptionalSlides(presentation, value.Slides),
            SetTransitionOp value => ResolveSlides(presentation, value.Slides),
            _ => null,
        };
        ILayoutSlide? layout = op switch
        {
            InsertSlideOp { Layout: not null } value => ResolveLayout(presentation, value.Layout),
            SetLayoutOp value => ResolveLayout(presentation, value.Layout),
            _ => null,
        };
        // Detached shapes may no longer expose their SDK identity. Keep the receipt target
        // aligned with the original presentation used to resolve every operation.
        return new ResolvedSlidesOp(op, slide, shape, slides, layout, shape?.OfficeInteropShapeId);
    }

    /// <summary>
    /// Applies the operation once its targets resolved; an Aspose.Slides failure becomes an
    /// engine failure. A file the operation reads is opened through the SDK, which reports a
    /// file it cannot open.
    /// </summary>
    internal void Run()
    {
        EnsureResolved(_target);
        Engine(() => _target.Op.Accept(this));
    }

    /// <summary>Refuses an operation whose target could not be resolved.</summary>
    internal static void EnsureResolved(ResolvedSlidesOp target)
    {
        if (target.Failure is not null)
        {
            ExceptionDispatchInfo.Throw(target.Failure);
        }
    }

    /// <summary>Runs engine work, turning an Aspose.Slides failure into an engine failure.</summary>
    internal static T Engine<T>(Func<T> work)
    {
        try
        {
            return work();
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
