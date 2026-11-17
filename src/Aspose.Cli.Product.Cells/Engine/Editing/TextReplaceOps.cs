using System.Globalization;
using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Replaces text within the text cells of a sheet, and names the text it leaves, as <see cref="CellsTextUnits"/> gives them.</summary>
internal static class TextReplaceOps
{
    /// <summary>The most cells whose comments, or drawings, a warning names one by one.</summary>
    private const int NamedItems = 10;

    /// <summary>
    /// Replaces the matches of <paramref name="op"/> in the text cells of its range, row by row,
    /// up to its replacement limit, and returns how many matches it replaced.
    /// </summary>
    public static long Replace(Worksheet sheet, ReplaceTextOp op)
    {
        TextReplacement replacement = Replacement(op);
        int left = op.MaxReplacementCount ?? int.MaxValue;
        long replaced = 0;
        foreach (CellsTextUnit cell in CellsTextUnits.Cells(sheet, Range(op)).Where(static unit => unit.Part is null))
        {
            (string after, int matches) = replacement.Replace(cell.Text!, left);
            if (matches == 0)
            {
                continue;
            }

            ValueWriter.WriteText((Cell)cell.Holder, after);
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
    /// The units of the range or sheet other than its text cells that hold a match of
    /// <paramref name="op"/>, which <see cref="Replace"/> leaves as they are, matched as the
    /// operation matches a cell's text (regex, caseSensitive and wholeWord) on what a reader sees,
    /// or on a formula's text: one <c>TEXT_NOT_REPLACED</c> warning for each kind of unit, and one for
    /// each part linked to a cell of another sheet. A warning never repeats the find text or the text
    /// that holds the match. Each is reported only if one of its units still holds a match once the
    /// batch has run (<see cref="UnreplacedText"/>).
    /// </summary>
    public static IReadOnlyList<UnreplacedText> Unreached(Worksheet sheet, ReplaceTextOp op)
    {
        TextReplacement replacement = Replacement(op);
        bool Matches(string? text) => text is { Length: > 0 } && replacement.Matches(text, 1).Count > 0;
        bool Holds(CellsTextUnit unit) => unit.Part == TextPart.Formula ? Matches(unit.FormulaText) : unit.Shown.Any(Matches);
        return [.. CellsTextUnits.Cells(sheet, Range(op)).Where(static unit => unit.Part is not null)
            .Concat(CellsTextUnits.Parts(sheet, Range(op), sheet.Name)).Where(Holds)
            .GroupBy(static unit => unit.LinkedCell is null ? (object)unit.Part! : unit)
            .OrderBy(static group => TextPart.All.TakeWhile(part => part != group.First().Part).Count())
            .Select(group =>
            {
                CellsTextUnit[] units = [.. group];
                TextPart part = units[0].Part!;
                (string what, bool several, string location) = group.Key switch
                {
                    CellsTextUnit linked => Linked(linked),
                    _ when part == TextPart.HeaderFooter => HeaderFooter(sheet, units),
                    _ when part == TextPart.Comment => Comments(sheet, units),
                    _ when part == TextPart.ChartTitle => ChartTitles(sheet, units),
                    _ when part == TextPart.AxisTitle => AxisTitles(sheet, units),
                    _ when part == TextPart.TextBox => Drawings(sheet, units),
                    _ => Others(sheet, part, units),
                };
                return Found(op, sheet, part, what, several, location, () => units.Any(Holds));
            })];
    }

    /// <summary>
    /// The formulas, cells that show a number, date, boolean or error, series names, data labels and
    /// alternative text that hold a match, by the names of what holds them, such as <c>the formulas of cells C1, C2</c>.
    /// </summary>
    private static (string, bool, string) Others(Worksheet sheet, TextPart part, CellsTextUnit[] units)
    {
        string[] names = [.. units.Select(static unit => unit.Name).Distinct()];
        string of = part switch
        {
            _ when part == TextPart.Formula => names.Length == 1 ? "the formula of cell" : "the formulas of cells",
            _ when part == TextPart.TypedValue => names.Length == 1 ? "cell" : "cells",
            _ when part == TextPart.ChartData => "the series names of",
            _ when part == TextPart.DataLabel => "the data labels of",
            _ => "the alternative text of",
        };
        return ($"{of} {Named(names)}", units.Length > 1, names.Length == 1 ? units[0].Location : CellsAddresses.OfSheet("sheet", sheet.Name));
    }

    /// <summary>A title, series name, data label or text box that shows the text of a cell of another sheet.</summary>
    private static (string, bool, string) Linked(CellsTextUnit unit)
    {
        string noun = unit.Part == TextPart.ChartTitle ? "title" : unit.Part == TextPart.AxisTitle ? "axis title"
            : unit.Part == TextPart.ChartData ? "series name" : unit.Part == TextPart.DataLabel ? "data label" : "text";
        return ($"the {noun} of {unit.Name}, which shows cell {unit.LinkedCell},", false, unit.Location);
    }

    /// <summary>The header and footer sections that hold a match, named by their part and section, such as <c>the header (center) and the first-page footer (left)</c>.</summary>
    private static (string, bool, string) HeaderFooter(Worksheet sheet, CellsTextUnit[] units)
    {
        HeaderFooterSection[] matched = [.. units.Select(static unit => (HeaderFooterSection)unit.Holder)];
        string[] parts = [.. matched.GroupBy(static section => section.Part)
            .Select(static part => $"{part.Key} ({string.Join(", ", part.Select(static section => HeaderFooterText.SectionName(section.Section)))})")];
        return ($"the {string.Join(" and the ", parts)}", parts.Length > 1, CellsAddresses.OfSheet("page-setup", sheet.Name));
    }

    /// <summary>The comments that hold a match, of the cells of the range when the operation has one, by their cells.</summary>
    private static (string, bool, string) Comments(Worksheet sheet, CellsTextUnit[] units)
    {
        string[] cells = [.. units.Select(static unit => (Comment)unit.Holder).Select(static comment => A1.FormatCell(new CellRef(comment.Row, comment.Column)))];
        return ($"the {(cells.Length == 1 ? "comment of cell" : "comments of cells")} {Named(cells)}", cells.Length > 1,
            cells.Length == 1 ? CellsAddresses.At("comment", sheet.Name, cells[0]) : CellsAddresses.OfSheet("sheet", sheet.Name));
    }

    /// <summary>The chart titles that hold a match, by the number of their chart.</summary>
    private static (string, bool, string) ChartTitles(Worksheet sheet, CellsTextUnit[] units)
    {
        int[] charts = [.. units.Select(static unit => (int)unit.Holder)];
        string numbers = string.Join(", ", charts.Select(static index => (index + 1).ToString(CultureInfo.InvariantCulture)));
        return ($"the {(charts.Length == 1 ? "title of chart" : "titles of charts")} {numbers}", charts.Length > 1,
            charts.Length == 1 ? CellsAddresses.Indexed("chart", sheet.Name, charts[0]) : CellsAddresses.OfSheet("sheet", sheet.Name));
    }

    /// <summary>
    /// The axis titles that hold a match, by the number of their chart and their axes, such as
    /// <c>the axis titles of chart 1 (category, value)</c>.
    /// </summary>
    private static (string, bool, string) AxisTitles(Worksheet sheet, CellsTextUnit[] units)
    {
        (int Chart, string[] Axes)[] matched = [.. units.Select(static unit => ((int Chart, string Axis))unit.Holder)
            .GroupBy(static title => title.Chart).Select(static chart => (chart.Key, chart.Select(static title => title.Axis).ToArray()))];
        string charts = string.Join(", ", matched.Select(static chart => string.Create(CultureInfo.InvariantCulture, $"{chart.Chart + 1} ({string.Join(", ", chart.Axes)})")));
        return ($"the {(units.Length == 1 ? "axis title" : "axis titles")} of {(matched.Length == 1 ? "chart" : "charts")} {charts}", units.Length > 1,
            matched.Length == 1 ? CellsAddresses.Indexed("chart", sheet.Name, matched[0].Chart) : CellsAddresses.OfSheet("sheet", sheet.Name));
    }

    /// <summary>The drawings whose own text holds a match, such as a text box, by their names.</summary>
    private static (string, bool, string) Drawings(Worksheet sheet, CellsTextUnit[] units)
    {
        string[] names = [.. units.Select(static unit => $"'{((Shape)unit.Holder).Name}'")];
        return ($"the text of {(names.Length == 1 ? "drawing" : "drawings")} {Named(names)}", names.Length > 1,
            CellsAddresses.OfSheet("sheet", sheet.Name));
    }

    /// <summary>The first <see cref="NamedItems"/> of <paramref name="items"/> and how many more, such as <c>B1, B2 and 2 more</c>.</summary>
    private static string Named(string[] items) => items.Length <= NamedItems
        ? string.Join(", ", items)
        : string.Create(CultureInfo.InvariantCulture, $"{string.Join(", ", items.Take(NamedItems))} and {items.Length - NamedItems} more");

    // What remains is read only while the sheet is still in its workbook.
    private static UnreplacedText Found(ReplaceTextOp op, Worksheet sheet, TextPart part, string what, bool several, string location, Func<bool> remains) =>
        UnreplacedText.Found(part, op, "replace_text",
            $"Operation {op.Id} (replace_text) found a match in {what} on worksheet '{sheet.Name}' and left "
                + (several ? "them as they were" : "it as it was")
                + (part == TextPart.Formula || part == TextPart.TypedValue ? ": replace_text changes only cells that hold text." : ": replace_text changes the text of cells only."), location, () => sheet.Workbook.Worksheets.Contains(sheet, ReferenceEqualityComparer.Instance) && remains(), docs: "cells/editing");

    private static TextReplacement Replacement(ReplaceTextOp op) =>
        TextReplacement.Create(op.Find, op.Replace, op.Regex, op.CaseSensitive, op.WholeWord);

    private static RangeRef? Range(ReplaceTextOp op) => op.Range is { } text ? A1.ParseRange(text).Range : null;
}
