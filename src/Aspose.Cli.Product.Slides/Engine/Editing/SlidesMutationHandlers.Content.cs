using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesEvaluationMarks;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

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

        if (operation.Scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All)
        {
            WarnUnreplacedChartTitles(operation, replacement);
        }

        return default;
    }

    /// <summary>
    /// Names each chart on a slide whose title holds a match, which replace_text leaves as it is:
    /// chart titles and data are not text it reaches. A title matches as a paragraph of shape text
    /// does. The message never repeats the find text or the title.
    /// </summary>
    private void WarnUnreplacedChartTitles(SlidesReplaceTextOp operation, TextReplacement replacement)
    {
        for (int number = 1; number <= _presentation.Slides.Count; number++)
        {
            foreach (IChart chart in _presentation.Slides[number - 1].Shapes.OfType<IChart>()
                .Where(chart => TitleMatches(chart, replacement)))
            {
                string rect = string.Create(CultureInfo.InvariantCulture,
                    $$"""{"x":{{chart.X:0.##}},"y":{{chart.Y:0.##}},"width":{{chart.Width:0.##}},"height":{{chart.Height:0.##}}}""");
                _warnings.Add(new Warning(SlidesDiagnostics.ChartTitleNotReplaced, string.Create(CultureInfo.InvariantCulture,
                    $"Operation {operation.Id} (replace_text) found a match in the title of chart '{chart.Name}' (shapeId {chart.OfficeInteropShapeId}) on slide {number} and left the title as it was: replace_text does not change chart titles or chart data."))
                {
                    Hint = string.Create(CultureInfo.InvariantCulture,
                        $$"""To change the title, rebuild the chart: read its kind, categories and series with 'slides read --slides {{number}} --content full', apply {"op":"delete_shape","slide":{{number}},"shapeId":{{chart.OfficeInteropShapeId}}}, then insert_chart on slide {{number}} with the same data, the new title and "rect":{{rect}}. The new chart takes the automatic chart style."""),
                    Location = SlidesStateProjection.ShapeAddress(number, chart.OfficeInteropShapeId),
                    Details = new JsonObject { ["id"] = operation.Id, ["op"] = "replace_text" },
                });
            }
        }
    }

    // Whether a paragraph of the chart's title holds a match, read as Replace reads a paragraph.
    private static bool TitleMatches(IChart chart, TextReplacement replacement) =>
        chart.HasTitle
        && chart.ChartTitle.TextFrameForOverriding is { } title
        && title.Paragraphs.Any(paragraph =>
            replacement.Matches(string.Concat(paragraph.Portions.Select(static portion => portion.Text ?? string.Empty)), 1).Count > 0);

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
