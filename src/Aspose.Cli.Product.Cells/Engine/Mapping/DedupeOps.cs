using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Removes duplicate rows from a range, comparing all columns or a subset.</summary>
internal static class DedupeOps
{
    public static long? RemoveDuplicates(Worksheet sheet, RemoveDuplicatesOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        bool hasHeaders = op.HasHeader ?? false;

        // The engine takes column offsets within the range, not absolute columns;
        // an omitted subset means "every column in the range".
        int[] offsets = op.Columns is { Count: > 0 } columns
            ? columns.Select(column => A1.ParseColumn(column) - range.Start.Column).ToArray()
            : Enumerable.Range(0, range.ColumnCount).ToArray();

        sheet.Cells.RemoveDuplicates(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, hasHeaders, offsets);

        return range.CellCount;
    }
}
