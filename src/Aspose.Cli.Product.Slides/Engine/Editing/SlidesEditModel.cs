using Aspose.Cli.Sdk.IO.Input;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// The presentation a <c>slides edit</c> batch changes, as the SDK runner applies operations to
/// it and snapshots them (<see cref="SlidesSnapshots"/>). The operations run in order, and each
/// one's targets are resolved just before it runs, against the presentation as the earlier
/// operations left it, so an operation can address a slide an earlier one inserted.
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
    /// <param name="evaluation">Whether the engine runs in evaluation mode, which cuts text short when it is read.</param>
    /// <param name="secrets">The secrets the operations name by environment variable.</param>
    /// <param name="readInput">Tells the write pipeline that an operation read the presentation's text.</param>
    internal SlidesEditModel(
        InputSource inputs,
        SlidesPresentationLoader loader,
        Presentation presentation,
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
    }

    /// <summary>
    /// The operation's targets, resolved the first time it is asked for, just before the
    /// operation runs: the slides and shape it addresses, wherever later operations move them.
    /// </summary>
    internal SlidesMutationHandlers.ResolvedSlidesOp Target(SlidesOp operation)
    {
        if (!_targets.TryGetValue(operation, out SlidesMutationHandlers.ResolvedSlidesOp? target))
        {
            _targets[operation] = target = SlidesMutationHandlers.ResolveTarget(_presentation, operation);
        }

        return target;
    }

    /// <summary>What the operations reported about their results, such as a background they reset.</summary>
    internal IReadOnlyList<Warning> Warnings => _warnings;

    public OperationSnapshot Snapshot(SlidesOp operation)
    {
        SlidesMutationHandlers.ResolvedSlidesOp target = Target(operation);
        if (!ReferenceEquals(operation, _applied))
        {
            SlidesMutationHandlers.EnsureResolved(target);
        }

        // After the operation, evaluation mode may read the text it wrote cut short.
        Action? readCut = _evaluation && ReferenceEquals(operation, _applied) ? _readInput : null;
        return SlidesMutationHandlers.Engine(() => operation.Accept(new SlidesSnapshots(_presentation, target, _evaluation, readCut)));
    }

    public void Apply(SlidesOp operation)
    {
        _applied = operation;
        new SlidesMutationHandlers(_inputs, _loader, _presentation, Target(operation), _warnings, _evaluation, _secrets, () =>
        {
            _truncated.Add(operation);
            _readInput();
        }).Run();
    }

    /// <summary>
    /// What a failed operation addressed: its shape, its slide or slides as resolved when it
    /// ran, the slides it names, or the part of the presentation it changes.
    /// </summary>
    public IReadOnlyList<string> AttemptedTargets(SlidesOp operation)
    {
        SlidesOpTarget named = SlidesOpTargets.Of(operation);
        if (!named.ByResolution)
        {
            return named.Written;
        }

        SlidesMutationHandlers.ResolvedSlidesOp target = Target(operation);
        int Number(ISlide slide) => _presentation.Slides.IndexOf(slide) + 1;
        if (target.Slide is { } slide && _presentation.Slides.Contains(slide))
        {
            return target.ShapeId is long shapeId
                ? [SlidesStateProjection.ShapeAddress(Number(slide), shapeId)]
                : [SlidesStateProjection.SlideAddress(Number(slide))];
        }

        if (target.Slides?.Where(_presentation.Slides.Contains).ToArray() is { Length: > 0 } slides)
        {
            return [.. slides.Select(item => SlidesStateProjection.SlideAddress(Number(item)))];
        }

        return named.Written;
    }

    /// <summary>The slides in order, which number what the batch changed on each.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() =>
        new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal) { [SlidesStateProjection.SlideUnit] = [.. _presentation.Slides] };

    public NoEffect? Explain(SlidesOp operation) => operation switch
    {
        // Like a verification issue, it never repeats the find text.
        SlidesReplaceTextOp replace when _truncated.Contains(operation) => new NoEffect(NoEffectReasons.EvaluationTruncated,
            $"Operation {operation.Id} (replace_text) replaced nothing: evaluation mode read text in scope '{replace.Scope}' cut short, and text it cut is left as it is.")
        {
            Hint = "Apply an Aspose.Slides license and run the edit again; a search in evaluation mode reads the same cut text. "
                + replace.Scope switch
                {
                    SlidesSearchScopes.Notes => "Without one, set_notes replaces a slide's whole speaker notes without reading them.",
                    SlidesSearchScopes.Shapes => "Without one, set_text replaces a shape's whole text without reading it.",
                    _ => "Without one, set_text replaces a shape's whole text and set_notes a slide's whole speaker notes without reading them.",
                },
        },
        SlidesReplaceTextOp replace => new NoEffect(NoEffectReasons.NoMatch,
            $"Operation {operation.Id} (replace_text) matched no text in scope '{replace.Scope}', so nothing was replaced.")
        {
            Hint = "Search with 'slides search' and the same pattern and scope"
                + (replace.Scope == SlidesSearchScopes.All ? ". " : ", or set \"scope\": \"all\" on the op to cover both shape text and speaker notes.")
                + "Chart text and alternative text are never matched.",
        },
        _ => null,
    };
}
