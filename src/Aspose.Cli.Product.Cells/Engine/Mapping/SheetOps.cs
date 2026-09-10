using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Sheet-level ops: adding, renaming, deleting, showing/hiding and freezing
/// panes. None report a cell count.
/// </summary>
internal static class SheetOps
{
    public static long? AddSheet(Workbook workbook, AddSheetOp op)
    {
        int index = workbook.Worksheets.Add();
        Worksheet sheet = workbook.Worksheets[index];
        sheet.Name = op.Name;
        if (op.Position is { } position)
        {
            sheet.MoveTo(position);
        }

        return null;
    }

    public static long? RenameSheet(Worksheet sheet, RenameSheetOp op)
    {
        sheet.Name = op.To;
        return null;
    }

    public static long? DeleteSheet(Workbook workbook, Worksheet sheet)
    {
        workbook.Worksheets.RemoveAt(sheet.Index);
        return null;
    }

    public static long? SetVisibility(Worksheet sheet, SetSheetVisibilityOp op)
    {
        sheet.IsVisible = !op.Hidden;
        return null;
    }

    public static long? SetActiveSheet(Workbook workbook, Worksheet sheet)
    {
        // Excel cannot activate a hidden tab. Keep this explicit instead of
        // relying on the SDK to ignore or normalize an invalid selection.
        if (!sheet.IsVisible)
        {
            throw CellsErrors.OpsInvalid($"sheet '{sheet.Name}' is hidden; show it before making it active");
        }

        workbook.Worksheets.ActiveSheetIndex = sheet.Index;
        return null;
    }

    public static long? MoveSheet(Worksheet sheet, MoveSheetOp op)
    {
        // The engine re-scopes cross-sheet and 3-D references to the new tab
        // order. A position past the last sheet is clamped to the end, matching
        // add_sheet's positioning; the parser guarantees position >= 0.
        sheet.MoveTo(op.Position);
        return null;
    }

    public static long? Freeze(Worksheet sheet, FreezePanesOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        if (cell is { Row: 0, Column: 0 })
        {
            sheet.UnFreezePanes();
        }
        else
        {
            sheet.FreezePanes(cell.Row, cell.Column, cell.Row, cell.Column);
        }

        return null;
    }
}
