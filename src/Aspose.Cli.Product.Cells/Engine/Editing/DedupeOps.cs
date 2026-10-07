using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Removes duplicate rows from a range, comparing all columns or a subset, and reports the rows
/// removed.
/// </summary>
internal static class DedupeOps
{
    public static long? RemoveDuplicates(Worksheet sheet, RemoveDuplicatesOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;

        // The engine takes column offsets within the range, not absolute columns;
        // an omitted subset means "every column in the range".
        int[] offsets = op.Columns is { } columns
            ? columns.Select(column => A1.ParseColumn(column) - range.Start.Column).ToArray()
            : Enumerable.Range(0, range.ColumnCount).ToArray();

        int before = FilledRows(sheet.Cells, range);
        sheet.Cells.RemoveDuplicates(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, op.HasHeader, offsets);

        // The engine reports nothing; the removed rows are the filled rows that are gone.
        return before - FilledRows(sheet.Cells, range);
    }

    private static int FilledRows(Aspose.Cells.Cells cells, RangeRef range)
    {
        int filled = 0;
        for (int row = range.Start.Row; row <= range.End.Row; row++)
        {
            for (int column = range.Start.Column; column <= range.End.Column; column++)
            {
                if (cells.CheckCell(row, column) is { Value: not (null or "") })
                {
                    filled++;
                    break;
                }
            }
        }

        return filled;
    }
}
