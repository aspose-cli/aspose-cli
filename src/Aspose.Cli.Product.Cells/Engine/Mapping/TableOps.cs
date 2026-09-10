using Aspose.Cells;
using Aspose.Cells.Tables;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Turns ranges into native tables (ListObjects) with style and totals.</summary>
internal static class TableOps
{
    public static long? CreateTable(Worksheet sheet, CreateTableOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        int index = sheet.ListObjects.Add(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, hasHeaders: true);

        ListObject table = sheet.ListObjects[index];
        if (op.Name is { } name)
        {
            table.DisplayName = name;
        }

        if (op.Style is { } style)
        {
            // Built-in styles are named like the TableStyleType enum members
            // ("TableStyleMedium2"); anything else is treated as a custom style.
            if (Enum.TryParse(style, ignoreCase: true, out TableStyleType styleType))
            {
                table.TableStyleType = styleType;
            }
            else
            {
                table.TableStyleName = style;
            }
        }

        if (op.TotalsRow is true)
        {
            table.ShowTotals = true;
        }

        return range.CellCount;
    }
}
