using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Row and column outline grouping — the collapsible +/- summaries in Excel.
/// Contract coordinates are A1-style (1-based rows, letter columns); they are
/// converted to the engine's zero-based indexes exactly once, here.
/// </summary>
internal static class OutlineOps
{
    public static long? GroupRows(Worksheet sheet, GroupRowsOp op)
    {
        sheet.Cells.GroupRows(op.From - 1, (op.To ?? op.From) - 1, op.Collapse);
        return null;
    }

    public static long? UngroupRows(Worksheet sheet, UngroupRowsOp op)
    {
        sheet.Cells.UngroupRows(op.From - 1, (op.To ?? op.From) - 1);
        return null;
    }

    public static long? GroupColumns(Worksheet sheet, GroupColumnsOp op)
    {
        int first = ColumnIndex(op.From);
        int last = op.To is { } to ? ColumnIndex(to) : first;
        sheet.Cells.GroupColumns(first, last, op.Collapse);
        return null;
    }

    public static long? UngroupColumns(Worksheet sheet, UngroupColumnsOp op)
    {
        int first = ColumnIndex(op.From);
        int last = op.To is { } to ? ColumnIndex(to) : first;
        sheet.Cells.UngroupColumns(first, last);
        return null;
    }

    /// <summary>Column letters to zero-based index (validated by the parser).</summary>
    private static int ColumnIndex(string letters) => A1.ParseColumn(letters);
}
