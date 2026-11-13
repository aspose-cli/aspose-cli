using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Cell hyperlinks — external URLs and internal cross-sheet references.</summary>
internal static class HyperlinkOps
{
    public static void SetHyperlink(Worksheet sheet, SetHyperlinkOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        // The parser guarantees exactly one is set. An internal target is rebuilt as a
        // quoted reference: the engine stores the location verbatim, and Excel cannot
        // follow an unquoted name such as P&L!A1.
        string address = op.Url ?? Sheets.Reference(sheet, op.Target!);
        // set_ replaces: a link that already covers the cell gives way, so a repeat leaves one.
        for (int i = sheet.Hyperlinks.Count - 1; i >= 0; i--)
        {
            CellArea area = sheet.Hyperlinks[i].Area;
            if (cell.Row >= area.StartRow && cell.Row <= area.EndRow && cell.Column >= area.StartColumn && cell.Column <= area.EndColumn)
            {
                sheet.Hyperlinks.RemoveAt(i);
            }
        }

        // The cell shows the caller's text, never the rebuilt reference. The display is the cell's
        // text, which setting it types as PutValue does (#N/A an error, 'abc as abc), so the cell is
        // then written as the text it is, as set_values writes it. Its quote prefix is the display's
        // alone: on an empty cell the engine writes a quoted internal target with one, which writing
        // the display does not drop (probed on 26.9.0).
        string display = op.Display ?? op.Url ?? op.Target!;
        int index = sheet.Hyperlinks.Add(cell.Row, cell.Column, 1, 1, address);
        sheet.Hyperlinks[index].TextToDisplay = display;
        Cell shown = sheet.Cells[cell.Row, cell.Column];
        ValueWriter.DropQuotePrefix(shown);
        ValueWriter.WriteText(shown, display);
    }

    public static void RemoveHyperlink(Worksheet sheet, DeleteHyperlinkOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        for (int i = 0; i < sheet.Hyperlinks.Count; i++)
        {
            CellArea area = sheet.Hyperlinks[i].Area;
            if (cell.Row >= area.StartRow && cell.Row <= area.EndRow
                && cell.Column >= area.StartColumn && cell.Column <= area.EndColumn)
            {
                sheet.Hyperlinks.RemoveAt(i);
                return;
            }
        }

        throw NotFound(sheet, op.Cell);
    }

    /// <summary>
    /// <c>HYPERLINK_NOT_FOUND</c> for a cell no hyperlink covers, listing the areas the sheet's
    /// hyperlinks cover in collection order.
    /// </summary>
    private static CliException NotFound(Worksheet sheet, string cell)
    {
        var areas = new string[sheet.Hyperlinks.Count];
        for (int i = 0; i < areas.Length; i++)
        {
            CellArea area = sheet.Hyperlinks[i].Area;
            areas[i] = A1.FormatRange(new RangeRef(
                new CellRef(area.StartRow, area.StartColumn),
                new CellRef(area.EndRow, area.EndColumn)));
        }

        return CliErrors.NotFound(CellsDiagnostics.HyperlinkNotFound, "hyperlink", cell, areas);
    }
}
