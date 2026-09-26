using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cells.Tables;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Turns ranges into native tables (ListObjects) with style and totals.</summary>
internal static class TableOps
{
    public static long? CreateTable(Worksheet sheet, CreateTableOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        Action<ListObject>? applyStyle = op.Style is { } style ? ResolveStyle(sheet.Workbook, style) : null;
        if (op.Name is { } requested)
        {
            RequireUnusedName(sheet.Workbook, requested);
        }

        int index = sheet.ListObjects.Add(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, hasHeaders: true);

        ListObject table = sheet.ListObjects[index];
        if (op.Name is { } name)
        {
            table.DisplayName = name;
        }

        applyStyle?.Invoke(table);
        if (op.TotalsRow)
        {
            table.ShowTotals = true;
        }

        return range.CellCount;
    }

    /// <summary>
    /// Excel requires a table name to be unique among the workbook's tables and defined names.
    /// The engine stores a second table with a taken name without complaint (probed on 26.9.0),
    /// so the check runs before the table exists.
    /// </summary>
    private static void RequireUnusedName(Workbook workbook, string name)
    {
        bool taken = workbook.Worksheets.Any(worksheet => worksheet.ListObjects.Any(
                table => string.Equals(table.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
            || workbook.Worksheets.Names.Any(defined => string.Equals(defined.Text, name, StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            throw new OperationInvalidException(
                $"a table or defined name '{name}' already exists in the workbook",
                hint: "Choose a name that no table or defined name in the workbook uses.");
        }
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
