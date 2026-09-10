using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Row and column structure operations. Contract coordinates are A1-style
/// (1-based rows, letter columns); everything is converted to the engine's
/// zero-based indexes exactly once, here.
/// </summary>
internal static class RowColumnOps
{
    // Structural inserts/deletes must update references ACROSS the whole
    // workbook, not just the edited sheet. The default InsertRows/DeleteRows
    // overloads adjust only same-sheet formulas, so a =SUM(Data!A1:A4) on
    // another sheet silently kept its old bounds after a row was inserted into
    // Data's span — a same-sheet SUM expanded, the cross-sheet one did not, and
    // the summary a human reads under-counted with no error (probed on 26.6.0).
    // DeleteColumns already passed updateReference:true; the other three now
    // match it.
    private static readonly InsertOptions UpdateAll = new() { UpdateReference = true };

    public static long? InsertRows(Worksheet sheet, InsertRowsOp op)
    {
        sheet.Cells.InsertRows(op.At - 1, op.Count ?? 1, UpdateAll);
        return null;
    }

    public static long? DeleteRows(Worksheet sheet, DeleteRowsOp op)
    {
        sheet.Cells.DeleteRows(op.At - 1, op.Count ?? 1, updateReference: true);
        return null;
    }

    public static long? InsertColumns(Worksheet sheet, InsertColumnsOp op)
    {
        sheet.Cells.InsertColumns(ColumnIndex(op.At), op.Count ?? 1, UpdateAll);
        return null;
    }

    public static long? DeleteColumns(Worksheet sheet, DeleteColumnsOp op)
    {
        sheet.Cells.DeleteColumns(ColumnIndex(op.At), op.Count ?? 1, updateReference: true);
        return null;
    }

    public static long? ResizeRows(Worksheet sheet, ResizeRowsOp op)
    {
        int first = op.From - 1;
        int last = (op.To ?? op.From) - 1;

        if (op.Height is { } height)
        {
            for (int row = first; row <= last; row++)
            {
                sheet.Cells.SetRowHeight(row, height);
            }
        }
        else
        {
            sheet.AutoFitRows(first, last);
        }

        return null;
    }

    public static long? ResizeColumns(Worksheet sheet, ResizeColumnsOp op)
    {
        int first = ColumnIndex(op.From);
        int last = op.To is { } to ? ColumnIndex(to) : first;

        if (op.Width is { } width)
        {
            for (int column = first; column <= last; column++)
            {
                sheet.Cells.SetColumnWidth(column, width);
            }
        }
        else
        {
            sheet.AutoFitColumns(first, last);
        }

        return null;
    }

    /// <summary>Column letters to zero-based index (validated by the parser).</summary>
    private static int ColumnIndex(string letters) => A1.ParseColumn(letters);
}
