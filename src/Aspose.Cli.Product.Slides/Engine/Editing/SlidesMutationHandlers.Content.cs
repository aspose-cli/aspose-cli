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
        ITextFrame[] frames = [.. SlidesTextUnits.Of(_presentation, operation.Scope)
            .Where(unit => SlidesTextUnits.Writes(unit, _evaluation)).SelectMany(static unit => unit.Frames)];
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

        // Each part it does not change that holds a match is named while the batch leaves one there;
        // a chart's categories and series names in one warning, which names those that still hold one:
        // of its candidates, the one whose parts are exactly those holding a match when the batch ends.
        foreach (SlidesTextUnit[] found in SlidesTextUnits.Of(_presentation, operation.Scope).Where(unit => unit.Part is not null && Holds(unit))
            .GroupBy(static unit => unit.Part == TextPart.ChartData ? unit.Holder! : unit, ReferenceEqualityComparer.Instance).Select(static units => units.ToArray()))
        {
            SlidesTextUnit[][] candidates = found.Length == 1 ? [found] : [[found[0]], [found[1]], found];
            foreach (SlidesTextUnit[] named in candidates)
            {
                (string where, string left, string unchanged, string hint) = Unchanged(named);
                _unreplaced.Add(UnreplacedText.Found(named[0].Part!, operation, "replace_text",
                    $"Operation {operation.Id} (replace_text) found a match in {where} and left {left}: replace_text does not change {unchanged}.",
                    hint, named[0].Location!, () => found.All(unit => named.Contains(unit) == Remains(unit))));
            }
        }

        return default;

        bool Remains(SlidesTextUnit unit) => SlidesTextUnits.Of(_presentation, operation.Scope)
            .Any(now => ReferenceEquals(now.Holder, unit.Holder) && now.Part == unit.Part && now.ChartData == unit.ChartData && Holds(now));

        bool Holds(SlidesTextUnit unit) => unit.Texts.Any(text => text.Length > 0 && replacement.Matches(text, 1).Count > 0);
    }

    /// <summary>
    /// How replace_text names text it found and left, by the kind of part that holds it: where it
    /// found it, what it left, what it does not change and how to change that text. The message
    /// never repeats the find text or the part's text.
    /// </summary>
    private static (string Found, string Left, string Unchanged, string Hint) Unchanged(SlidesTextUnit[] units)
    {
        SlidesTextUnit unit = units[0];
        const string Charts = "chart titles or chart data";
        IChart? chart = unit.Holder as IChart;
        bool grouped = unit.Shape is IGroupShape;
        if (unit.Part == TextPart.ChartTitle)
        {
            return ($"the title of {unit.Where}", "the title as it was", Charts,
                grouped ? GroupedChartHint : RebuildHint(unit.Slide, chart!, "To change the title, rebuild the chart", "the same data, the new title"));
        }

        if (unit.Part == TextPart.ChartData)
        {
            string change = units.Length > 1 ? "the categories and every series as read, each changed where it matches"
                : unit.ChartData == SlidesTextUnits.Categories ? "the categories, changed where they match"
                : "every series as read, values and all, renamed where its name matches";
            return ($"the {string.Join(" and ", units.Select(static item => item.ChartData))} of {unit.Where}", "them as they were", Charts, grouped ? GroupedChartHint : DataHint(unit.Slide, chart!, change));
        }

        return unit.Part == TextPart.AxisTitle ? ($"an axis title of {unit.Where}", "it as it was", "a chart's axis titles", AxisTitleHint)
            : unit.Part == TextPart.DataLabel ? ($"a data label of {unit.Where}", "it as it was", "a chart's data labels", Unchangeable("a chart's data labels"))
            : unit.Part == TextPart.AltText ? ($"the alternative text of {unit.Where}", "it as it was", "alternative text", Unchangeable("the alternative text of a shape"))
            : unit.Part == TextPart.Comment ? (unit.Where, "it as it was", "comments", Unchangeable("comments"))
            : (unit.Where, "it as it was", "the text of layouts and masters", Unchangeable("the text of a layout or master, which every slide on it shows"));
    }

    // The hint for text no operation changes.
    private static string Unchangeable(string what) =>
        $"No operation changes {what}, so it keeps that text: report it as unchanged, or change it in a presentation editor.";

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
