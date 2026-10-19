using System.Text;
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
        TextReplacement replacement = TextReplacement.Create(
            operation.Find, operation.Replace, operation.Regex, operation.CaseSensitive, operation.WholeWord);
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

            remaining -= Replace(paragraph, replacement, remaining);
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

    private static int Replace(IParagraph paragraph, TextReplacement replacement, int limit)
    {
        IPortion[] portions = paragraph.Portions.ToArray();
        if (portions.Length == 0)
        {
            return 0;
        }

        string[] original = portions.Select(static portion => portion.Text ?? string.Empty).ToArray();
        IReadOnlyList<(int Start, int Length, string Replacement)> matches =
            replacement.Matches(string.Concat(original), limit);
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
            (int start, int length, string text) = matches[match];
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

            // The run holding the first matched character owns the replacement.
            int owner = 0;
            while (owner < original.Length - 1 && start >= starts[owner] + original[owner].Length)
            {
                owner++;
            }

            edited[owner].Insert(start - starts[owner], text);
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

    public ValueTuple Apply(SetNotesOp operation)
    {
        INotesSlide notes = Slide.NotesSlideManager.NotesSlide
            ?? Slide.NotesSlideManager.AddNotesSlide();
        notes.NotesTextFrame!.Text = operation.Text;
        return default;
    }
}
