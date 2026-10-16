using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cells.Tables;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Turns ranges into native tables (ListObjects) with style and totals.</summary>
internal static class TableOps
{
    public static void CreateTable(Worksheet sheet, DefineTableOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        Action<ListObject>? applyStyle = op.TableStyle is { } style ? ResolveStyle(sheet.Workbook, style) : null;
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
    }

    /// <summary>
    /// Moves a table's last row, as Excel's Resize Table does when only the rows change. The
    /// header row stays as it is, so the new range spans the table's columns from its top-left
    /// cell, and keeps a data row below the header: the engine names the header of a column it
    /// adds itself (Column5) and breaks the formulas of a column it drops. A table with a totals
    /// row only grows: shrunk, the engine turns the last data row it keeps into the totals row
    /// and leaves the old totals formulas in the rows it drops. Grown, it writes the totals row
    /// over the new last row, so that row must be empty.
    /// </summary>
    public static void ResizeTable(Worksheet sheet, ResizeTableOp op)
    {
        ListObject table = Find(sheet, op.Name)
            ?? throw CliErrors.NotFound(CellsDiagnostics.TableNotFound, "table", op.Name, [.. sheet.ListObjects.Select(static candidate => candidate.DisplayName)],
                hint: "Name the table's sheet in 'sheet'; 'aspose-cli cells inspect <file> --detail tables' lists every table with its sheet.");
        RangeRef range = A1.ParseRange(op.Range).Range;
        var start = new CellRef(table.StartRow, table.StartColumn);
        string current = A1.FormatRange(new RangeRef(start, new CellRef(table.EndRow, table.EndColumn)));

        // The CLI cannot shrink a table with a totals row, so no suggestion ends above its last row.
        int lowest = table.ShowTotals ? table.EndRow : start.Row + 1;
        if (range.Start != start || range.End.Column != table.EndColumn || range.End.Row <= start.Row)
        {
            string suggested = A1.FormatRange(new RangeRef(start, new CellRef(Math.Max(range.End.Row, lowest), table.EndColumn)));
            throw new OperationInvalidException(
                $"the new range {op.Range} of table '{table.DisplayName}' must start at {A1.FormatCell(start)}, end in column {A1.ColumnName(table.EndColumn)} "
                    + $"and keep a data row below the header; the table covers {current}",
                hint: $"Keep the table's header row and columns and change only its last row, such as {suggested}.");
        }

        if (table.ShowTotals && range.End.Row < table.EndRow)
        {
            string grown = A1.FormatRange(new RangeRef(start, new CellRef(table.EndRow + 1, table.EndColumn)));
            throw new OperationInvalidException(
                $"table '{table.DisplayName}' has a totals row, so its new range {op.Range} cannot end above its last row {table.EndRow + 1}; the table covers {current}",
                hint: $"The CLI cannot shrink a table with a totals row, only extend it: give a range that ends at row {table.EndRow + 1} or below, such as {grown}.");
        }

        // The engine writes the totals row over whatever the new last row holds, so a totals row
        // lands only on an empty row.
        if (table.ShowTotals && range.End.Row > table.EndRow && !IsEmptyRow(sheet, range.End.Row, start.Column, table.EndColumn))
        {
            int empty = range.End.Row + 1;
            while (empty < A1.MaxRows - 1 && !IsEmptyRow(sheet, empty, start.Column, table.EndColumn))
            {
                empty++;
            }

            string landing = A1.FormatRange(new RangeRef(start, new CellRef(empty, table.EndColumn)));
            throw new OperationInvalidException(
                $"table '{table.DisplayName}' has a totals row, which would be written over the values in row {range.End.Row + 1}, the last row of {op.Range}; the table covers {current}",
                hint: $"End the range on an empty row, such as {landing}: the totals row moves there, and its old row {table.EndRow + 1} becomes an empty data row of the table.");
        }

        table.Resize(range.Start.Row, range.Start.Column, range.End.Row, range.End.Column, hasHeaders: true);
    }

    /// <summary>Whether the row holds no value and no formula in the columns.</summary>
    private static bool IsEmptyRow(Worksheet sheet, int row, int firstColumn, int lastColumn)
    {
        for (int column = firstColumn; column <= lastColumn; column++)
        {
            if (sheet.Cells.CheckCell(row, column) is { } cell && (cell.IsFormula || cell.Type != CellValueType.IsNull))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The table of the sheet with the name, ignoring case as Excel does; null when it has none.</summary>
    internal static ListObject? Find(Worksheet sheet, string name) => sheet.ListObjects.FirstOrDefault(
        table => string.Equals(table.DisplayName, name, StringComparison.OrdinalIgnoreCase));

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
        TableStyleType[] builtIn = Enum.GetValues<TableStyleType>()
            .Where(static type => type is not (TableStyleType.None or TableStyleType.Custom))
            .ToArray();
        foreach (TableStyleType type in builtIn)
        {
            if (string.Equals(type.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return table => table.TableStyleType = type;
            }
        }

        TableStyleCollection customStyles = workbook.Worksheets.TableStyles;
        if (customStyles[name] is { } custom)
        {
            return table => table.TableStyleName = custom.Name;
        }

        // The workbook's own styles come first so the listed names include them.
        var available = new List<string>(customStyles.Count + builtIn.Length);
        for (int i = 0; i < customStyles.Count; i++)
        {
            available.Add(customStyles[i].Name);
        }

        available.AddRange(builtIn.Select(static type => type.ToString()));
        throw CliErrors.NotFound(ErrorCodes.StyleNotFound, "table style", name, available);
    }
}
