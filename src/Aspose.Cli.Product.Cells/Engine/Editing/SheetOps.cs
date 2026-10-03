using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Sheet-level ops: adding, renaming, deleting, showing/hiding and freezing
/// panes. Adding counts the one sheet it adds; the others report no count.
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

        return 1;
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
            throw new OperationInvalidException($"sheet '{sheet.Name}' is hidden; show it before making it active");
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
