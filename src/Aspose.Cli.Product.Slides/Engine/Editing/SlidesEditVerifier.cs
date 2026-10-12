using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

/// <summary>
/// Records what each applied operation of a batch should leave in the output, then reads the
/// staged output back and reports every expectation it does not meet; the SDK runner records
/// each applied operation and turns the read-back into the verification. Only effects with a
/// reliable read-back are recorded: the slide order, slide titles, shape text, body paragraphs
/// with their levels and bullets, speaker notes, layouts, hidden slides, footers, transitions,
/// sections, document properties, the slide size and, for replace_text, how often its find and
/// replacement text occur in its scope, each as the operation left it in the edited
/// presentation, on the slides it resolved before the batch. The batch is checked as a whole:
/// an expectation that a later operation changes or removes is dropped and its operation stays
/// unchecked, except that the slide order and the sections a later operation changes again carry
/// the earlier operations into its expectation. An operation counts as checked only when every
/// effect it recorded was read back. Evaluation mode reads longer text cut short, so it records
/// no text, and an operation that sets or replaces text is then left unchecked.
/// </summary>
internal sealed class SlidesEditVerifier : IEditVerifier<SlidesOp>
{
    /// <summary>The expectations a later operation of the same kind builds on, so it carries the earlier operations.</summary>
    private static readonly HashSet<string> Cumulative = new(StringComparer.Ordinal) { "order", "sections" };

    private readonly Presentation _presentation;
    private readonly Func<SlidesOp, SlidesMutationHandlers.ResolvedSlidesOp> _targets;
    private readonly bool _evaluation;
    private readonly Dictionary<string, Expectation> _expected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _carried = new(StringComparer.Ordinal);
    private readonly HashSet<string> _incomplete = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recorded = new(StringComparer.Ordinal);

    /// <param name="presentation">The presentation the batch edits, which each record reads as the operation left it.</param>
    /// <param name="targets">Each operation's targets as resolved before the batch, which earlier operations may have moved.</param>
    /// <param name="evaluation">Whether the engine runs in evaluation mode, which cuts read text short.</param>
    internal SlidesEditVerifier(Presentation presentation, Func<SlidesOp, SlidesMutationHandlers.ResolvedSlidesOp> targets, bool evaluation)
    {
        _presentation = presentation;
        _targets = targets;
        _evaluation = evaluation;
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
            if (expectation.Read(_presentation) != expectation.Value)
            {
                _expected.Remove(key);
                Drop(key, expectation);
            }
        }

        switch (operation)
        {
            case InsertSlideOp or DeleteSlidesOp or MoveSlideOp or CopySlideOp or ImportSlidesOp:
                Expect(id, "order", SlidesDiagnostics.SlideOrderMismatch, "the slide order", null, SlideOrder);
                break;
            case SetSlideHiddenOp or SetLayoutOp:
                foreach (uint slideId in Selected(operation))
                {
                    Expect(id, $"hidden/{slideId}", SlidesDiagnostics.SlideContentMismatch, "whether the slide is hidden", slideId,
                        presentation => Slide(presentation, slideId)?.Hidden.ToString());
                    Expect(id, $"layout/{slideId}", SlidesDiagnostics.SlideContentMismatch, "the slide's layout", slideId,
                        presentation => Slide(presentation, slideId) is { LayoutSlide: { } layout }
                            ? $"{layout.MasterSlide?.Name}|{layout.Name}"
                            : null);
                }

                break;
            case SetFooterOp:
                foreach (uint slideId in Selected(operation))
                {
                    Expect(id, $"footer/{slideId}", SlidesDiagnostics.SlideContentMismatch, "the slide's footer", slideId,
                        presentation => Slide(presentation, slideId) is { } slide ? Footer(slide) : null);
                }

                break;
            case SetTransitionOp:
                foreach (uint slideId in Selected(operation))
                {
                    Expect(id, $"transition/{slideId}", SlidesDiagnostics.SlideContentMismatch, "the slide's transition", slideId,
                        presentation => Slide(presentation, slideId)?.SlideShowTransition is { } transition
                            ? string.Create(CultureInfo.InvariantCulture, $"{transition.Type}|{transition.Duration}")
                            : null);
                }

                break;
            case SetTitleOp title:
                ExpectText(id, title, "title", "the slide's title", static slide => Title(slide));
                break;
            case SetNotesOp notes:
                ExpectText(id, notes, "notes", "the slide's speaker notes", static slide => Notes(slide));
                break;
            case SetTextOp text:
                if (_targets(text).ShapeId is long shapeId)
                {
                    ExpectText(id, text, $"shape/{shapeId}", $"the text of shape {Invariant(shapeId)}",
                        slide => slide.Shapes.FirstOrDefault(shape => shape.OfficeInteropShapeId == shapeId) is { } shape ? ShapeText(shape) : null);
                }
                else
                {
                    _incomplete.Add(id);
                }

                break;
            case SetBodyOp body:
                if (_targets(body).Slide is { } bodySlide)
                {
                    long bodyId = SlidesAuthoring.Body(bodySlide).OfficeInteropShapeId;
                    ExpectText(id, body, $"shape/{bodyId}", $"the paragraphs of shape {Invariant(bodyId)}",
                        slide => slide.Shapes.FirstOrDefault(shape => shape.OfficeInteropShapeId == bodyId) is IAutoShape { TextFrame: { } frame }
                            ? Paragraphs(frame)
                            : null);
                }
                else
                {
                    _incomplete.Add(id);
                }

                break;
            case SlidesReplaceTextOp replace:
                if (_evaluation)
                {
                    _incomplete.Add(id);
                    break;
                }

                Expect(id, $"replace/{id}", SlidesDiagnostics.SlideContentMismatch, "the text replace_text left in its scope", null,
                    presentation => Occurrences(presentation, replace));
                break;
            case InsertSectionOp:
                Expect(id, "sections", SlidesDiagnostics.SlideContentMismatch, "the sections", null, static presentation =>
                    string.Join("\n", presentation.Sections.Select(static section =>
                        $"{section.Name}|{section.StartedFromSlide?.SlideId.ToString(CultureInfo.InvariantCulture) ?? "-"}")));
                break;
            case SlidesSetPropertiesOp:
                foreach (string name in Properties(_presentation.DocumentProperties).Keys)
                {
                    Expect(id, $"property/{name}", SlidesDiagnostics.PresentationSettingMismatch, $"the {name} property", null,
                        presentation => Properties(presentation.DocumentProperties)[name]);
                }

                break;
            case SetSlideSizeOp:
                Expect(id, "slide-size", SlidesDiagnostics.PresentationSettingMismatch, "the slide size", null, static presentation =>
                    string.Create(CultureInfo.InvariantCulture, $"{presentation.SlideSize.Size.Width:0.##}x{presentation.SlideSize.Size.Height:0.##}"));
                break;
            default:
                // Styles, pictures, shapes, tables, charts and backgrounds have no reliable read-back here.
                _incomplete.Add(id);
                break;
        }
    }

    /// <summary>Reads the staged output back against every expectation still standing.</summary>
    internal VerificationFacts Verify(Presentation output)
    {
        var issues = new List<VerificationIssue>();
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (Expectation expectation in _expected.Values)
        {
            covered.UnionWith(expectation.Ops);
            if (expectation.Read(output) is var found && found != expectation.Value)
            {
                failed.UnionWith(expectation.Ops);
                string location = expectation.SlideId is uint slideId
                    && output.Slides.FirstOrDefault(slide => slide.SlideId == slideId) is { } slide
                    ? string.Create(CultureInfo.InvariantCulture, $"slide {output.Slides.IndexOf(slide) + 1}")
                    : "presentation";
                issues.Add(VerificationIssues.Of(
                    expectation.Code,
                    $"Operation {expectation.Ops[^1]}: the output does not hold {expectation.Subject} the operation left.",
                    location,
                    "Read the output back with 'slides read' or 'slides inspect', then apply the operation again."));
            }
        }

        string[] checkedOps = [.. _recorded.Where(id => covered.Contains(id) && !failed.Contains(id) && !_incomplete.Contains(id))];
        return new VerificationFacts(issues, checkedOps);
    }

    private void ExpectText(string id, SlideTargetOp operation, string part, string subject, Func<ISlide, string?> read)
    {
        if (_evaluation || TargetSlideId(operation) is not uint slideId)
        {
            _incomplete.Add(id);
            return;
        }

        Expect(id, $"{part}/{slideId}", SlidesDiagnostics.SlideContentMismatch, subject, slideId,
            presentation => Slide(presentation, slideId) is { } slide ? read(slide) : null);
    }

    private void Expect(string id, string key, DiagnosticDescriptor code, string subject, uint? slideId, Func<Presentation, string?> read)
    {
        if (_expected.Remove(key, out Expectation? earlier))
        {
            Drop(key, earlier);
        }

        List<string> ops = _carried.Remove(key, out List<string>? carried) ? carried : [];
        ops.Add(id);
        _expected[key] = new Expectation(ops, code, subject, slideId, read, read(_presentation));
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

        _incomplete.UnionWith(expectation.Ops);
    }

    /// <summary>The id of the slide a slide operation addressed, resolved before the batch.</summary>
    private uint? TargetSlideId(SlideTargetOp operation) => _targets(operation).Slide?.SlideId;

    /// <summary>The ids of the slides a multi-slide operation selects, resolved before the batch.</summary>
    private IEnumerable<uint> Selected(SlidesOp operation) =>
        (_targets(operation).Slides ?? []).Select(static slide => slide.SlideId);

    private static string SlideOrder(Presentation presentation) =>
        string.Join(",", presentation.Slides.Select(static slide => slide.SlideId.ToString(CultureInfo.InvariantCulture)));

    private static string Footer(ISlide slide) =>
        $"{slide.HeaderFooterManager.IsFooterVisible}|{slide.HeaderFooterManager.IsSlideNumberVisible}|{slide.HeaderFooterManager.IsDateTimeVisible}|"
        + string.Join("\n", slide.Shapes
            .Where(static shape => shape.Placeholder?.Type == PlaceholderType.Footer)
            .Select(static shape => (shape as IAutoShape)?.TextFrame?.Text));

    private static string Paragraphs(ITextFrame frame) =>
        string.Join("\n", frame.Paragraphs.Select(static paragraph => string.Create(CultureInfo.InvariantCulture,
            $"{paragraph.Text}|{paragraph.ParagraphFormat.GetEffective().Depth}|{paragraph.ParagraphFormat.GetEffective().Bullet.Type}")));

    /// <summary>
    /// How often replace_text's find text, and its replacement when it is literal text, occur in
    /// the shapes and notes of its scope: the replaced text is gone and its replacement is there.
    /// </summary>
    private static string Occurrences(Presentation presentation, SlidesReplaceTextOp replace)
    {
        Regex? regex = replace.Regex ? SafeRegex.Create(replace.Find, replace.CaseSensitive) : null;
        StringComparison comparison = replace.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        long found = 0;
        long replacements = 0;
        foreach (ISlide slide in presentation.Slides)
        {
            IEnumerable<string?> texts = replace.Scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All
                ? slide.Shapes.Where(static shape => !IsEvaluationWatermark(shape)).SelectMany(TextFrames).SelectMany(static frame => frame.Paragraphs).Select(static paragraph => paragraph.Text)
                : [];
            if (replace.Scope is SlidesSearchScopes.Notes or SlidesSearchScopes.All
                && slide.NotesSlideManager.NotesSlide?.NotesTextFrame is { } notes)
            {
                texts = texts.Concat(notes.Paragraphs.Select(static paragraph => paragraph.Text));
            }

            foreach (string text in texts.OfType<string>())
            {
                found += regex is not null ? regex.Matches(text).Count : Count(text, replace.Find, comparison);
                if (!replace.Regex && replace.Replace.Length > 0)
                {
                    replacements += Count(text, replace.Replace, comparison);
                }
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{found}|{replacements}");
    }

    private static long Count(string text, string value, StringComparison comparison)
    {
        long count = 0;
        for (int at = text.IndexOf(value, comparison); at >= 0; at = text.IndexOf(value, at + value.Length, comparison))
        {
            count++;
        }

        return count;
    }

    private static ISlide? Slide(Presentation presentation, uint slideId) =>
        presentation.Slides.FirstOrDefault(slide => slide.SlideId == slideId);

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>One effect the output should hold: the operations it checks, what reads it, and the value they left.</summary>
    private sealed record Expectation(
        IReadOnlyList<string> Ops,
        DiagnosticDescriptor Code,
        string Subject,
        uint? SlideId,
        Func<Presentation, string?> Read,
        string? Value);
}
