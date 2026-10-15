using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Text and speaker notes.
internal sealed partial class SlidesMutationHandlers
{
    public ValueTuple Apply(SetTextOp operation)
    {
        if (Shape is not IAutoShape { TextFrame: not null } auto)
        {
            throw new OperationInvalidException($"Shape {Shape.OfficeInteropShapeId} has no editable text frame.");
        }

        auto.TextFrame.Text = operation.Text;
        return default;
    }

    /// <summary>
    /// Replaces matches inside each paragraph of every shape (including table cells, group
    /// children and SmartArt nodes) and speaker notes. Only the matched characters change:
    /// replacement text takes the formatting of the first matched character, and every
    /// other run and paragraph keeps its own formatting. Evaluation mode reads longer text as
    /// its first characters and a truncation notice: a paragraph it cut short is left as it is,
    /// since writing back what was read would lose the rest of its text, and the result
    /// discloses the cut with EVALUATION_INPUT_TRUNCATED. The watermark text box an evaluation
    /// save added is left alone. With a maximum count, the first matches in slide, shape and
    /// paragraph order are replaced.
    /// </summary>
    public ValueTuple Apply(SlidesReplaceTextOp operation)
    {
        Regex? regex = operation.Regex ? SafeRegex.Create(operation.Find, operation.CaseSensitive) : null;
        if (_evaluation && _presentation.Slides.SelectMany(slide => Frames(slide, operation.Scope)).Any(static frame => CutByEvaluation(frame.Text)))
        {
            _readInput();
        }

        int remaining = operation.MaxReplacementCount ?? int.MaxValue;
        foreach (IParagraph paragraph in _presentation.Slides
            .SelectMany(slide => Frames(slide, operation.Scope))
            .SelectMany(static frame => frame.Paragraphs)
            .Where(paragraph => !_evaluation || !CutByEvaluation(paragraph.Text))
            .ToArray())
        {
            if (remaining == 0)
            {
                break;
            }

            remaining -= Replace(paragraph, operation, regex, remaining);
        }

        return default;
    }

    /// <summary>The text frames of a slide that a search scope reaches: its shapes, its speaker notes, or both.</summary>
    private IEnumerable<ITextFrame> Frames(ISlide slide, string scope)
    {
        IEnumerable<ITextFrame> shapes = scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All
            ? slide.Shapes.Where(shape => !_evaluation || !IsEvaluationWatermark(shape)).SelectMany(TextFrames)
            : [];
        return scope is SlidesSearchScopes.Notes or SlidesSearchScopes.All
            && slide.NotesSlideManager.NotesSlide?.NotesTextFrame is { } notes
            ? shapes.Append(notes)
            : shapes;
    }

    private static int Replace(IParagraph paragraph, SlidesReplaceTextOp op, Regex? regex, int limit)
    {
        IPortion[] portions = paragraph.Portions.ToArray();
        if (portions.Length == 0)
        {
            return 0;
        }

        string[] original = portions.Select(static portion => portion.Text ?? string.Empty).ToArray();
        IReadOnlyList<(int Start, int Length, string Replacement)> matches =
            Matches(string.Concat(original), op, regex, limit);
        if (matches.Count == 0)
        {
            return 0;
        }

        var starts = new int[original.Length];
        for (int index = 1; index < original.Length; index++)
        {
            starts[index] = starts[index - 1] + original[index - 1].Length;
        }

        // Later matches are applied first, so the original offsets of earlier ones stay valid.
        StringBuilder[] edited = original.Select(static text => new StringBuilder(text)).ToArray();
        for (int match = matches.Count - 1; match >= 0; match--)
        {
            (int start, int length, string replacement) = matches[match];
            int end = start + length;
            for (int index = original.Length - 1; index >= 0; index--)
            {
                int from = Math.Max(start, starts[index]);
                int to = Math.Min(end, starts[index] + original[index].Length);
                if (from < to)
                {
                    edited[index].Remove(from - starts[index], to - from);
                }
            }

            // The run holding the first matched character owns the replacement; an empty
            // match at the paragraph end belongs to the last run.
            int owner = 0;
            while (owner < original.Length - 1 && start >= starts[owner] + original[owner].Length)
            {
                owner++;
            }

            edited[owner].Insert(start - starts[owner], replacement);
        }

        for (int index = 0; index < portions.Length; index++)
        {
            string value = edited[index].ToString();
            if (value.Length == 0 && original[index].Length > 0 && paragraph.Portions.Count > 1)
            {
                paragraph.Portions.Remove(portions[index]);
            }
            else if (!string.Equals(value, original[index], StringComparison.Ordinal))
            {
                portions[index].Text = value;
            }
        }

        return matches.Count;
    }

    /// <summary>The first <paramref name="limit"/> matches in a paragraph's text, each a whole word when the operation asks for it.</summary>
    private static IReadOnlyList<(int Start, int Length, string Replacement)> Matches(
        string text,
        SlidesReplaceTextOp op,
        Regex? regex,
        int limit)
    {
        if (regex is not null)
        {
            return regex.Matches(text)
                .Where(match => !op.WholeWord || IsWholeWord(text, match.Index, match.Length))
                .Take(limit)
                .Select(match => (match.Index, match.Length, match.Result(op.Replace)))
                .ToArray();
        }

        StringComparison comparison = op.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = new List<(int, int, string)>();
        for (int found = text.IndexOf(op.Find, comparison); found >= 0 && matches.Count < limit;)
        {
            if (op.WholeWord && !IsWholeWord(text, found, op.Find.Length))
            {
                found = text.IndexOf(op.Find, found + 1, comparison);
                continue;
            }

            matches.Add((found, op.Find.Length, op.Replace));
            found = text.IndexOf(op.Find, found + op.Find.Length, comparison);
        }

        return matches;
    }

    /// <summary>Whether no letter, digit or underscore adjoins the text at <paramref name="start"/> on either side.</summary>
    private static bool IsWholeWord(string text, int start, int length) =>
        (start == 0 || !IsWordCharacter(text[start - 1]))
        && (start + length >= text.Length || !IsWordCharacter(text[start + length]));

    private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

    public ValueTuple Apply(SetNotesOp operation)
    {
        INotesSlide notes = Slide.NotesSlideManager.NotesSlide
            ?? Slide.NotesSlideManager.AddNotesSlide();
        notes.NotesTextFrame!.Text = operation.Text;
        return default;
    }
}
