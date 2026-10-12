using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>AutoFilter and in-place range sorting.</summary>
internal static class SortFilterOps
{
    public static long? SetAutoFilter(Worksheet sheet, SetAutoFilterOp op)
    {
        sheet.AutoFilter.Range = op.Range;
        return null;
    }

    /// <summary>Removes the AutoFilter; 1 when the sheet had one, null when there was nothing to remove.</summary>
    public static long? ClearAutoFilter(Worksheet sheet)
    {
        bool had = !string.IsNullOrEmpty(sheet.AutoFilter.Range);
        sheet.RemoveAutoFilter();
        return had ? 1 : null;
    }

    public static long? SortRange(Workbook workbook, Worksheet sheet, SortRangeOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;

        DataSorter sorter = workbook.DataSorter;
        sorter.Clear();
        sorter.HasHeaders = op.HasHeader;

        foreach (CellsSortKey key in op.By)
        {
            int column = A1.ParseColumn(key.Column);
            sorter.AddKey(
                column,
                key.Order == CellsSortOrders.Desc ? SortOrder.Descending : SortOrder.Ascending);
        }

        sorter.Sort(sheet.Cells, range.Start.Row, range.Start.Column, range.End.Row, range.End.Column);
        return range.CellCount;
    }
}
