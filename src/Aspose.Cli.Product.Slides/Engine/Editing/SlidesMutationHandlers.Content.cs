using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Text and speaker notes.
internal sealed partial class SlidesMutationHandlers
{
    public long Apply(SetTextOp operation)
    {
        if (Shape is not IAutoShape { TextFrame: not null } auto)
        {
            throw new OperationInvalidException($"Shape {Shape.OfficeInteropShapeId} has no editable text frame.");
        }

        auto.TextFrame.Text = operation.Text;
        _touched.Add(Slide.SlideId);
        return 1;
    }

    /// <summary>
    /// Replaces matches inside each paragraph of every shape (including table cells, group
    /// children and SmartArt nodes) and speaker notes. Only the matched characters change:
    /// replacement text takes the formatting of the first matched character, and every
    /// other run and paragraph keeps its own formatting.
    /// </summary>
    public long Apply(SlidesReplaceTextOp operation)
    {
        Regex? regex = operation.Regex ? SafeRegex.Create(operation.Find, operation.MatchCase) : null;
        long count = 0;
        foreach (ISlide slide in _presentation.Slides)
        {
            var frames = new List<ITextFrame>();
            if (operation.Scope is PresentationSearchScopes.Shapes or PresentationSearchScopes.All)
            {
                frames.AddRange(slide.Shapes.SelectMany(TextFrames));
            }

            if (operation.Scope is PresentationSearchScopes.Notes or PresentationSearchScopes.All
                && slide.NotesSlideManager.NotesSlide?.NotesTextFrame is { } notes)
            {
                frames.Add(notes);
            }

            long replaced = frames
                .SelectMany(static frame => frame.Paragraphs)
                .Sum(paragraph => (long)Replace(paragraph, operation, regex));
            if (replaced > 0)
            {
                count += replaced;
                _touched.Add(slide.SlideId);
            }
        }

        return count;
    }

    private static int Replace(IParagraph paragraph, SlidesReplaceTextOp op, Regex? regex)
    {
        IPortion[] portions = paragraph.Portions.ToArray();
        if (portions.Length == 0)
        {
            return 0;
        }

        string[] original = portions.Select(static portion => portion.Text ?? string.Empty).ToArray();
        IReadOnlyList<(int Start, int Length, string Replacement)> matches =
            Matches(string.Concat(original), op, regex);
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

    private static IReadOnlyList<(int Start, int Length, string Replacement)> Matches(
        string text,
        SlidesReplaceTextOp op,
        Regex? regex)
    {
        if (regex is not null)
        {
            return regex.Matches(text)
                .Select(match => (match.Index, match.Length, match.Result(op.Replace)))
                .ToArray();
        }

        StringComparison comparison = op.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = new List<(int, int, string)>();
        for (int found = text.IndexOf(op.Find, comparison);
            found >= 0;
            found = text.IndexOf(op.Find, found + op.Find.Length, comparison))
        {
            matches.Add((found, op.Find.Length, op.Replace));
        }

        return matches;
    }

    public long Apply(SetNotesOp operation)
    {
        INotesSlide notes = Slide.NotesSlideManager.NotesSlide
            ?? Slide.NotesSlideManager.AddNotesSlide();
        notes.NotesTextFrame!.Text = operation.Text;
        _touched.Add(Slide.SlideId);
        return 1;
    }
}
