using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Sheet-level ops: adding, renaming, deleting, showing/hiding and freezing
/// panes. Adding counts the one sheet it adds; the others report no count.
/// </summary>
internal static class SheetOps
{
    public static void AddSheet(Workbook workbook, InsertSheetOp op)
    {
        int index = workbook.Worksheets.Add();
        Worksheet sheet = workbook.Worksheets[index];
        sheet.Name = op.Name;
        if (op.At is { } at)
        {
            sheet.MoveTo(at - 1);
        }
    }

    public static void RenameSheet(Worksheet sheet, RenameSheetOp op)
    {
        sheet.Name = op.Name;
    }

    public static void DeleteSheet(Workbook workbook, Worksheet sheet)
    {
        workbook.Worksheets.RemoveAt(sheet.Index);
    }

    public static void SetVisibility(Worksheet sheet, SetSheetVisibilityOp op)
    {
        sheet.IsVisible = !op.Hidden;
    }

    public static void SetActiveSheet(Workbook workbook, Worksheet sheet)
    {
        // Excel cannot activate a hidden tab. Keep this explicit instead of
        // relying on the SDK to ignore or normalize an invalid selection.
        if (!sheet.IsVisible)
        {
            throw new OperationInvalidException($"sheet '{sheet.Name}' is hidden; show it before making it active");
        }

        workbook.Worksheets.ActiveSheetIndex = sheet.Index;
    }

    public static void MoveSheet(Worksheet sheet, MoveSheetOp op)
    {
        // The engine re-scopes cross-sheet and 3-D references to the new tab
        // order. A position past the last sheet is clamped to the end, matching
        // insert_sheet's positioning; the parser guarantees to >= 1.
        sheet.MoveTo(op.To - 1);
    }

    public static void Freeze(Worksheet sheet, FreezePanesOp op)
    {
        // The parser refuses A1, which freezes nothing.
        CellRef cell = A1.ParseCell(op.Cell);
        sheet.FreezePanes(cell.Row, cell.Column, cell.Row, cell.Column);
    }

    /// <summary>Removes frozen panes.</summary>
    public static void Unfreeze(Worksheet sheet)
    {
        sheet.UnFreezePanes();
    }
}
