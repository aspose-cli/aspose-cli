using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Cell hyperlinks — external URLs and internal cross-sheet references.</summary>
internal static class HyperlinkOps
{
    public static long? SetHyperlink(Worksheet sheet, SetHyperlinkOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        // The parser guarantees exactly one is set. An internal target is rebuilt as a
        // quoted reference: the engine stores the location verbatim, and Excel cannot
        // follow an unquoted name such as P&L!A1.
        string address = op.Url ?? Sheets.Reference(sheet, op.Target!);
        int index = sheet.Hyperlinks.Add(cell.Row, cell.Column, 1, 1, address);

        // The cell shows the caller's text, never the rebuilt reference.
        sheet.Hyperlinks[index].TextToDisplay = op.Display ?? op.Url ?? op.Target;
        return null;
    }

    public static long? RemoveHyperlink(Worksheet sheet, RemoveHyperlinkOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        for (int i = 0; i < sheet.Hyperlinks.Count; i++)
        {
            CellArea area = sheet.Hyperlinks[i].Area;
            if (cell.Row >= area.StartRow && cell.Row <= area.EndRow
                && cell.Column >= area.StartColumn && cell.Column <= area.EndColumn)
            {
                sheet.Hyperlinks.RemoveAt(i);
                return null;
            }
        }

        // The executor attaches the op index to this domain error.
        throw new OperationInvalidException($"no hyperlink covers cell '{op.Cell}'");
    }
}
