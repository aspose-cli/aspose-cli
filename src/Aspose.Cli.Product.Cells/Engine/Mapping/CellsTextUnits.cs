using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one enumeration of where a worksheet holds text, for the commands whose object is the text
/// itself: replace_text, which writes the text of cells, and the text it leaves as it was. Each
/// unit reads as a read reports it. The units of a sheet are its text cells in address order (a
/// formula and a value of any other type are no text), then its parts outside them: the header and
/// footer sections that print on some page, past their formatting codes
/// (<see cref="HeaderFooterText"/>); the comments, by row and column; the title of each chart, then
/// the category and value axis titles of each chart but a pie, which has no axes, an automatic
/// title and one linked to a cell, whose text it follows, reading as none; and the text of every
/// other drawing but a group, whose shapes are given. A range reaches only its cells and their comments.
/// </summary>
internal static class CellsTextUnits
{
    /// <summary>The text cells of <paramref name="range"/>, or of the sheet: a list, since writing a cell must not disturb the enumeration.</summary>
    internal static List<CellsTextUnit> Cells(Worksheet sheet, RangeRef? range)
    {
        var cells = new List<Cell>();
        foreach (Cell cell in sheet.Cells)
        {
            if (cell.Type == CellValueType.IsString && !cell.IsFormula && Within(range, cell.Row, cell.Column))
            {
                cells.Add(cell);
            }
        }

        cells.Sort(static (a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
        return cells.ConvertAll(static cell => new CellsTextUnit(null, cell));
    }

    /// <summary>The parts of <paramref name="sheet"/> outside its cells, or the comments of the cells of <paramref name="range"/>.</summary>
    internal static IEnumerable<CellsTextUnit> Parts(Worksheet sheet, RangeRef? range)
    {
        IEnumerable<CellsTextUnit> comments = sheet.Comments.Cast<Comment>().Where(comment => Within(range, comment.Row, comment.Column))
            .OrderBy(static comment => comment.Row).ThenBy(static comment => comment.Column)
            .Select(comment => Held(TextPart.Comment, comment, () => sheet.Comments.Cast<Comment>(), comment, () => comment.Note));
        if (range is not null)
        {
            return comments;
        }

        IEnumerable<Chart> Charts() => Enumerable.Range(0, sheet.Charts.Count).Select(index => sheet.Charts[index]);
        return HeaderFooterText.InEffect(sheet.PageSetup)
            .Select(section => new CellsTextUnit(TextPart.HeaderFooter, section, () => HeaderFooterText.InEffect(sheet.PageSetup)
                .FirstOrDefault(now => section with { Text = now.Text } == now) is { } now ? HeaderFooterText.Visible(now.Text) : null))
            .Concat(comments)
            .Concat(Charts().Select((chart, index) => Held(TextPart.ChartTitle, index, Charts, chart, () => Title(chart.Title))))
            .Concat(Charts().SelectMany((chart, index) => ChartPivotVocabulary.IsPieFamily(chart.Type) ? [] : new[]
            {
                Held(TextPart.AxisTitle, (index, "category"), Charts, chart, () => Title(chart.CategoryAxis.Title)),
                Held(TextPart.AxisTitle, (index, "value"), Charts, chart, () => Title(chart.ValueAxis.Title)),
            }))
            .Concat(sheet.Shapes.Cast<Shape>().Where(static shape => shape is not (CommentShape or ChartShape or GroupShape))
                .Select(shape => Held(TextPart.TextBox, shape, () => sheet.Shapes.Cast<Shape>(), shape, () => shape.Text)));
    }

    // A part that reads as none once its owner has left the sheet's collection of them.
    private static CellsTextUnit Held<T>(TextPart part, object holder, Func<IEnumerable<T>> collection, T owner, Func<string?> read)
        where T : class =>
        new(part, holder, () => collection().Contains(owner, ReferenceEqualityComparer.Instance) ? read() : null);

    private static string? Title(Title? title) =>
        title is null || title.IsAutoText || !string.IsNullOrEmpty(title.LinkedSource) ? null : title.Text;

    private static bool Within(RangeRef? range, int row, int column) =>
        range is not { } area
        || (row >= area.Start.Row && row <= area.End.Row && column >= area.Start.Column && column <= area.End.Column);
}

/// <summary>
/// One place of a sheet that holds text: a text cell, which replace_text writes, or a part it does
/// not change, which it names when it leaves a match there.
/// </summary>
/// <param name="Part">The kind of part; null for a cell.</param>
/// <param name="Holder">What holds the text: the <see cref="Cell"/>, the <see cref="HeaderFooterSection"/>,
/// the <see cref="Comment"/>, the zero-based index of a chart for its title and with the name of its
/// axis for an axis title, or the <see cref="Shape"/>.</param>
/// <param name="Reader">How a part reads now; null for a cell.</param>
internal sealed record CellsTextUnit(TextPart? Part, object Holder, Func<string?>? Reader = null)
{
    /// <summary>The text now; null once the sheet no longer holds the part.</summary>
    public string? Text => Holder is Cell cell ? cell.StringValue : Reader!();
}
