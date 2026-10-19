using Aspose.Cells;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Replaces text within the text values of a sheet's cells.</summary>
internal static class TextReplaceOps
{
    /// <summary>
    /// Replaces the matches of <paramref name="op"/> in the text cells of its range, row by row,
    /// up to its replacement limit, and returns how many matches it replaced. A formula, and a
    /// value of any other type, is never touched.
    /// </summary>
    public static long Replace(Worksheet sheet, ReplaceTextOp op)
    {
        TextReplacement replacement = TextReplacement.Create(op.Find, op.Replace, op.Regex, op.CaseSensitive, op.WholeWord);
        RangeRef? range = op.Range is { } text ? A1.ParseRange(text).Range : null;
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

    // The stored text cells of the range, or of the sheet, in address order; a snapshot of the list,
    // since writing a cell must not disturb the enumeration.
    private static List<Cell> TextCells(Worksheet sheet, RangeRef? range)
    {
        var cells = new List<Cell>();
        foreach (Cell cell in sheet.Cells)
        {
            if (cell.Type == CellValueType.IsString && !cell.IsFormula
                && (range is not { } area || (cell.Row >= area.Start.Row && cell.Row <= area.End.Row
                    && cell.Column >= area.Start.Column && cell.Column <= area.End.Column)))
            {
                cells.Add(cell);
            }
        }

        cells.Sort(static (a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
        return cells;
    }
}
