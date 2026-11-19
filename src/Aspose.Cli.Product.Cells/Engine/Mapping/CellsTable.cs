using System.Drawing;
using Aspose.Cells;
using Aspose.Cli.Sdk.Tables;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The Cells object table: one name for each Aspose.Cells member a read gets and a set sets,
/// where each kind of object sits, and the engine calls that add, delete and move objects. Its
/// addresses are the engine's own: a worksheet by its name, a cell, range, row or column in A1
/// notation, a chart or picture by its shape id, other objects by their position, from 0, in the
/// engine's collection.
/// </summary>
internal static partial class CellsTable
{
    /// <summary>The workbook itself, addressed as <c>workbook</c>.</summary>
    internal static readonly ObjectKind<Workbook> WorkbookKind = new("workbook", "The workbook.")
    {
        Row.Text<Workbook>("activeSheetName", "Workbook.Worksheets.ActiveSheetName", static w => w.Worksheets.ActiveSheetName, static (w, v) => w.Worksheets.ActiveSheetName = v),
        Row.Text<Workbook>("defaultStyle.font.name", "Workbook.DefaultStyle.Font.Name; set on the DefaultStyle, which is then set back",
            static w => w.DefaultStyle.Font.Name,
            static (w, v) =>
            {
                Style style = w.DefaultStyle;
                style.Font.Name = v;
                w.DefaultStyle = style;
            }),
        Row.Number<Workbook>("defaultStyle.font.doubleSize", "Workbook.DefaultStyle.Font.DoubleSize, in points; set on the DefaultStyle, which is then set back",
            static w => w.DefaultStyle.Font.DoubleSize,
            static (w, v) =>
            {
                Style style = w.DefaultStyle;
                style.Font.DoubleSize = v;
                w.DefaultStyle = style;
            }),
        Row.Enum<Workbook, ProtectionType>("settings.protectionType", "Workbook.Settings.ProtectionType; the protect_workbook and unprotect_workbook actions change it", static w => w.Settings.ProtectionType),
        Row.Boolean<Workbook>("isWorkbookProtectedWithPassword", "Workbook.IsWorkbookProtectedWithPassword", static w => w.IsWorkbookProtectedWithPassword),
    };

    /// <summary>A worksheet, addressed by its name: <c>sheet/Sales</c>.</summary>
    internal static readonly ObjectKind<Worksheet> SheetKind = new("sheet", "A worksheet or chart sheet of the workbook.")
    {
        Row.Text<Worksheet>("name", "Worksheet.Name", static s => s.Name, static (s, v) => s.Name = v),
        Row.Integer<Worksheet>("index", "Worksheet.Index: the sheet's position, from 0; move changes it", static s => s.Index),
        Row.Enum<Worksheet, SheetType>("type", "Worksheet.Type", static s => s.Type),
        Row.Enum<Worksheet, VisibilityType>("visibilityType", "Worksheet.VisibilityType", static s => s.VisibilityType, static (s, v) => s.VisibilityType = v),
        Row.Of<Worksheet, Color?>("tabColor", ValueKinds.Optional(ValueKinds.Color), "Worksheet.TabColor; null sets Color.Empty, no tab color",
            static s => s.TabColor, static (s, v) => s.TabColor = v ?? Color.Empty),
        Row.Boolean<Worksheet>("isGridlinesVisible", "Worksheet.IsGridlinesVisible", static s => s.IsGridlinesVisible, static (s, v) => s.IsGridlinesVisible = v),
        Row.Boolean<Worksheet>("isRowColumnHeadersVisible", "Worksheet.IsRowColumnHeadersVisible", static s => s.IsRowColumnHeadersVisible, static (s, v) => s.IsRowColumnHeadersVisible = v),
        Row.Integer<Worksheet>("zoom", "Worksheet.Zoom, in percent", static s => s.Zoom, static (s, v) => s.Zoom = v),
        Row.Boolean<Worksheet>("isProtected", "Worksheet.IsProtected; the protect_sheet and unprotect_sheet actions change it", static s => s.IsProtected),
        Row.Text<Worksheet>("autoFilter.range", "Worksheet.AutoFilter.Range, an area such as A1:F20; null without an AutoFilter", static s => s.AutoFilter.Range, static (s, v) => s.AutoFilter.Range = v),
        Row.Of<Worksheet, CellsValues.FreezedPanes?>("freezedPanes", CellsValues.Panes, "Worksheet.GetFreezedPanes(out row, out column, out freezedRows, out freezedColumns), the cell as A1 writes it; null when the panes are not frozen; the freeze_panes and unfreeze_panes actions change it",
            static s => s.GetFreezedPanes(out int row, out int column, out int rows, out int columns) ? new CellsValues.FreezedPanes(CellsHelper.CellIndexToName(row, column), rows, columns) : null),
        Row.Text<Worksheet>("maxDisplayRange", "Worksheet.Cells.MaxDisplayRange.Address: the area that holds the sheet's data, merged cells and shapes, which --at <sheet>/range/<area> reads; null for an empty sheet",
            static s => s.Cells.MaxDisplayRange?.Address),
    };

    /// <summary>A cell, addressed by its A1 name: <c>sheet/Sales/cell/B6</c>.</summary>
    internal static readonly ObjectKind<Cell> CellKind = new("cell", "A cell of a worksheet. Every cell the sheet's grid has can be addressed; read lists the cells the engine holds.")
    {
        Row.Enum<Cell, CellValueType>("type", "Cell.Type", static c => c.Type),
        Row.Of<Cell, object?>("value", CellsValues.CellValue, "Cell.Value, a date as Cell.DoubleValue; set with Cell.PutValue of the value's type", CellsValues.Of, CellsValues.Put),
        Row.Text<Cell>("formula", "Cell.Formula: null in a cell without one; a formula set is not calculated until the calculate action", static c => c.Formula, static (c, v) => c.Formula = v),
        Row.Text<Cell>("formulaR1C1", "Cell.R1C1Formula", static c => c.R1C1Formula, static (c, v) => c.R1C1Formula = v),
        Row.Text<Cell>("stringValue", "Cell.StringValue: the value as the cell's number format shows it", static c => c.StringValue),
        Row.Boolean<Cell>("isMerged", "Cell.IsMerged", static c => c.IsMerged),
        Row.Boolean<Cell>("isDynamicArrayFormula", "Cell.IsDynamicArrayFormula; the set_dynamic_array_formula action sets one", static c => c.IsDynamicArrayFormula),
    };

    /// <summary>A cell's style: <c>sheet/Sales/cell/B6/style</c>.</summary>
    internal static readonly ObjectKind<Cell> StyleKind = new("style", "A cell's style: each row is one member of Cell.GetStyle(), set alone with Cell.SetStyle(style).")
    {
        CellsStyles.CellRows,
    };

    /// <summary>A range, addressed by its A1 area: <c>sheet/Sales/range/A1:F6</c>.</summary>
    internal static readonly ObjectKind<Aspose.Cells.Range> RangeKind = new("range", "A range of a worksheet, made with Cells.CreateRange(area); read does not list ranges, --at reads one. Its value holds every cell of the area: a whole column is 1,048,576 rows, so read a sheet's data at the area its maxDisplayRange gives.")
    {
        Row.Of<Aspose.Cells.Range, object?[][]>("value", CellsValues.Matrix, "Range.Value: one list per row, one value per column, a date as its serial number; set, the value has the range's numbers of rows and columns",
            CellsValues.Rows, CellsValues.SetRows),
    };

    /// <summary>The table, built on first use, once every kind is initialized.</summary>
    internal static DocumentTable<Workbook> Definition => Built.Value;

    private static class Built
    {
        internal static readonly DocumentTable<Workbook> Value = Build();
    }

    private static DocumentTable<Workbook> Build()
    {
        var table = new DocumentTable<Workbook>(WorkbookKind);
        table.Place(WorkbookKind, SheetKind, new Locator<Workbook, Worksheet>
        {
            Segment = "sheet",
            Key = [KeyPart.Text],
            // The engine's lookup by name ignores case: a name two sheets share but for case matches both.
            FindAll = static (w, key) => [.. w.Worksheets.Where(sheet => string.Equals(sheet.Name, key.Name(), StringComparison.OrdinalIgnoreCase))],
            List = static w => w.Worksheets,
            KeyOf = static (_, sheet) => Key.Of(sheet.Name),
            Delete = static (w, sheet) => w.Worksheets.RemoveAt(sheet.Index),
            Move = static (_, sheet, to) => sheet.MoveTo(to),
        });
        table.Adds(WorkbookKind, SheetKind, new Adder<Workbook, Worksheet>
        {
            Name = "sheet",
            Description = "WorksheetCollection.Insert(int index, SheetType sheetType, string sheetName)",
            Arguments =
            [
                Argument.Of("index", ValueKinds.Integer, "The new sheet's position, the engine's, from 0."),
                Argument.Of("sheetType", ValueKinds.Enum<SheetType>(), "The engine's SheetType."),
                Argument.Of("sheetName", ValueKinds.Text, "The new sheet's name."),
            ],
            Create = static (w, a) => w.Worksheets.Insert(a.Get<int>("index"), a.Get<SheetType>("sheetType"), a.Get<string>("sheetName")),
        });
        table.Place(SheetKind, CellKind, new Locator<Worksheet, Cell>
        {
            Segment = "cell",
            Key = [KeyPart.CellName],
            Find = static (sheet, key) => sheet.Cells[key.Name()],
            List = static sheet => sheet.Cells.Cast<Cell>(),
            KeyOf = static (_, cell) => Key.Of(cell.Name),
            ListedWhenNamed = true,
        });
        table.Place(CellKind, StyleKind, new Locator<Cell, Cell>
        {
            Segment = "style",
            Find = static (cell, _) => cell,
            List = static cell => [cell],
            KeyOf = static (_, _) => Key.None,
            ListedWhenNamed = true,
        });
        table.Place(SheetKind, RangeKind, new Locator<Worksheet, Aspose.Cells.Range>
        {
            Segment = "range",
            Key = [KeyPart.Area],
            Find = static (sheet, key) => sheet.Cells.CreateRange(key.Name()),
            List = static _ => [],
            KeyOf = static (_, range) => Key.Of(range.Address),
        });
        PlaceSheetObjects(table);
        PlaceCharts(table);
        CellsActions.Declare(table);
        table.SearchesText(CellKind, "stringValue");
        table.SearchesText(CellKind, "formula");
        table.SearchesText(CommentKind, "note");
        table.Inventory(CellsInventory.Kinds, CellsInventory.Count);
        return table;
    }
}
