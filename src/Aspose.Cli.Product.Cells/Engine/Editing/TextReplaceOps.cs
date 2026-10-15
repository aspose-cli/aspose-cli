using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk.Operations;
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
        string pattern = op.Regex ? op.Find : Regex.Escape(op.Find);
        if (op.WholeWord)
        {
            pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
        }

        Regex find = SafeRegex.Create(pattern, op.CaseSensitive);
        // A literal replacement is inserted verbatim; a regex one honors $1 and ${name}.
        string replacement = op.Regex ? op.Replace : op.Replace.Replace("$", "$$", StringComparison.Ordinal);
        RangeRef? range = op.Range is { } text ? A1.ParseRange(text).Range : null;
        long left = op.MaxReplacementCount ?? long.MaxValue;
        long replaced = 0;
        try
        {
            foreach (Cell cell in TextCells(sheet, range))
            {
                string before = cell.StringValue;
                int matches = 0;
                string after = find.Replace(before, match =>
                {
                    if (matches >= left)
                    {
                        return match.Value;
                    }

                    matches++;
                    return match.Result(replacement);
                });
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
        }
        catch (RegexMatchTimeoutException)
        {
            throw new OperationInvalidException(
                $"find took longer than {SafeRegex.DefaultTimeout.TotalSeconds:0} second to match a cell's text",
                "Simplify the regular expression, for example by removing nested repetition such as (a+)+.");
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
