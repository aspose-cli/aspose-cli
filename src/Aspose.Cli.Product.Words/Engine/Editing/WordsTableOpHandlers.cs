using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Tables;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Owns table and list mutations.</summary>
internal static class WordsTableOpHandlers
{
    internal static long InsertTable(Document document, Node anchor, InsertTableOp op, WordsDocumentLoader loader)
    {
        Style? style = null;
        if (op.Style is not null)
        {
            style = document.Styles[op.Style] ?? throw StyleNotFound(op.Style);
        }

        // One table, then per row a row node and per cell a cell, a paragraph and a run.
        loader.EnsureNodeCapacity(document, 1 + ((long)op.Rows * (1 + (3L * op.Cols))));
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

        if (style is not null)
        {
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
        // The terminal paragraph owns the cell marker and must survive tracked replacement.
        Paragraph paragraph = cell.LastParagraph;
        if (paragraph is null)
        {
            paragraph = new Paragraph(table.Document);
            cell.AppendChild(paragraph);
        }

        Run? firstRun = paragraph.GetChildNodes(NodeType.Run, true).Cast<Run>()
            .FirstOrDefault(static run => !run.IsDeleteRevision);
        var builder = new DocumentBuilder((Document)table.Document);
        builder.MoveTo(firstRun is null ? paragraph : firstRun);
        builder.PushFont();
        foreach (Node child in cell.GetChildNodes(NodeType.Any, false).Cast<Node>().ToArray())
        {
            if (child != paragraph)
            {
                child.Remove();
            }
        }

        paragraph.RemoveAllChildren();
        builder.MoveTo(paragraph);
        builder.PopFont();
        builder.Write(op.Text);
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

