using System.Globalization;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Replaces text within the text values of a sheet's cells.</summary>
internal static class TextReplaceOps
{
    /// <summary>The most cells whose comments, or drawings, a warning names one by one.</summary>
    private const int NamedItems = 10;

    /// <summary>
    /// Replaces the matches of <paramref name="op"/> in the text cells of its range, row by row,
    /// up to its replacement limit, and returns how many matches it replaced. A formula, and a
    /// value of any other type, is never touched.
    /// </summary>
    public static long Replace(Worksheet sheet, ReplaceTextOp op)
    {
        TextReplacement replacement = Replacement(op);
        RangeRef? range = Range(op);
        int left = op.MaxReplacementCount ?? int.MaxValue;
        long replaced = 0;
        foreach (Cell cell in TextCells(sheet, range))
        {
            (string after, int matches) = replacement.Replace(cell.StringValue, left);
            if (matches == 0)
            {
                continue;
            }

            cell.PutValue(after);
            replaced += matches;
            left -= matches;
            if (left == 0)
            {
                break;
            }
        }

        return replaced;
    }

    /// <summary>
    /// The text of the sheet outside its cells that holds a match of <paramref name="op"/>, which
    /// <see cref="Replace"/> leaves as it is: one <c>TEXT_NOT_REPLACED</c> warning each for the
    /// header and footer, the comments, the chart titles, the axis titles and the text of the
    /// drawings, such as text boxes, that hold one, matched as the operation matches a cell's text
    /// (regex, caseSensitive and wholeWord). The header and footer are matched on the text they
    /// print, past their formatting codes (<see cref="HeaderFooterText.Visible"/>), in every
    /// section that prints on some page: the first page's and the even pages' too when the sheet
    /// has its own for them. The chart and axis titles are read as a read reports them: an
    /// automatic title as none, and the axis titles of a pie, which has no axes, as none. A title
    /// linked to a cell follows the cell, so it is not named either. A range reaches only the
    /// comments of its cells; the header, footer, charts and drawings belong to the whole sheet.
    /// A warning never repeats the find text or the text that holds the match. Each is reported
    /// only if the same section, comment, title or drawing still holds a match once the batch has
    /// run (<see cref="UnreplacedText"/>).
    /// </summary>
    public static IReadOnlyList<UnreplacedText> Unreached(Worksheet sheet, ReplaceTextOp op)
    {
        TextReplacement replacement = Replacement(op);
        RangeRef? range = Range(op);
        bool Holds(string? text) => !string.IsNullOrEmpty(text) && replacement.Matches(text, 1).Count > 0;
        UnreplacedText?[] found =
        [
            range is null ? HeaderFooter(sheet, op, Holds) : null,
            Comments(sheet, op, range, Holds),
            range is null ? ChartTitles(sheet, op, Holds) : null,
            range is null ? AxisTitles(sheet, op, Holds) : null,
            range is null ? Drawings(sheet, op, Holds) : null,
        ];
        return [.. found.OfType<UnreplacedText>()];
    }

    /// <summary>The header and footer sections that hold a match, named by their part and section, such as <c>the header (center) and the first-page footer (left)</c>.</summary>
    private static UnreplacedText? HeaderFooter(Worksheet sheet, ReplaceTextOp op, Func<string?, bool> holds)
    {
        HeaderFooterSection[] matched = [.. HeaderFooterText.InEffect(sheet.PageSetup).Where(section => holds(HeaderFooterText.Visible(section.Text)))];
        if (matched.Length == 0)
        {
            return null;
        }

        string[] parts = [.. matched.GroupBy(static section => section.Part)
            .Select(static part => $"{part.Key} ({string.Join(", ", part.Select(static section => HeaderFooterText.SectionName(section.Section)))})")];
        HeaderFooterSection[] read = [.. matched.Where(static section => section.Pages == HeaderFooterPages.Default)];
        bool first = matched.Any(static section => section.Pages == HeaderFooterPages.FirstPage);
        bool even = matched.Any(static section => section.Pages == HeaderFooterPages.EvenPages);
        var hint = new List<string>();
        if (read.Length > 0)
        {
            hint.Add("Read the header and footer with 'aspose-cli cells inspect <file> --detail layout', then set the new text with set_page_setup, "
                + "whose headerCenter and footerCenter replace the center sections"
                + (read.Any(static section => section.Section != 1) ? "; no operation changes the left and right sections." : "."));
        }

        if (first || even)
        {
            string pages = (first, even) switch
            {
                (true, true) => "of the first page and of the even pages",
                (true, false) => "of the first page",
                _ => "of the even pages",
            };
            hint.Add($"No operation reads or changes the header and footer {pages}: tell the user they still hold the old text.");
        }

        return Found(op, sheet, TextPart.HeaderFooter, $"the {string.Join(" and the ", parts)}", parts.Length > 1,
            string.Join(" ", hint), CellsAddresses.OfSheet("page-setup", sheet.Name),
            () => HeaderFooterText.InEffect(sheet.PageSetup).Any(section => holds(HeaderFooterText.Visible(section.Text))
                && matched.Any(then => then with { Text = section.Text } == section)));
    }

    /// <summary>The comments that hold a match, of the cells of the range when the operation has one, by their cells.</summary>
    private static UnreplacedText? Comments(Worksheet sheet, ReplaceTextOp op, RangeRef? range, Func<string?, bool> holds)
    {
        Comment[] comments = [.. sheet.Comments.Cast<Comment>()
            .Where(comment => Within(range, comment.Row, comment.Column) && holds(comment.Note))
            .OrderBy(static comment => comment.Row).ThenBy(static comment => comment.Column)];
        if (comments.Length == 0)
        {
            return null;
        }

        string[] cells = [.. comments.Select(static comment => A1.FormatCell(new CellRef(comment.Row, comment.Column)))];
        return Found(op, sheet, TextPart.Comment, $"the {(cells.Length == 1 ? "comment of cell" : "comments of cells")} {Named(cells)}", cells.Length > 1,
            "Read the comments with 'aspose-cli cells inspect <file> --detail comments', then set the new text of each with set_comment.",
            cells.Length == 1 ? CellsAddresses.At("comment", sheet.Name, cells[0]) : CellsAddresses.OfSheet("sheet", sheet.Name),
            () => sheet.Comments.Cast<Comment>().Any(comment => comments.Contains(comment, ReferenceEqualityComparer.Instance) && holds(comment.Note)));
    }

    /// <summary>The chart titles that hold a match, by the number of their chart.</summary>
    private static UnreplacedText? ChartTitles(Worksheet sheet, ReplaceTextOp op, Func<string?, bool> holds)
    {
        int[] charts = [.. Enumerable.Range(0, sheet.Charts.Count).Where(index => holds(Title(sheet.Charts[index].Title)))];
        if (charts.Length == 0)
        {
            return null;
        }

        Chart[] found = [.. charts.Select(index => sheet.Charts[index])];

        string numbers = string.Join(", ", charts.Select(static index => (index + 1).ToString(CultureInfo.InvariantCulture)));
        return Found(op, sheet, TextPart.ChartTitle, $"the {(charts.Length == 1 ? "title of chart" : "titles of charts")} {numbers}", charts.Length > 1,
            "Read the titles with 'aspose-cli cells inspect <file> --detail charts', then set each new title with set_chart and the index of its chart.",
            charts.Length == 1 ? CellsAddresses.Indexed("chart", sheet.Name, charts[0]) : CellsAddresses.OfSheet("sheet", sheet.Name),
            () => Charts(sheet).Any(chart => found.Contains(chart, ReferenceEqualityComparer.Instance) && holds(Title(chart.Title))));
    }

    /// <summary>
    /// The axis titles that hold a match, by the number of their chart and their axes, such as
    /// <c>the axis titles of chart 1 (category, value)</c>.
    /// </summary>
    private static UnreplacedText? AxisTitles(Worksheet sheet, ReplaceTextOp op, Func<string?, bool> holds)
    {
        var matched = new List<(int Chart, string[] Axes)>();
        for (int index = 0; index < sheet.Charts.Count; index++)
        {
            Chart chart = sheet.Charts[index];
            if (ChartPivotVocabulary.IsPieFamily(chart.Type))
            {
                continue;
            }

            string[] axes = [.. new[] { "category", "value" }.Where(axis => holds(Title(Axis(chart, axis))))];
            if (axes.Length > 0)
            {
                matched.Add((index, axes));
            }
        }

        if (matched.Count == 0)
        {
            return null;
        }

        int titles = matched.Sum(static chart => chart.Axes.Length);
        (Chart Chart, string[] Axes)[] found = [.. matched.Select(chart => (sheet.Charts[chart.Chart], chart.Axes))];
        string charts = string.Join(", ", matched.Select(static chart => string.Create(CultureInfo.InvariantCulture, $"{chart.Chart + 1} ({string.Join(", ", chart.Axes)})")));
        return Found(op, sheet, TextPart.AxisTitle, $"the {(titles == 1 ? "axis title" : "axis titles")} of {(matched.Count == 1 ? "chart" : "charts")} {charts}", titles > 1,
            "Read the axis titles with 'aspose-cli cells inspect <file> --detail charts', then set the new titles with set_chart, its axisTitles and the index of its chart.",
            matched.Count == 1 ? CellsAddresses.Indexed("chart", sheet.Name, matched[0].Chart) : CellsAddresses.OfSheet("sheet", sheet.Name),
            () => found.Any(then => Charts(sheet).Contains(then.Chart, ReferenceEqualityComparer.Instance)
                && then.Axes.Any(axis => holds(Title(Axis(then.Chart, axis))))));

        static Title Axis(Chart chart, string axis) => axis == "category" ? chart.CategoryAxis.Title : chart.ValueAxis.Title;
    }

    /// <summary>
    /// The drawings whose own text holds a match, such as a text box, by their names: every shape
    /// but a comment, a chart, whose titles are named apart, and a group, whose shapes are named.
    /// </summary>
    private static UnreplacedText? Drawings(Worksheet sheet, ReplaceTextOp op, Func<string?, bool> holds)
    {
        Shape[] drawings = [.. sheet.Shapes.Cast<Shape>().Where(shape => shape is not (CommentShape or ChartShape or GroupShape) && holds(shape.Text))];
        string[] names = [.. drawings.Select(static shape => $"'{shape.Name}'")];
        if (names.Length == 0)
        {
            return null;
        }

        return Found(op, sheet, TextPart.TextBox, $"the text of {(names.Length == 1 ? "drawing" : "drawings")} {Named(names)}", names.Length > 1,
            "No operation reads or changes the text of a text box or another drawing: tell the user it still holds the old text.",
            CellsAddresses.OfSheet("sheet", sheet.Name),
            () => sheet.Shapes.Cast<Shape>().Any(shape => drawings.Contains(shape, ReferenceEqualityComparer.Instance) && holds(shape.Text)));
    }

    /// <summary>The first <see cref="NamedItems"/> of <paramref name="items"/> and how many more, such as <c>B1, B2 and 2 more</c>.</summary>
    private static string Named(string[] items) => items.Length <= NamedItems
        ? string.Join(", ", items)
        : string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", items.Take(NamedItems))} and {items.Length - NamedItems} more");

    /// <summary>
    /// A chart or axis title as a read reports it: its text, or none when the title is automatic
    /// or the chart has none; and none when it is linked to a cell, whose text it follows.
    /// </summary>
    private static string? Title(Title? title) =>
        title is null || title.IsAutoText || !string.IsNullOrEmpty(title.LinkedSource) ? null : title.Text;

    // What remains is read only while the sheet is still in its workbook.
    private static UnreplacedText Found(ReplaceTextOp op, Worksheet sheet, TextPart part, string what, bool several, string hint, string location, Func<bool> remains) =>
        UnreplacedText.Found(part, op, "replace_text",
            $"Operation {op.Id} (replace_text) found a match in {what} on worksheet '{sheet.Name}' and left "
                + (several ? "them as they were" : "it as it was") + ": replace_text changes the text of cells only.",
            hint, location, () => sheet.Workbook.Worksheets.Contains(sheet, ReferenceEqualityComparer.Instance) && remains(), docs: "cells/editing");

    private static IEnumerable<Chart> Charts(Worksheet sheet) => Enumerable.Range(0, sheet.Charts.Count).Select(index => sheet.Charts[index]);

    private static TextReplacement Replacement(ReplaceTextOp op) =>
        TextReplacement.Create(op.Find, op.Replace, op.Regex, op.CaseSensitive, op.WholeWord);

    private static RangeRef? Range(ReplaceTextOp op) => op.Range is { } text ? A1.ParseRange(text).Range : null;

    private static bool Within(RangeRef? range, int row, int column) =>
        range is not { } area
        || (row >= area.Start.Row && row <= area.End.Row && column >= area.Start.Column && column <= area.End.Column);

    // The stored text cells of the range, or of the sheet, in address order; a snapshot of the list,
    // since writing a cell must not disturb the enumeration.
    private static List<Cell> TextCells(Worksheet sheet, RangeRef? range)
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
        return cells;
    }
}
