using Aspose.Slides;
using Aspose.Slides.SmartArt;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The text a slide shows, as results report it: each shape's text frames and lines, the
/// slide's title and notes, and the share of it a character budget lets a result carry.
/// </summary>
internal static class SlidesShapeText
{
    /// <summary>A part of a shape's text a read reports apart, as <see cref="Clean"/> leaves it, within the budget.</summary>
    internal static string? Take(string? value, ref int remaining, ref bool truncated) => TakeText(Clean(value), ref remaining, ref truncated);

    /// <summary>
    /// A slide's title, a shape's text or the notes, as <see cref="Title"/>, <see cref="ShapeText"/>
    /// and <see cref="Notes"/> give them and inspect, search and replace_text see them, within the budget.
    /// </summary>
    internal static string? TakeText(string? value, ref int remaining, ref bool truncated)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length <= remaining)
        {
            remaining -= value.Length;
            return value;
        }

        truncated = true;
        string result = remaining == 0 ? string.Empty : value[..remaining];
        remaining = 0;
        return result;
    }

    /// <summary>
    /// A part of a shape's text as a read reports it apart, such as a paragraph, a run, a table cell
    /// or chart data: each line keeps its words, one space apart, and the lines stay apart; null
    /// for text that shows nothing.
    /// </summary>
    internal static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(
                "\n",
                value.Split('\n')
                    .Select(static line => string.Join(" ", line.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)))
                    .Where(static line => line.Length > 0));

    /// <summary>
    /// The text a shape shows: its own frame, table cells, group children and SmartArt nodes,
    /// one paragraph or line break per line.
    /// </summary>
    internal static string? ShapeText(IShape shape) =>
        EmptyToNull(string.Join(
            "\n",
            TextFrames(shape).Select(static frame => Lines(frame.Text)).Where(static text => !string.IsNullOrWhiteSpace(text))));

    /// <summary>
    /// Ends each line of engine text with a line feed: the engine ends paragraphs with a
    /// carriage return and line breaks with a vertical tab. Offsets into the text stay valid.
    /// </summary>
    private static string? Lines(string? text) => text?.Replace('\r', '\n').Replace('\v', '\n');

    /// <summary>
    /// Every text frame a shape shows, in reading order: its own frame, each table cell
    /// (a merged range once), each group child and each SmartArt node. Chart text is not included.
    /// </summary>
    internal static IEnumerable<ITextFrame> TextFrames(IShape shape)
    {
        switch (shape)
        {
            case IGroupShape group:
                foreach (ITextFrame frame in group.Shapes.SelectMany(TextFrames))
                {
                    yield return frame;
                }

                break;
            case ITable table:
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    for (int column = 0; column < table.Columns.Count; column++)
                    {
                        ICell cell = table[column, row];
                        if (cell.FirstRowIndex == row && cell.FirstColumnIndex == column && cell.TextFrame is { } text)
                        {
                            yield return text;
                        }
                    }
                }

                break;
            case ISmartArt smartArt:
                foreach (ISmartArtNode node in smartArt.AllNodes)
                {
                    if (node.TextFrame is { } text)
                    {
                        yield return text;
                    }
                }

                break;
            case IAutoShape { TextFrame: { } text }:
                yield return text;
                break;
        }
    }

    internal static string? PreviewText(ISlide slide)
    {
        string value = string.Join(
            " · ",
            slide.Shapes.Select(ShapeText).Where(static text => text is not null));
        return value.Length switch
        {
            0 => null,
            <= SlidesSlideInfo.PreviewTextLength => value,
            _ => value[..SlidesSlideInfo.PreviewTextLength],
        };
    }

    /// <summary>
    /// Text an operation writes as the verification compares it with what a shape or the notes
    /// hold: each paragraph and line break a line, as <see cref="Clean"/> leaves it.
    /// </summary>
    internal static string? AsRead(string? text) => Clean(Lines(text));

    /// <summary>
    /// The slide's title: the text of its title shape (<see cref="SlidesAuthoring.Title"/>), one
    /// line per paragraph or line break as the shape's own text; none when that shape is empty or
    /// the slide has none.
    /// </summary>
    internal static string? Title(ISlide slide) => EmptyToNull(Lines(SlidesAuthoring.Title(slide)?.TextFrame?.Text));

    internal static string? Notes(ISlide slide) =>
        EmptyToNull(Lines(slide.NotesSlideManager.NotesSlide?.NotesTextFrame?.Text));

    internal static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
