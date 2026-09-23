using Aspose.Cli.Sdk.Operations;
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
        Action<ListObject>? applyStyle = op.Style is { } style ? ResolveStyle(sheet.Workbook, style) : null;
        int index = sheet.ListObjects.Add(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, hasHeaders: true);

        ListObject table = sheet.ListObjects[index];
        if (op.Name is { } name)
        {
            table.DisplayName = name;
        }

        applyStyle?.Invoke(table);
        if (op.TotalsRow is true)
        {
            table.ShowTotals = true;
        }

        return range.CellCount;
    }

    /// <summary>
    /// Resolves a style name before the table exists: a built-in style by its exact
    /// name (<c>TableStyleMedium2</c>, never an enum number), otherwise a custom
    /// style the workbook defines. The engine would store any other name verbatim.
    /// </summary>
    private static Action<ListObject> ResolveStyle(Workbook workbook, string name)
    {
        foreach (TableStyleType type in Enum.GetValues<TableStyleType>())
        {
            if (type is not (TableStyleType.None or TableStyleType.Custom)
                && string.Equals(type.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return table => table.TableStyleType = type;
            }
        }

        if (workbook.Worksheets.TableStyles[name] is { } custom)
        {
            return table => table.TableStyleName = custom.Name;
        }

        throw new OperationInvalidException(
            $"table style '{name}' is neither built in nor defined in the workbook",
            hint: "Use a built-in style such as TableStyleLight1-21, TableStyleMedium1-28 or TableStyleDark1-11.");
    }
}
