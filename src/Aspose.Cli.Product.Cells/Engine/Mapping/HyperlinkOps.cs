using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Cell hyperlinks — external URLs and internal cross-sheet references.</summary>
internal static class HyperlinkOps
{
    public static long? SetHyperlink(Worksheet sheet, SetHyperlinkOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        string address = op.Url ?? op.Target!; // the parser guarantees exactly one is set
        int index = sheet.Hyperlinks.Add(cell.Row, cell.Column, 1, 1, address);

        Hyperlink hyperlink = sheet.Hyperlinks[index];
        if (op.Display is { } display)
        {
            hyperlink.TextToDisplay = display;
        }

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
        throw CellsErrors.OpsInvalid($"no hyperlink covers cell '{op.Cell}'");
    }
}
