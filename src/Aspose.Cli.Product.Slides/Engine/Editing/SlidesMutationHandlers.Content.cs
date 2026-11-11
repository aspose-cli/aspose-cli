using System.Globalization;
using System.Text;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesEvaluationMarks;

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
        // The watermark an evaluation save added is left alone.
        ITextFrame[] frames = [.. SlidesTextUnits.Of(_presentation, operation.Scope)
            .Where(unit => !_evaluation || !unit.IsEvaluationWatermark).SelectMany(static unit => unit.Frames)];
        if (_evaluation && frames.Any(static frame => CutByEvaluation(frame.Text)))
        {
            _readInput();
        }

        int remaining = operation.MaxReplacementCount ?? int.MaxValue;
        foreach (IParagraph paragraph in frames
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
            WarnUnreplacedChartText(operation, replacement);
        }

        return default;
    }

    /// <summary>
    /// Names each chart on a slide, inside a group or not, whose title, axis titles or data hold a
    /// match, which replace_text leaves as it is: chart text is not text it reaches. The title
    /// warns as part <c>chart-title</c>, the axis titles as <c>axis-title</c>, the categories and
    /// series names as <c>chart-data</c>; each matches as a paragraph of shape text does, and is
    /// named only if the chart is still in the presentation and that part of it still holds a
    /// match once the batch has run. The message never repeats the find text or the chart's text.
    /// </summary>
    private void WarnUnreplacedChartText(SlidesReplaceTextOp operation, TextReplacement replacement)
    {
        for (int number = 1; number <= _presentation.Slides.Count; number++)
        {
            foreach ((IChart chart, IGroupShape? group) in SlidesTextUnits.Charts(_presentation.Slides[number - 1].Shapes))
            {
                string chartName = string.Create(CultureInfo.InvariantCulture, $"chart '{chart.Name}' (shapeId {chart.OfficeInteropShapeId})");
                string where = group is null
                    ? string.Create(CultureInfo.InvariantCulture, $"{chartName} on slide {number}")
                    : string.Create(CultureInfo.InvariantCulture, $"{chartName} in group '{group.Name}' (shapeId {group.OfficeInteropShapeId}) on slide {number}");
                string location = SlidesStateProjection.ShapeAddress(number, chart.OfficeInteropShapeId);
                Func<bool> Remains(Func<bool> holds) => () => _presentation.Slides
                    .SelectMany(static slide => SlidesTextUnits.Charts(slide.Shapes)).Any(found => ReferenceEquals(found.Chart, chart)) && holds();
                if (TitleMatches(chart, replacement))
                {
                    _unreplaced.Add(UnreplacedText.Found(TextPart.ChartTitle, operation, "replace_text",
                        $"Operation {operation.Id} (replace_text) found a match in the title of {where} and left the title as it was: replace_text does not change chart titles or chart data.",
                        group is null ? RebuildHint(number, chart, "To change the title, rebuild the chart", "the same data, the new title") : GroupedChartHint,
                        location, Remains(() => TitleMatches(chart, replacement))));
                }

                if (AxisTitleMatches(chart, replacement))
                {
                    _unreplaced.Add(UnreplacedText.Found(TextPart.AxisTitle, operation, "replace_text",
                        $"Operation {operation.Id} (replace_text) found a match in an axis title of {where} and left it as it was: replace_text does not change a chart's axis titles.",
                        AxisTitleHint,
                        location, Remains(() => AxisTitleMatches(chart, replacement))));
                }

                if (DataMatched(chart, replacement) is { } data)
                {
                    _unreplaced.Add(UnreplacedText.Found(TextPart.ChartData, operation, "replace_text",
                        $"Operation {operation.Id} (replace_text) found a match in the {data.Parts} of {where} and left them as they were: replace_text does not change chart titles or chart data.",
                        group is not null ? GroupedChartHint : DataHint(number, chart, data.Change),
                        location, Remains(() => DataMatched(chart, replacement) is not null)));
                }
            }
        }
    }

    // No operation addresses a shape inside a group, and slides read reports only a slide's own shapes.
    private const string GroupedChartHint =
        "No operation addresses a shape inside a group, so the chart keeps that text: report it as unchanged, or " + RebuildGroupedChart + ".";

    // A chart inside a group is rebuilt as any chart is (editing.md), with the group it was in.
    private const string RebuildGroupedChart =
        "rebuild the chart outside the group: apply delete_shape to the group, which removes everything the group holds, then insert_chart the chart as it should be and insert again what else the group held; 'slides read' does not report what a group holds";

    // No operation writes an axis title, and a chart insert_chart rebuilds has none.
    private const string AxisTitleHint =
        "No operation changes a chart's axis titles, so the chart keeps that text: report it as unchanged, or change it in a presentation editor.";

    // How to change the data of a chart on the slide itself: set_chart_data, unless it cannot
    // update the chart in place, when the chart is rebuilt as its title would be.
    private static string DataHint(int number, IChart chart, string change) =>
        SlidesChartUpdate.CannotUpdate(chart) is not null
            ? RebuildHint(number, chart, "To change them, rebuild the chart, since its data cannot be updated in place", "the changed data, its title")
            : string.Create(CultureInfo.InvariantCulture,
                $"To change them, read the chart's categories and series with 'slides read --slides {number} --content full', then apply set_chart_data to shapeId {chart.OfficeInteropShapeId} on slide {number} with {change}.");

    // A hint that rebuilds the chart: read it, delete it, then insert it again at its rect.
    private static string RebuildHint(int number, IChart chart, string change, string with)
    {
        string rect = string.Create(CultureInfo.InvariantCulture,
            $$"""{"x":{{chart.X:0.##}},"y":{{chart.Y:0.##}},"width":{{chart.Width:0.##}},"height":{{chart.Height:0.##}}}""");
        return string.Create(CultureInfo.InvariantCulture,
            $$"""{{change}}: read its kind, categories and series with 'slides read --slides {{number}} --content full', apply {"op":"delete_shape","slide":{{number}},"shapeId":{{chart.OfficeInteropShapeId}}}, then insert_chart on slide {{number}} with {{with}} and "rect":{{rect}}. The new chart takes the automatic chart style.""");
    }

    private static bool TitleMatches(IChart chart, TextReplacement replacement) =>
        chart.HasTitle && chart.ChartTitle.TextFrameForOverriding is { } title && FrameMatches(title, replacement);

    private static bool AxisTitleMatches(IChart chart, TextReplacement replacement) =>
        SlidesShapeFormats.AxisTitles(chart).Any(title => FrameMatches(title, replacement));

    // Whether a paragraph of a title holds a match, read as Replace reads a paragraph.
    private static bool FrameMatches(ITextFrame frame, TextReplacement replacement) =>
        frame.Paragraphs.Any(paragraph =>
            replacement.Matches(string.Concat(paragraph.Portions.Select(static portion => portion.Text ?? string.Empty)), 1).Count > 0);

    /// <summary>
    /// Which of the chart's data holds a match, its categories or its series names, as set_chart_data
    /// would change them; null when neither does. A category is read as <c>slides read</c> reads it,
    /// and a series name from its cells or, without them, as its literal text.
    /// </summary>
    private static (string Parts, string Change)? DataMatched(IChart chart, TextReplacement replacement)
    {
        bool categories = chart.ChartData.Categories.Any(category => Matches(replacement, Text(category.Value)));
        bool names = chart.ChartData.Series.Any(series => series.Name.DataSourceType == DataSourceType.Worksheet
            ? series.Name.AsCells.Any(cell => Matches(replacement, Text(cell.Value)))
            : Matches(replacement, series.Name.AsLiteralString));
        return (categories, names) switch
        {
            (true, true) => ("categories and series names", "the categories and every series as read, each changed where it matches"),
            (true, false) => ("categories", "the categories, changed where they match"),
            (false, true) => ("series names", "every series as read, values and all, renamed where its name matches"),
            _ => null,
        };

        static string? Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    // Whether a category or series name holds a match; an empty one holds no text.
    private static bool Matches(TextReplacement replacement, string? text) =>
        !string.IsNullOrEmpty(text) && replacement.Matches(text, 1).Count > 0;

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
