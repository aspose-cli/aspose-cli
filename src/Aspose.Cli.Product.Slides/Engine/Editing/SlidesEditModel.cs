using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// The presentation a <c>slides edit</c> batch changes, as the SDK runner applies operations to
/// it and snapshots them (<see cref="SlidesSnapshots"/>). Every operation's targets are resolved
/// against the presentation as it was before the first operation, so an operation addresses
/// what the caller read, not what earlier operations made.
/// </summary>
internal sealed class SlidesEditModel : IOperationModel<SlidesOp>
{
    private readonly InputSource _inputs;
    private readonly SlidesPresentationLoader _loader;
    private readonly Presentation _presentation;
    private readonly Dictionary<SlidesOp, SlidesMutationHandlers.ResolvedSlidesOp> _targets = new(ReferenceEqualityComparer.Instance);
    private readonly bool _evaluation;
    private readonly IReadOnlyDictionary<string, Secret>? _secrets;
    private readonly Action _readInput;
    private readonly List<Warning> _warnings = [];

    // The operations that left text alone because evaluation mode read it cut short.
    private readonly HashSet<SlidesOp> _truncated = new(ReferenceEqualityComparer.Instance);

    // The operation that applied last: the snapshot after it reads what it left, even when it
    // deleted its own target.
    private SlidesOp? _applied;

    /// <param name="inputs">Reads and charges the files that operations read.</param>
    /// <param name="loader">Opens the presentations that operations import.</param>
    /// <param name="presentation">The presentation being edited.</param>
    /// <param name="batch">The validated operations, whose targets are resolved now.</param>
    /// <param name="evaluation">Whether the engine runs in evaluation mode, which cuts text short when it is read.</param>
    /// <param name="secrets">The secrets the operations name by environment variable.</param>
    /// <param name="readInput">Tells the write pipeline that an operation read the presentation's text.</param>
    internal SlidesEditModel(
        InputSource inputs,
        SlidesPresentationLoader loader,
        Presentation presentation,
        IReadOnlyList<SlidesOp> batch,
        bool evaluation,
        IReadOnlyDictionary<string, Secret>? secrets,
        Action readInput)
    {
        _inputs = inputs;
        _loader = loader;
        _presentation = presentation;
        _evaluation = evaluation;
        _secrets = secrets;
        _readInput = readInput;
        foreach (SlidesMutationHandlers.ResolvedSlidesOp target in SlidesMutationHandlers.ResolveBatch(presentation, batch))
        {
            _targets[target.Op] = target;
        }
    }

    /// <summary>The operation's targets as resolved before the batch: the slides and shape it addresses, whatever earlier operations moved.</summary>
    internal SlidesMutationHandlers.ResolvedSlidesOp Target(SlidesOp operation) => _targets[operation];

    /// <summary>What the operations reported about their results, such as a background they reset.</summary>
    internal IReadOnlyList<Warning> Warnings => _warnings;

    public OperationSnapshot Snapshot(SlidesOp operation)
    {
        SlidesMutationHandlers.ResolvedSlidesOp target = _targets[operation];
        if (!ReferenceEquals(operation, _applied))
        {
            SlidesMutationHandlers.EnsureLive(_presentation, target);
        }

        return SlidesMutationHandlers.Engine(() => operation.Accept(new SlidesSnapshots(_presentation, target)));
    }

    public void Apply(SlidesOp operation)
    {
        _applied = operation;
        new SlidesMutationHandlers(_inputs, _loader, _presentation, _targets[operation], _warnings, _evaluation, _secrets, () =>
        {
            _truncated.Add(operation);
            _readInput();
        }).Run();
    }

    /// <summary>
    /// What a failed operation addressed: its shape, its slide or slides as resolved before the
    /// batch, the slides it names, or the part of the presentation it changes.
    /// </summary>
    public IReadOnlyList<string> AttemptedTargets(SlidesOp operation)
    {
        SlidesMutationHandlers.ResolvedSlidesOp target = _targets[operation];
        int Number(ISlide slide) => _presentation.Slides.IndexOf(slide) + 1;
        if (operation is InsertSectionOp section)
        {
            return [SlidesSnapshots.SectionAddress(section.Name)];
        }

        if (target.Slide is { } slide && _presentation.Slides.Contains(slide))
        {
            return target.ShapeId is long shapeId
                ? [SlidesSnapshots.ShapeAddress(Number(slide), shapeId)]
                : [SlidesSnapshots.Address(Number(slide))];
        }

        if (target.Slides?.Where(_presentation.Slides.Contains).ToArray() is { Length: > 0 } slides)
        {
            return [.. slides.Select(item => SlidesSnapshots.Address(Number(item)))];
        }

        return operation switch
        {
            SlideTargetOp { Slide: int number } => [SlidesSnapshots.Address(number)],
            SlidesSetPropertiesOp => ["property/all"],
            SetSlideSizeOp => ["presentation/slide-size"],
            _ => [$"{SlidesSnapshots.SlideUnit}/all"],
        };
    }

    public NoEffect? Explain(SlidesOp operation) => operation switch
    {
        // Like a verification issue, it never repeats the find text.
        SlidesReplaceTextOp replace when _truncated.Contains(operation) => new NoEffect(NoEffectReasons.EvaluationTruncated,
            $"Operation {operation.Id} (replace_text) replaced nothing: evaluation mode read text in scope '{replace.Scope}' cut short, and text it cut is left as it is.")
        {
            Hint = "Apply an Aspose.Slides license and run the edit again; a search in evaluation mode reads the same cut text.",
        },
        SlidesReplaceTextOp replace => new NoEffect(NoEffectReasons.NoMatch,
            $"Operation {operation.Id} (replace_text) matched no text in scope '{replace.Scope}', so nothing was replaced.")
        {
            Hint = "Search with 'slides search' and the same pattern and scope; --scope all also covers speaker notes. "
                + "Chart text and alternative text are never matched.",
        },
        _ => null,
    };
}
