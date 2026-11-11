using Aspose.Slides;
using Aspose.Slides.Charts;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The one enumeration of where a presentation holds text, for the commands whose object is the
/// text itself: search, replace_text and the chart text it leaves, the occurrences --verify
/// counts, extract of text and notes, and whether evaluation mode cut what it read. A unit is a
/// slide's own shape, with its table cells, group children and SmartArt nodes, or the slide's
/// speaker notes, in slide order, each slide's shapes before its notes. Commands that report one
/// shape's structure, layout or state read the shapes themselves.
/// </summary>
internal static class SlidesTextUnits
{
    /// <summary>The units of every slide a search scope reaches: its shapes, its notes, or both.</summary>
    internal static IEnumerable<SlidesTextUnit> Of(Presentation presentation, string scope = SlidesSearchScopes.All) =>
        presentation.Slides.SelectMany((slide, index) => Of(slide, index + 1, scope));

    /// <summary>The units of slide <paramref name="number"/> a search scope reaches.</summary>
    internal static IEnumerable<SlidesTextUnit> Of(ISlide slide, int number, string scope)
    {
        IEnumerable<SlidesTextUnit> shapes = scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All
            ? slide.Shapes.Select(shape => new SlidesTextUnit(number, slide, SlidesSearchScopes.Shapes, shape, [.. TextFrames(shape)]))
            : [];
        return scope is SlidesSearchScopes.Notes or SlidesSearchScopes.All && slide.NotesSlideManager.NotesSlide?.NotesTextFrame is { } notes
            ? shapes.Append(new SlidesTextUnit(number, slide, SlidesSearchScopes.Notes, null, [notes]))
            : shapes;
    }

    /// <summary>Every shape of a collection, a group's children after the group, with the slide's own group that holds it, or null.</summary>
    internal static IEnumerable<(IShape Shape, IGroupShape? Group)> Shapes(IShapeCollection shapes, IGroupShape? group = null) =>
        shapes.SelectMany(shape => shape is IGroupShape inner
            ? Shapes(inner.Shapes, group ?? inner).Prepend((shape, group))
            : [(shape, group)]);

    /// <summary>Every chart of a collection, a group's included, with the slide's own group that holds it, or null.</summary>
    internal static IEnumerable<(IChart Chart, IGroupShape? Group)> Charts(IShapeCollection shapes) =>
        Shapes(shapes).Where(static found => found.Shape is IChart).Select(static found => ((IChart)found.Shape, found.Group));
}

/// <summary>One place that holds text: a slide's own shape, or its speaker notes.</summary>
/// <param name="Slide">The 1-based number of the slide.</param>
/// <param name="Owner">The slide.</param>
/// <param name="Scope">The search scope that reaches it, as a hit names it: <c>shapes</c> or <c>notes</c>.</param>
/// <param name="Shape">The slide's own shape, or null for the notes.</param>
/// <param name="Frames">The frames replace_text writes and --verify counts, in reading order.</param>
internal sealed record SlidesTextUnit(int Slide, ISlide Owner, string Scope, IShape? Shape, IReadOnlyList<ITextFrame> Frames)
{
    /// <summary>The text as results report it, one line per paragraph or line break; null when it shows none.</summary>
    public string? Text => Shape is null ? Notes(Owner) : ShapeText(Shape);

    /// <summary>Whether the unit is the watermark text box an evaluation save adds to every slide.</summary>
    public bool IsEvaluationWatermark => Shape is not null && SlidesEvaluationMarks.IsEvaluationWatermark(Shape);
}
