using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>AutoFilter and in-place range sorting.</summary>
internal static class SortFilterOps
{
    public static void SetAutoFilter(Worksheet sheet, SetAutoFilterOp op)
    {
        sheet.AutoFilter.Range = op.Range;
    }

    /// <summary>Removes the AutoFilter.</summary>
    public static void ClearAutoFilter(Worksheet sheet)
    {
        sheet.RemoveAutoFilter();
    }

    public static void SortRange(Workbook workbook, Worksheet sheet, SortRangeOp op)
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
    }
}
