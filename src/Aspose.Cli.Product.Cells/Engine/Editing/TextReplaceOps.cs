using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Replaces text within the text values of a sheet's cells.</summary>
internal static class TextReplaceOps
{
    /// <summary>The most cells whose comments a warning names one by one.</summary>
    private const int NamedComments = 10;

    private static readonly string[] Sections = ["left", "center", "right"];

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
    /// <see cref="Replace"/> leaves as it is: one <c>TEXT_OUTSIDE_CELLS_NOT_REPLACED</c> warning
    /// each for the header and footer, the comments and the chart titles that hold one, matched as
    /// the operation matches a cell's text (regex, caseSensitive and wholeWord). The header,
    /// footer and chart titles are read as a read reports them, an automatic chart title as none.
    /// A range reaches only the comments of its cells; the header, footer and charts belong to the
    /// whole sheet. A warning never repeats the find text or the text that holds the match.
    /// </summary>
    public static IReadOnlyList<Warning> Unreached(Worksheet sheet, ReplaceTextOp op)
    {
        TextReplacement replacement = Replacement(op);
        RangeRef? range = Range(op);
        bool Holds(string? text) => !string.IsNullOrEmpty(text) && replacement.Matches(text, 1).Count > 0;
        var warnings = new List<Warning>();

        if (range is null)
        {
            PageSetup page = sheet.PageSetup;
            int[] header = [.. Enumerable.Range(0, Sections.Length).Where(section => Holds(page.GetHeader(section)))];
            int[] footer = [.. Enumerable.Range(0, Sections.Length).Where(section => Holds(page.GetFooter(section)))];
            string[] parts = [.. Part("header", header), .. Part("footer", footer)];
            if (parts.Length > 0)
            {
                bool sides = header.Concat(footer).Any(static section => section != 1);
                warnings.Add(Warning(op, sheet, $"the {string.Join(" and the ", parts)}", parts.Length > 1,
                    "Read the header and footer with 'aspose-cli cells inspect <file> --detail layout', then set the new text with set_page_setup, "
                        + "whose headerCenter and footerCenter replace the center sections"
                        + (sides ? "; no operation changes the left and right sections." : "."),
                    CellsAddresses.OfSheet("page-setup", sheet.Name)));
            }
        }

        Comment[] comments = [.. sheet.Comments.Cast<Comment>()
            .Where(comment => Within(range, comment.Row, comment.Column) && Holds(comment.Note))
            .OrderBy(static comment => comment.Row).ThenBy(static comment => comment.Column)];
        if (comments.Length > 0)
        {
            string[] cells = [.. comments.Select(static comment => A1.FormatCell(new CellRef(comment.Row, comment.Column)))];
            string named = cells.Length <= NamedComments
                ? string.Join(", ", cells)
                : string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", cells.Take(NamedComments))} and {cells.Length - NamedComments} more");
            warnings.Add(Warning(op, sheet, $"the {(cells.Length == 1 ? "comment of cell" : "comments of cells")} {named}", cells.Length > 1,
                "Read the comments with 'aspose-cli cells inspect <file> --detail comments', then set the new text of each with set_comment.",
                cells.Length == 1 ? CellsAddresses.At("comment", sheet.Name, cells[0]) : CellsAddresses.OfSheet("sheet", sheet.Name)));
        }

        if (range is null)
        {
            int[] charts = [.. Enumerable.Range(0, sheet.Charts.Count).Where(index => Holds(Title(sheet.Charts[index])))];
            if (charts.Length > 0)
            {
                string numbers = string.Join(", ", charts.Select(static index => (index + 1).ToString(CultureInfo.InvariantCulture)));
                warnings.Add(Warning(op, sheet, $"the {(charts.Length == 1 ? "title of chart" : "titles of charts")} {numbers}", charts.Length > 1,
                    "Read the titles with 'aspose-cli cells inspect <file> --detail charts', then set each new title with set_chart and the index of its chart.",
                    charts.Length == 1 ? CellsAddresses.Indexed("chart", sheet.Name, charts[0]) : CellsAddresses.OfSheet("sheet", sheet.Name)));
            }
        }

        return warnings;
    }

    /// <summary>A part of the header or footer and the sections that hold a match, such as <c>header (center)</c>.</summary>
    private static IEnumerable<string> Part(string part, int[] sections) =>
        sections.Length == 0 ? [] : [$"{part} ({string.Join(", ", sections.Select(static section => Sections[section]))})"];

    /// <summary>A chart's title as a read reports it: its text, or none when the title is automatic.</summary>
    private static string? Title(Chart chart) => chart.Title.IsAutoText ? null : chart.Title.Text;

    private static Warning Warning(ReplaceTextOp op, Worksheet sheet, string what, bool several, string hint, string location) =>
        new(CellsDiagnostics.TextOutsideCellsNotReplaced,
            $"Operation {op.Id} (replace_text) found a match in {what} on worksheet '{sheet.Name}' and left "
                + (several ? "them as they were" : "it as it was") + ": replace_text changes the text of cells only.")
        {
            Hint = hint,
            Location = location,
            Docs = "cells/editing",
            Details = new JsonObject { ["id"] = op.Id, ["op"] = "replace_text" },
        };

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
