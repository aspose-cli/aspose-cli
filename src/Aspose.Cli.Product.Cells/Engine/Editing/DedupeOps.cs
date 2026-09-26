using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Removes duplicate rows from a range, comparing all columns or a subset.</summary>
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

        sheet.Cells.RemoveDuplicates(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, op.HasHeader, offsets);

        return range.CellCount;
    }
}
