using System.Globalization;
using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The one enumeration of where a presentation holds text, for the commands whose object is the
/// text itself: search, replace_text and the text it leaves, the occurrences --verify counts,
/// extract of text and notes, and whether evaluation mode cut what it read. Each slide gives, in
/// order, each of its own shapes (with its table cells, group children and SmartArt nodes, which
/// replace_text writes) followed by the text of that shape replace_text does not change (the
/// alternative text and the chart titles, axis titles, data and data labels in it), then its
/// speaker notes; with scope <c>all</c>, its comments and the text of the layout and master it
/// is the first slide to show. Commands that report one shape's structure, layout or state read
/// the shapes themselves.
/// </summary>
internal static class SlidesTextUnits
{
    /// <summary>The units of every slide a search scope reaches: its shapes, notes, comments, layout or master, or all of them.</summary>
    internal static IReadOnlyList<SlidesTextUnit> Of(Presentation presentation, string scope = SlidesSearchScopes.All)
    {
        IComment[] comments = scope is SlidesSearchScopes.Comments or SlidesSearchScopes.All ? Comments(presentation) : [];
        var shown = new HashSet<IBaseSlide>(ReferenceEqualityComparer.Instance);
        return [.. presentation.Slides.SelectMany((slide, index) => Of(slide, index + 1, scope)
            .Concat(Around(slide, index + 1, comments, shown).Where(unit => scope == SlidesSearchScopes.All || unit.Scope == scope)))];
    }

    /// <summary>The units of slide <paramref name="number"/> a search scope reaches, without its comments, layout and master.</summary>
    internal static IEnumerable<SlidesTextUnit> Of(ISlide slide, int number, string scope)
    {
        if (scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All)
        {
            foreach (IShape shape in slide.Shapes)
            {
                yield return new SlidesTextUnit(number, slide, SlidesSearchScopes.Shapes, shape, [.. TextFrames(shape)]);
                foreach ((IShape inner, IGroupShape? group) in Shapes([shape]))
                {
                    foreach (SlidesTextUnit unit in Unwritten(number, slide, shape, inner, group))
                    {
                        yield return unit;
                    }
                }
            }
        }

        if (scope is SlidesSearchScopes.Notes or SlidesSearchScopes.All && slide.NotesSlideManager.NotesSlide?.NotesTextFrame is { } notes)
        {
            yield return new SlidesTextUnit(number, slide, SlidesSearchScopes.Notes, null, [notes]);
        }
    }

    /// <summary>Every shape, a group's children after the group, with the slide's own group that holds it, or null.</summary>
    internal static IEnumerable<(IShape Shape, IGroupShape? Group)> Shapes(IEnumerable<IShape> shapes, IGroupShape? group = null) =>
        shapes.SelectMany(shape => shape is IGroupShape inner
            ? Shapes(inner.Shapes, group ?? inner).Prepend((shape, group))
            : [(shape, group)]);

    /// <summary>Every chart of a collection, a group's included, with the slide's own group that holds it, or null.</summary>
    internal static IEnumerable<(IChart Chart, IGroupShape? Group)> Charts(IShapeCollection shapes) =>
        Shapes(shapes).Where(static found => found.Shape is IChart).Select(static found => ((IChart)found.Shape, found.Group));

    // The text of a shape replace_text does not change: its alternative text and, for a chart,
    // its title, axis titles, data (categories and series names) and data labels.
    private static IEnumerable<SlidesTextUnit> Unwritten(int number, ISlide slide, IShape shape, IShape inner, IGroupShape? group)
    {
        string within = group is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" in group '{group.Name}' (shapeId {group.OfficeInteropShapeId})");
        string where = string.Create(CultureInfo.InvariantCulture,
            $"{(inner is IChart ? "chart" : "shape")} '{inner.Name}' (shapeId {inner.OfficeInteropShapeId}){within} on slide {number}");
        SlidesTextUnit Unit(TextPart part, Func<IEnumerable<string?>> reads) => new(number, slide, SlidesSearchScopes.Shapes, shape, [])
        {
            Part = part,
            Holder = inner,
            Reads = reads,
            Where = where,
            Location = ShapeAddress(number, inner.OfficeInteropShapeId),
        };

        if (!string.IsNullOrWhiteSpace(inner.AlternativeText))
        {
            yield return Unit(TextPart.AltText, () => [inner.AlternativeText]);
        }

        if (inner is IChart chart)
        {
            yield return Unit(TextPart.ChartTitle, () => chart.HasTitle && chart.ChartTitle.TextFrameForOverriding is { } title ? Paragraphs(title) : []);
            yield return Unit(TextPart.AxisTitle, () => SlidesShapeFormats.AxisTitles(chart).SelectMany(Paragraphs));
            yield return Unit(TextPart.ChartData, () => CategoryTexts(chart)) with { ChartData = Categories };
            yield return Unit(TextPart.ChartData, () => SeriesNames(chart)) with { ChartData = SeriesNamesData };
            yield return Unit(TextPart.DataLabel, () => chart.ChartData.Series.SelectMany(static series => series.DataPoints)
                .Select(static point => point.Label?.TextFrameForOverriding).OfType<ITextFrame>().SelectMany(Paragraphs));
        }
    }

    // A slide's comments, and the text outside the placeholders of the layout and master it is the
    // first to show: none when the slide hides the background graphics, and no master's when its
    // layout hides the master's.
    private static IEnumerable<SlidesTextUnit> Around(ISlide slide, int number, IComment[] comments, HashSet<IBaseSlide> shown)
    {
        foreach (IComment comment in comments.Where(comment => comment.Slide?.SlideId == slide.SlideId))
        {
            yield return new SlidesTextUnit(number, slide, SlidesSearchScopes.Comments, null, [])
            {
                Part = TextPart.Comment,
                Holder = comment,
                Reads = () => [comment.Text],
                Where = string.Create(CultureInfo.InvariantCulture, $"a comment by '{comment.Author?.Name}' on slide {number}"),
                Location = SlideAddress(number),
            };
        }

        foreach ((IBaseSlide owner, string scope, string kind, bool showing) in new (IBaseSlide, string, string, bool)[]
        {
            (slide.LayoutSlide, SlidesSearchScopes.Layouts, "layout", slide.ShowMasterShapes),
            (slide.LayoutSlide.MasterSlide, SlidesSearchScopes.Masters, "master", slide.ShowMasterShapes && slide.LayoutSlide.ShowMasterShapes),
        })
        {
            if (!showing || !shown.Add(owner))
            {
                continue;
            }

            foreach (IShape shape in owner.Shapes.Where(static shape => shape.Placeholder is null))
            {
                yield return new SlidesTextUnit(number, slide, scope, null, [])
                {
                    Part = TextPart.Layout,
                    Holder = shape,
                    Reads = () => TextFrames(shape).SelectMany(Paragraphs),
                    Where = string.Create(CultureInfo.InvariantCulture, $"shape '{shape.Name}' of the {kind} '{owner.Name}' that slide {number} shows"),
                    Location = SlideAddress(number),
                };
            }
        }
    }

    // A frame's paragraphs, each as replace_text reads it: the text of its runs.
    private static IEnumerable<string> Paragraphs(ITextFrame frame) =>
        frame.Paragraphs.Select(static paragraph => string.Concat(paragraph.Portions.Select(static portion => portion.Text ?? string.Empty)));

    /// <summary>
    /// Whether replace_text writes a unit: text it changes, but in evaluation mode the watermark
    /// box an evaluation save added. The replacement and the verification of what it should leave
    /// both ask this.
    /// </summary>
    internal static bool Writes(SlidesTextUnit unit, bool evaluation) =>
        unit.Part is null && (!evaluation || !unit.IsEvaluationWatermark);

    /// <summary>The chart data of a unit that holds a chart's categories, as a warning names them.</summary>
    internal const string Categories = "categories";

    /// <summary>The chart data of a unit that holds a chart's series names, as a warning names them.</summary>
    internal const string SeriesNamesData = "series names";

    /// <summary>A chart's categories, each as <c>slides read</c> reads it.</summary>
    internal static IEnumerable<string?> CategoryTexts(IChart chart) =>
        chart.ChartData.Categories.Select(static category => Convert.ToString(category.Value, CultureInfo.InvariantCulture));

    /// <summary>A chart's series names, from their cells or, without them, as their literal text.</summary>
    internal static IEnumerable<string?> SeriesNames(IChart chart) =>
        chart.ChartData.Series.SelectMany(static series => series.Name.DataSourceType == DataSourceType.Worksheet
            ? series.Name.AsCells.Select(static cell => Convert.ToString(cell.Value, CultureInfo.InvariantCulture))
            : [series.Name.AsLiteralString]);
}

/// <summary>
/// One place that holds text: a slide's own shape or speaker notes, which replace_text writes, or
/// a part it does not change, which it names when it leaves a match there.
/// </summary>
/// <param name="Slide">The 1-based number of the slide that shows it.</param>
/// <param name="Owner">The slide that shows it.</param>
/// <param name="Scope">The search scope that reaches it, as a hit names it, such as <c>shapes</c> or <c>notes</c>.</param>
/// <param name="Shape">The slide's own shape that holds it, or null.</param>
/// <param name="Frames">The frames replace_text writes and --verify counts, in reading order; none for a part it does not change.</param>
internal sealed record SlidesTextUnit(int Slide, ISlide Owner, string Scope, IShape? Shape, IReadOnlyList<ITextFrame> Frames)
{
    /// <summary>For text replace_text does not change, the kind of part it names; null for text it writes.</summary>
    public TextPart? Part { get; init; }

    /// <summary>For text replace_text does not change, the object that holds it, which with <see cref="ChartData"/> tells the same unit apart in a later read.</summary>
    public object? Holder { get; init; }

    /// <summary>For a chart's data, which of it the unit holds: <see cref="SlidesTextUnits.Categories"/> or <see cref="SlidesTextUnits.SeriesNamesData"/>.</summary>
    public string? ChartData { get; init; }

    /// <summary>For text replace_text does not change, how it reads now.</summary>
    public Func<IEnumerable<string?>>? Reads { get; init; }

    /// <summary>For text replace_text does not change, where it is, as a warning names it.</summary>
    public string Where { get; init; } = string.Empty;

    /// <summary>For text replace_text does not change, the target address a warning names.</summary>
    public string? Location { get; init; }

    /// <summary>The text one paragraph, category or name at a time, as replace_text reads it.</summary>
    public IEnumerable<string> Texts => Reads is null
        ? Frames.SelectMany(static frame => frame.Paragraphs).Select(static paragraph => string.Concat(paragraph.Portions.Select(static portion => portion.Text ?? string.Empty)))
        : Reads().OfType<string>();

    /// <summary>The text as results report it, one line per paragraph or line break; null when it shows none.</summary>
    public string? Text => Reads is not null
        ? EmptyToNull(string.Join("\n", Texts.Where(static text => !string.IsNullOrWhiteSpace(text))))
        : Shape is null ? Notes(Owner) : ShapeText(Shape);

    /// <summary>Whether the unit is the watermark text box an evaluation save adds to every slide.</summary>
    public bool IsEvaluationWatermark => Reads is null && Shape is not null && SlidesEvaluationMarks.IsEvaluationWatermark(Shape);
}
