using System.Globalization;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// Records what each applied operation of a batch should leave in the output, then reads the
/// staged output back and reports every expectation it does not meet; the SDK runner records
/// each applied operation and turns the read-back into the verification. Only effects with a
/// reliable read-back are recorded: the slide order, slide titles, shape text, body paragraphs
/// with their levels and bullets, speaker notes, layouts, hidden slides, footers, transitions,
/// sections, document properties and the slide size, each as the operation left it in the edited
/// presentation, on the slides it resolved when it ran. What an operation writes as text is
/// worked out from its input instead: the text set_title, set_text and set_notes were given, as a
/// read reports it; for replace_text, how often its find matches the text it writes once its
/// replacements are made, and which parts it does not change hold a match. The batch is checked as a whole:
/// an expectation that a later operation changes or removes is dropped and its operation stays
/// unchecked, except that the slide order and the sections a later operation changes again carry
/// the earlier operations into its expectation. An operation counts as checked when every effect
/// it recorded was read back, whether or not the output holds it. Evaluation mode reads longer text cut short, so it records
/// no text, and an operation that sets or replaces text is then left unchecked. Each effect is
/// read as the read projection reports it (<see cref="SlidesStateProjection"/>). The facts say why
/// an operation was left unchecked.
/// </summary>
internal sealed class SlidesEditVerifier : IEditVerifier<SlidesOp>
{
    /// <summary>The expectations a later operation of the same kind builds on, so it carries the earlier operations.</summary>
    private static readonly HashSet<string> Cumulative = new(StringComparer.Ordinal) { "order", "sections" };

    /// <summary>The location of an effect on every slide, or on a slide the output no longer holds.</summary>
    private static readonly Func<Presentation, string> AllSlides = static _ => AllAddress(SlideUnit);

    private const string NoReadBack = "The verification has no read-back for this operation's effect; check it with 'slides read --content full' or 'aspose-cli review'.";
    private const string EvaluationCutsText = "Evaluation mode reads text cut short, so no text is read back; a license lifts the cut.";
    private const string LaterOperationChangedIt = "A later operation of the batch changed what this one set, so its own effect could not be read back.";

    private readonly Presentation _presentation;
    private readonly Func<SlidesOp, SlidesMutationHandlers.ResolvedSlidesOp> _targets;
    private readonly bool _evaluation;
    private readonly Dictionary<string, Expectation> _expected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _carried = new(StringComparer.Ordinal);
    // Why each operation that cannot count as checked was left out.
    private readonly Dictionary<string, string> _incomplete = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recorded = new(StringComparer.Ordinal);
    // What each replace_text should leave, worked out from the presentation before it ran.
    private readonly Dictionary<SlidesOp, string> _before = new(ReferenceEqualityComparer.Instance);

    /// <param name="presentation">The presentation the batch edits, which each record reads as the operation left it.</param>
    /// <param name="targets">Each operation's targets as resolved when it ran, which later operations may move.</param>
    /// <param name="evaluation">Whether the engine runs in evaluation mode, which cuts read text short.</param>
    internal SlidesEditVerifier(Presentation presentation, Func<SlidesOp, SlidesMutationHandlers.ResolvedSlidesOp> targets, bool evaluation)
    {
        _presentation = presentation;
        _targets = targets;
        _evaluation = evaluation;
    }

    /// <summary>Works out what a replace_text should leave from the presentation before it runs.</summary>
    internal void Before(SlidesOp operation)
    {
        if (operation is SlidesReplaceTextOp replace && !_evaluation)
        {
            _before[operation] = Left(_presentation, replace, _evaluation, replacing: true);
        }
    }

    /// <summary>
    /// Records the effect of one operation that applied, against the presentation it left. An
    /// effect it cannot read throws, and the SDK leaves the operation unchecked.
    /// </summary>
    public void Record(SlidesOp operation, AppliedOperation applied)
    {
        string id = operation.Id!;
        _recorded.Add(id);

        // An earlier expectation this operation changed or removed no longer says what the
        // output should hold; its operations stay unchecked, or ride on the next cumulative one.
        foreach ((string key, Expectation expectation) in _expected.ToArray())
        {
            if (!expectation.Unmet && expectation.Read(_presentation) != expectation.Value)
            {
                _expected.Remove(key);
                Drop(key, expectation);
            }
        }

        switch (operation)
        {
            case InsertSlideOp or DeleteSlidesOp or MoveSlideOp or CopySlideOp or ImportSlidesOp:
                Expect(id, "order", SlidesDiagnostics.SlideOrderMismatch, "the slide order", AllSlides, static presentation => string.Join(",", SlideIds(presentation)));
                break;
            case SetSlideHiddenOp or SetLayoutOp:
                ExpectSlides(id, operation, "hidden", "whether the slide is hidden", static slide => slide.Hidden.ToString());
                ExpectSlides(id, operation, "layout", "the slide's layout", Layout);
                break;
            case SetFooterOp:
                ExpectSlides(id, operation, "footer", "the slide's footer", FooterState);
                break;
            case SetTransitionOp:
                ExpectSlides(id, operation, "transition", "the slide's transition", TransitionState);
                break;
            case SetTitleOp title:
                ExpectText(id, title, title.Text, "title", "the slide's title", static slide => Title(slide));
                break;
            case SetNotesOp notes:
                ExpectText(id, notes, notes.Text, "notes", "the slide's speaker notes", static slide => Notes(slide));
                break;
            case SetTextOp text when _targets(text).ShapeId is long shapeId:
                ExpectText(id, text, text.Text, $"shape/{shapeId}", $"the text of shape {Invariant(shapeId)}",
                    slide => slide.Shapes.FirstOrDefault(shape => shape.OfficeInteropShapeId == shapeId) is { } shape ? ShapeText(shape) : null);
                break;
            case SetBodyOp body when _targets(body).ShapeId is long bodyId:
                ExpectOnSlide(id, body, $"shape/{bodyId}", $"the paragraphs of shape {Invariant(bodyId)}",
                    slide => slide.Shapes.FirstOrDefault(shape => shape.OfficeInteropShapeId == bodyId) is { } shape
                        ? ParagraphState(shape)
                        : null);
                break;
            case SlidesReplaceTextOp replace when !_evaluation && _before.Remove(operation, out string? left):
                Expect(id, $"replace/{id}", SlidesDiagnostics.SlideContentMismatch, "the text replace_text left in its scope", AllSlides,
                    presentation => Left(presentation, replace, _evaluation, replacing: false), new Given(left));
                break;
            case InsertSectionOp:
                Expect(id, "sections", SlidesDiagnostics.SlideContentMismatch, "the sections", static _ => AllAddress("section"), static presentation =>
                    string.Join("\n", Sections(presentation).Select(static section => $"{section.Address}|{section.State}")));
                break;
            case SlidesSetPropertiesOp:
                foreach (string name in Properties(_presentation.DocumentProperties).Keys)
                {
                    Expect(id, PropertyAddress(name), SlidesDiagnostics.PresentationSettingMismatch, $"the {name} property", _ => PropertyAddress(name),
                        presentation => Properties(presentation.DocumentProperties)[name]);
                }

                break;
            case SetSlideSizeOp:
                Expect(id, "slide-size", SlidesDiagnostics.PresentationSettingMismatch, "the slide size", static _ => SlideSizeAddress, static presentation =>
                    SlideSize(presentation) is var size ? string.Create(CultureInfo.InvariantCulture, $"{size.Width:0.##}x{size.Height:0.##}") : null);
                break;
            default:
                // Styles, pictures, shapes, tables, charts and backgrounds have no reliable read-back here, nor
                // text an evaluation read cuts short or an operation whose shape or slide is gone.
                _incomplete[id] = operation is SlidesReplaceTextOp ? EvaluationCutsText : NoReadBack;
                break;
        }
    }

    /// <summary>Reads the staged output back against every expectation still standing.</summary>
    internal VerificationFacts Verify(Presentation output)
    {
        var issues = new List<VerificationIssue>();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (Expectation expectation in _expected.Values)
        {
            covered.UnionWith(expectation.Ops);
            if (expectation.Read(output) is var found && found != expectation.Value)
            {
                issues.Add(VerificationIssues.Of(
                    expectation.Code,
                    $"Operation {expectation.Ops[^1]}: the output does not hold {expectation.Subject} the operation left.",
                    expectation.Locate(output),
                    "Read the output back with 'slides read' or 'slides inspect', then apply the operation again."));
            }
        }

        // An operation whose effect was read back is checked whether or not the output holds it;
        // an issue reports each effect it does not hold.
        string[] checkedOps = [.. _recorded.Where(id => covered.Contains(id) && !_incomplete.ContainsKey(id))];
        return new VerificationFacts(issues, checkedOps) { UncheckedReasons = _incomplete };
    }

    /// <summary>
    /// Expects the text an operation was given, as a read reports it (<see cref="AsRead"/>), where
    /// <paramref name="read"/> finds it on the slide the operation resolved.
    /// </summary>
    private void ExpectText(string id, SlideTargetOp operation, string written, string part, string subject, Func<ISlide, string?> read) =>
        ExpectOnSlide(id, operation, part, subject, slide => Clean(read(slide)), new Given(AsRead(written)));

    /// <summary>Expects on the slide the operation resolved what <paramref name="read"/> reads there, or <paramref name="expected"/>.</summary>
    private void ExpectOnSlide(string id, SlideTargetOp operation, string part, string subject, Func<ISlide, string?> read, Given? expected = null)
    {
        if (_evaluation || TargetSlideId(operation) is not uint slideId)
        {
            _incomplete[id] = _evaluation ? EvaluationCutsText : NoReadBack;
            return;
        }

        ExpectSlide(id, part, subject, slideId, read, expected);
    }

    /// <summary>Expects what <paramref name="read"/> reads on each slide a multi-slide operation selected when it ran.</summary>
    private void ExpectSlides(string id, SlidesOp operation, string part, string subject, Func<ISlide, string?> read)
    {
        foreach (ISlide selected in _targets(operation).Slides ?? [])
        {
            ExpectSlide(id, part, subject, selected.SlideId, read);
        }
    }

    private void ExpectSlide(string id, string part, string subject, uint slideId, Func<ISlide, string?> read, Given? expected = null) =>
        Expect(id, $"{part}/{slideId}", SlidesDiagnostics.SlideContentMismatch, subject,
            presentation => Slide(presentation, slideId) is { } slide ? SlideAddress(presentation.Slides.IndexOf(slide) + 1) : AllSlides(presentation),
            presentation => Slide(presentation, slideId) is { } slide ? read(slide) : null,
            expected);

    /// <summary>
    /// Expects under <paramref name="key"/> the value worked out from the operation's input, or
    /// without one the value <paramref name="read"/> reads now, replacing an earlier expectation of
    /// the key; <paramref name="locate"/> gives the effect's target address in the output read
    /// back, which an issue reports as its location.
    /// </summary>
    private void Expect(string id, string key, DiagnosticDescriptor code, string subject, Func<Presentation, string> locate, Func<Presentation, string?> read, Given? expected = null)
    {
        if (_expected.Remove(key, out Expectation? earlier))
        {
            Drop(key, earlier);
        }

        List<string> ops = _carried.Remove(key, out List<string>? carried) ? carried : [];
        ops.Add(id);
        string? now = read(_presentation);
        _expected[key] = expected is { } given
            ? new Expectation(ops, code, subject, locate, read, given.Value) { Unmet = given.Value != now }
            : new Expectation(ops, code, subject, locate, read, now);
    }

    /// <summary>
    /// Drops an expectation a later operation changed: a cumulative one's operations ride on the
    /// next expectation of the same key, any other's stay unchecked.
    /// </summary>
    private void Drop(string key, Expectation expectation)
    {
        if (Cumulative.Contains(key))
        {
            if (!_carried.TryGetValue(key, out List<string>? carried))
            {
                _carried[key] = carried = [];
            }

            carried.AddRange(expectation.Ops);
            return;
        }

        foreach (string id in expectation.Ops)
        {
            _incomplete[id] = LaterOperationChangedIt;
        }
    }

    /// <summary>The id of the slide a slide operation addressed, resolved when it ran.</summary>
    private uint? TargetSlideId(SlideTargetOp operation) => _targets(operation).Slide?.SlideId;

    /// <summary>
    /// What replace_text leaves in its scope: how often its find matches the text it writes
    /// (<see cref="SlidesTextUnits.Writes"/>), and which parts it does not change hold a match, by
    /// location and part. <paramref name="replacing"/> first makes its replacements, as it would,
    /// up to its most replacements, in the text it writes.
    /// </summary>
    private static string Left(Presentation presentation, SlidesReplaceTextOp replace, bool evaluation, bool replacing)
    {
        TextReplacement replacement = TextReplacement.Create(replace.Find, replace.Replace, replace.Regex, replace.CaseSensitive, replace.WholeWord);
        int remaining = replace.MaxReplacementCount ?? int.MaxValue;
        long found = 0;
        var parts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (SlidesTextUnit unit in SlidesTextUnits.Of(presentation, replace.Scope))
        {
            bool written = SlidesTextUnits.Writes(unit, evaluation);
            if (unit.Part is null && !written)
            {
                continue;
            }

            foreach (string text in unit.Texts)
            {
                string after = text;
                if (replacing && written && remaining > 0)
                {
                    (after, int replaced) = replacement.Replace(text, remaining);
                    remaining -= replaced;
                }

                int count = after.Length == 0 ? 0 : replacement.Matches(after, int.MaxValue).Count;
                if (written)
                {
                    found += count;
                }
                else if (count > 0)
                {
                    parts.Add($"{unit.Location}|{unit.Part}");
                }
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{found}|{string.Join(",", parts)}");
    }

    private static ISlide? Slide(Presentation presentation, uint slideId) =>
        presentation.Slides.FirstOrDefault(slide => slide.SlideId == slideId);

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A value worked out from an operation's input, which may be none, such as text that reads as nothing.</summary>
    private readonly record struct Given(string? Value);

    /// <summary>One effect the output should hold: the operations it checks, where it is, what reads it, and the value they left.</summary>
    private sealed record Expectation(
        IReadOnlyList<string> Ops,
        DiagnosticDescriptor Code,
        string Subject,
        Func<Presentation, string> Locate,
        Func<Presentation, string?> Read,
        string? Value)
    {
        /// <summary>Whether the presentation the operation left already differs from the value, which a later operation does not excuse.</summary>
        public bool Unmet { get; init; }
    }
}
