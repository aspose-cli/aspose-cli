using System.Globalization;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one enumeration of where a workbook holds text a reader sees, for the commands whose object
/// is the text itself: search, and replace_text, which writes the text of text cells and names the
/// text it leaves as it was. Each unit reads as a read reports it. A sheet gives its cells in
/// address order (text; a formula; a number, date, boolean or error that shows the text), then its
/// parts outside them: the header and footer sections that print on some page, past their
/// formatting codes (<see cref="HeaderFooterText"/>); the comments, threaded ones included, by row
/// and column; for each chart its title, its category, value and secondary value axis titles (none
/// for a pie), its series names, the data labels with text of their own and its alternative text;
/// and the text and alternative text of every other drawing but a group, whose shapes are given.
/// An automatic title reads as none. A title, series name, data label or text box linked to a cell
/// shows that cell's text, so it is a unit only when the command does not reach the cell's sheet,
/// and then names the cell. A range reaches only its cells and their comments. No unit holds a data
/// validation message or list item, a hyperlink's screen tip, a sheet or defined name, the text of
/// an ActiveX control, or a pivot table caption that no cell shows.
/// </summary>
internal static class CellsTextUnits
{
    /// <summary>The units of every sheet of <paramref name="workbook"/>, or of the one at <paramref name="sheetIndex"/>, counting its cells against the budget.</summary>
    internal static IEnumerable<CellsTextUnit> Of(Workbook workbook, int? sheetIndex, ResourceBudgetLedger budgets) =>
        workbook.Worksheets.Cast<Worksheet>().Where(sheet => sheetIndex is null || sheet.Index == sheetIndex)
            .SelectMany(sheet => StoredCells.InAddressOrder(budgets, sheet, "search").Select(cell => Unit(sheet, cell))
                .Concat(Parts(sheet, null, sheetIndex is null ? null : sheet.Name)));

    /// <summary>The cells of <paramref name="range"/>, or of the sheet, in address order: a list, since writing a cell must not disturb the enumeration.</summary>
    internal static List<CellsTextUnit> Cells(Worksheet sheet, RangeRef? range)
    {
        var cells = new List<Cell>();
        foreach (Cell cell in sheet.Cells)
        {
            if (Within(range, cell.Row, cell.Column))
            {
                cells.Add(cell);
            }
        }

        cells.Sort(static (a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
        return cells.ConvertAll(cell => Unit(sheet, cell));
    }

    /// <summary>
    /// The parts of <paramref name="sheet"/> outside its cells, or the comments of the cells of
    /// <paramref name="range"/>. A part linked to a cell is given only when that cell is on another
    /// sheet than <paramref name="reached"/>, the one sheet the command reaches; never when it reaches every sheet (null).
    /// </summary>
    internal static IEnumerable<CellsTextUnit> Parts(Worksheet sheet, RangeRef? range, string? reached)
    {
        IEnumerable<CellsTextUnit> comments = sheet.Comments.Cast<Comment>().Where(comment => Within(range, comment.Row, comment.Column))
            .OrderBy(static comment => comment.Row).ThenBy(static comment => comment.Column)
            .Select(comment => Held(sheet, TextPart.Comment, comment, () => sheet.Comments.Cast<Comment>(), comment, () => comment.Note,
                CellsAddresses.At("comment", sheet.Name, A1.FormatCell(new CellRef(comment.Row, comment.Column)))));
        if (range is not null)
        {
            return comments;
        }

        IEnumerable<Chart> Charts() => Enumerable.Range(0, sheet.Charts.Count).Select(index => sheet.Charts[index]);
        IEnumerable<Shape> Drawings() => sheet.Shapes.Cast<Shape>().Where(static shape => shape is not (CommentShape or ChartShape or GroupShape));
        string drawings = CellsAddresses.OfSheet("sheet", sheet.Name);
        return HeaderFooterText.InEffect(sheet.PageSetup)
            .Select(section => new CellsTextUnit(sheet, TextPart.HeaderFooter, section, CellsAddresses.OfSheet("page-setup", sheet.Name),
                () => HeaderFooterText.InEffect(sheet.PageSetup).FirstOrDefault(now => section with { Text = now.Text } == now) is { } now ? HeaderFooterText.Visible(now.Text) : null))
            .Concat(comments)
            .Concat(Charts().SelectMany((chart, index) => ChartParts(sheet, chart, index, Charts, reached)))
            .Concat(Drawings().SelectMany(shape => new[]
            {
                Linked(sheet, TextPart.TextBox, shape, Drawings, shape, shape.LinkedCell, () => shape.Text, drawings, $"drawing '{shape.Name}'", reached),
                Linked(sheet, TextPart.AltText, shape, Drawings, shape, null, () => shape.AlternativeText, drawings, $"drawing '{shape.Name}'", null),
            }))
            .OfType<CellsTextUnit>();
    }

    /// <summary>The part a cell's text is: none for text, which replace_text writes.</summary>
    internal static TextPart? PartOf(Cell cell) =>
        cell.IsFormula ? TextPart.Formula : cell.Type == CellValueType.IsString ? null : TextPart.TypedValue;

    private static CellsTextUnit Unit(Worksheet sheet, Cell cell) =>
        new(sheet, PartOf(cell), cell, CellsAddresses.At("cell", sheet.Name, cell.Name)) { Name = cell.Name };

    // A chart's title, axis titles, series names, data labels with text of their own, and alternative text.
    private static IEnumerable<CellsTextUnit?> ChartParts(Worksheet sheet, Chart chart, int index, Func<IEnumerable<Chart>> charts, string? reached)
    {
        string location = CellsAddresses.Indexed("chart", sheet.Name, index);
        string name = string.Create(CultureInfo.InvariantCulture, $"chart {index + 1}");
        CellsTextUnit? Text(TextPart part, object holder, string? link, Func<string?> read) =>
            Linked(sheet, part, holder, charts, chart, link, read, location, name, reached);

        yield return Text(TextPart.ChartTitle, index, chart.Title?.LinkedSource, () => Title(chart.Title));
        if (!ChartPivotVocabulary.IsPieFamily(chart.Type))
        {
            yield return Text(TextPart.AxisTitle, (index, "category"), chart.CategoryAxis?.Title?.LinkedSource, () => Title(chart.CategoryAxis?.Title));
            yield return Text(TextPart.AxisTitle, (index, "value"), chart.ValueAxis?.Title?.LinkedSource, () => Title(chart.ValueAxis?.Title));
            yield return Text(TextPart.AxisTitle, (index, "secondary value"), chart.SecondValueAxis?.Title?.LinkedSource, () => Title(chart.SecondValueAxis?.Title));
        }

        for (int number = 0; number < chart.NSeries.Count; number++)
        {
            Series series = chart.NSeries[number];
            string? link = series.Name is ['=', ..] ? series.Name : null;
            yield return Text(TextPart.ChartData, index, link, () => link is null ? series.Name : series.DisplayName);
            // The stored points only: reading the labels of a point by its index adds hidden labels to the chart.
            foreach (ChartPoint point in series.Points)
            {
                DataLabels? label = point.DataLabels;
                yield return Text(TextPart.DataLabel, index, label?.LinkedSource, () => label?.Text);
            }
        }

        yield return Text(TextPart.AltText, index, null, () => chart.ChartObject?.AlternativeText);
    }

    // A part's own text, or, linked to a cell (=Sheet!$A$1, or $A$1 on the part's own sheet, as a
    // text box's text link writes it), that cell's text as the part shows it: then a unit only
    // when the command reaches one sheet and the cell is on another.
    private static CellsTextUnit? Linked<T>(Worksheet sheet, TextPart part, object holder, Func<IEnumerable<T>> collection, T owner,
        string? link, Func<string?> read, string location, string name, string? reached)
        where T : class
    {
        CellsTextUnit unit = Held(sheet, part, holder, collection, owner, read, location) with { Name = name };
        if (string.IsNullOrEmpty(link))
        {
            return unit;
        }

        string cell = link.TrimStart('=');
        int bang = cell.LastIndexOf('!');
        string linked = bang < 0 ? sheet.Name : cell[..bang].Trim('\'').Replace("''", "'", StringComparison.Ordinal);
        return reached is not null && !string.Equals(linked, reached, StringComparison.OrdinalIgnoreCase)
            ? unit with { LinkedCell = cell[..(bang + 1)] + cell[(bang + 1)..].Replace("$", string.Empty, StringComparison.Ordinal) }
            : null;
    }

    // A part that reads as none once its owner has left the sheet's collection of them.
    private static CellsTextUnit Held<T>(Worksheet sheet, TextPart part, object holder, Func<IEnumerable<T>> collection, T owner, Func<string?> read, string location)
        where T : class =>
        new(sheet, part, holder, location, () => collection().Contains(owner, ReferenceEqualityComparer.Instance) ? read() : null);

    private static string? Title(Title? title) => title is null || title.IsAutoText ? null : title.Text;

    private static bool Within(RangeRef? range, int row, int column) =>
        range is not { } area
        || (row >= area.Start.Row && row <= area.End.Row && column >= area.Start.Column && column <= area.End.Column);
}

/// <summary>
/// One place of a workbook that holds text: a cell, whose text replace_text writes when it holds
/// text, or a part it does not change, which it names when it leaves a match there.
/// </summary>
/// <param name="Sheet">The sheet that holds it.</param>
/// <param name="Part">What it is when it is not a text cell, such as a formula or a comment; null for a text cell.</param>
/// <param name="Holder">What holds the text: the <see cref="Cell"/>, the <see cref="HeaderFooterSection"/>,
/// the <see cref="Comment"/>, the zero-based index of a chart, with the name of its axis for an axis
/// title, or the <see cref="Shape"/>.</param>
/// <param name="Location">Its target address, such as <c>cell/Data!B2</c> or <c>chart/Data/1</c>.</param>
/// <param name="Reader">How a part reads now; null for a cell.</param>
internal sealed record CellsTextUnit(Worksheet Sheet, TextPart? Part, object Holder, string Location, Func<string?>? Reader = null)
{
    /// <summary>How a warning names it among others of its part: a cell's address, <c>chart 2</c> or <c>drawing 'Logo'</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>For a part linked to a cell of a sheet the command does not reach, that cell, such as <c>Other!B1</c>.</summary>
    public string? LinkedCell { get; init; }

    /// <summary>The text now; null once the sheet no longer holds the part.</summary>
    public string? Text => Holder is Cell cell ? cell.StringValue : Reader!();

    /// <summary>
    /// The texts a reader sees, the first as a read reports it: a cell's value (the invariant text
    /// of a number or date), then what it displays; a part's text. None once a cell holds another
    /// kind of value than it held.
    /// </summary>
    public IEnumerable<string?> Shown => Holder switch
    {
        Cell cell when CellsTextUnits.PartOf(cell) != Part => [],
        Cell cell => [CellMapper.RawValueString(cell) ?? cell.StringValue, cell.StringValue],
        _ => [Reader!()],
    };

    /// <summary>A formula cell's formula; null otherwise.</summary>
    public string? FormulaText => Holder is Cell { IsFormula: true } cell && Part == TextPart.Formula ? cell.Formula : null;

    /// <summary>The address of a cell, or of the cell of a comment, such as <c>B7</c>; null for any other part.</summary>
    public string? Address => Holder switch
    {
        Cell cell => cell.Name,
        Comment comment => A1.FormatCell(new CellRef(comment.Row, comment.Column)),
        _ => null,
    };
}
