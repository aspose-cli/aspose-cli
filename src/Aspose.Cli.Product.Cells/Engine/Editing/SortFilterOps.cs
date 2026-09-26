using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>AutoFilter and in-place range sorting.</summary>
internal static class SortFilterOps
{
    public static long? SetAutoFilter(Worksheet sheet, SetAutoFilterOp op)
    {
        if (op.Off)
        {
            sheet.RemoveAutoFilter();
            return null;
        }

        // The parser guarantees Range is set unless Off is true.
        sheet.AutoFilter.Range = op.Range!;
        return null;
    }

    public static long? SortRange(Workbook workbook, Worksheet sheet, SortRangeOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;

        DataSorter sorter = workbook.DataSorter;
        sorter.Clear();
        sorter.HasHeaders = op.HasHeader;

        foreach (SortKey key in op.By)
        {
            int column = A1.ParseColumn(key.Column);
            sorter.AddKey(
                column,
                key.Order == SortOrders.Desc ? SortOrder.Descending : SortOrder.Ascending);
        }

        sorter.Sort(sheet.Cells, range.Start.Row, range.Start.Column, range.End.Row, range.End.Column);
        return range.CellCount;
    }
}
