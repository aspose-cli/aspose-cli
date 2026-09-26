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

// Tables and lists.
internal sealed partial class WordsMutationHandlers
{
    public long Apply(InsertTableOp operation)
    {
        Style? style = operation.Style is null ? null : GetStyle(_document, operation.Style);

        // One table, then per row a row node and per cell a cell, a paragraph and a run.
        _loader.EnsureNodeCapacity(_document, 1 + ((long)operation.Rows * (1 + (3L * operation.Cols))));
        var table = new Table(_document);
        for (int rowIndex = 0; rowIndex < operation.Rows; rowIndex++)
        {
            var row = new Row(_document);
            table.AppendChild(row);
            for (int columnIndex = 0; columnIndex < operation.Cols; columnIndex++)
            {
                var cell = new Cell(_document);
                row.AppendChild(cell);
                var paragraph = new Paragraph(_document);
                cell.AppendChild(paragraph);
                string text = operation.Data is not null && rowIndex < operation.Data.Count && columnIndex < operation.Data[rowIndex].Count
                    ? operation.Data[rowIndex][columnIndex]
                    : string.Empty;
                paragraph.AppendChild(new Run(_document, text));
            }
        }

        if (style is not null)
        {
            table.Style = style;
        }

        Node cursor = Anchor;
        InsertRelative(Anchor, ref cursor, table, operation.Position);
        return (long)operation.Rows * operation.Cols;
    }

    public long Apply(SetTableCellOp operation)
    {
        if (Nodes.Count != 1 || Nodes[0] is not Table table)
        {
            throw Invalid("set_table_cell must target one table block");
        }

        if (operation.Row > table.Rows.Count || operation.Col > table.Rows[operation.Row - 1].Cells.Count)
        {
            throw Invalid($"table cell {operation.Row},{operation.Col} is outside the table");
        }

        Cell cell = table.Rows[operation.Row - 1].Cells[operation.Col - 1];
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
        builder.Write(operation.Text);
        return 1;
    }

    public long Apply(ApplyListOp operation)
    {
        List list = _document.Lists.Add(operation.Kind == "bullet" ? ListTemplate.BulletDefault : ListTemplate.NumberDefault);
        long count = 0;
        foreach (Paragraph paragraph in Nodes.SelectMany(Paragraphs))
        {
            paragraph.ListFormat.List = list;
            paragraph.ListFormat.ListLevelNumber = operation.Level;
            count++;
        }

        return count;
    }
}

