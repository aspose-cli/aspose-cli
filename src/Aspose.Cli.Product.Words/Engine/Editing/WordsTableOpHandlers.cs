using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Owns table and list mutations.</summary>
internal static class WordsTableOpHandlers
{
    internal static long InsertTable(Document document, Node anchor, InsertTableOp op)
    {
        var table = new Table(document);
        for (int rowIndex = 0; rowIndex < op.Rows; rowIndex++)
        {
            var row = new Row(document);
            table.AppendChild(row);
            for (int columnIndex = 0; columnIndex < op.Cols; columnIndex++)
            {
                var cell = new Cell(document);
                row.AppendChild(cell);
                var paragraph = new Paragraph(document);
                cell.AppendChild(paragraph);
                string text = op.Data is not null && rowIndex < op.Data.Count && columnIndex < op.Data[rowIndex].Count
                    ? op.Data[rowIndex][columnIndex]
                    : string.Empty;
                paragraph.AppendChild(new Run(document, text));
            }
        }

        if (op.Style is not null)
        {
            Style? style = document.Styles[op.Style];
            if (style is null)
            {
                throw StyleNotFound(op.Style);
            }

            table.Style = style;
        }

        Node cursor = anchor;
        InsertRelative(anchor, ref cursor, table, op.Position);
        return (long)op.Rows * op.Cols;
    }

    internal static long SetTableCell(IReadOnlyList<Node> nodes, SetTableCellOp op)
    {
        if (nodes.Count != 1 || nodes[0] is not Table table)
        {
            throw Invalid("set_table_cell must target one table block");
        }

        if (op.Row > table.Rows.Count || op.Col > table.Rows[op.Row - 1].Cells.Count)
        {
            throw Invalid($"table cell {op.Row},{op.Col} is outside the table");
        }

        Cell cell = table.Rows[op.Row - 1].Cells[op.Col - 1];
        cell.RemoveAllChildren();
        var paragraph = new Paragraph(table.Document);
        paragraph.AppendChild(new Run(table.Document, op.Text));
        cell.AppendChild(paragraph);
        return 1;
    }

    internal static long ApplyList(Document document, IReadOnlyList<Node> nodes, ApplyListOp op)
    {
        List list = document.Lists.Add(op.Kind == "bullet" ? ListTemplate.BulletDefault : ListTemplate.NumberDefault);
        long count = 0;
        foreach (Paragraph paragraph in nodes.SelectMany(Paragraphs))
        {
            paragraph.ListFormat.List = list;
            paragraph.ListFormat.ListLevelNumber = op.Level;
            count++;
        }

        return count;
    }
}

