using System.Data;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Tables and lists.
internal sealed partial class WordsMutationHandlers
{
    public ValueTuple Apply(InsertTableOp operation)
    {
        Style? style = operation.TableStyle is null ? null : GetStyle(_document, operation.TableStyle);

        // One table, then per row a row node and per cell a cell, a paragraph and a run.
        _loader.EnsureNodeCapacity(_document, 1 + ((long)operation.RowCount * (1 + (3L * operation.ColumnCount))));
        var table = new Table(_document);
        for (int rowIndex = 0; rowIndex < operation.RowCount; rowIndex++)
        {
            var row = new Row(_document);
            table.AppendChild(row);
            for (int columnIndex = 0; columnIndex < operation.ColumnCount; columnIndex++)
            {
                var cell = new Cell(_document);
                row.AppendChild(cell);
                var paragraph = new Paragraph(_document);
                cell.AppendChild(paragraph);
                string text = operation.Values is not null && rowIndex < operation.Values.Count && columnIndex < operation.Values[rowIndex].Count
                    ? operation.Values[rowIndex][columnIndex]
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
        return default;
    }

    public ValueTuple Apply(SetTableCellOp operation)
    {
        if (Nodes.Count != 1 || Nodes[0] is not Table table)
        {
            throw Invalid("set_table_cell must target one table block");
        }

        if (operation.Row > table.Rows.Count || operation.Column > table.Rows[operation.Row - 1].Cells.Count)
        {
            throw Invalid($"table cell {operation.Row},{operation.Column} is outside the table");
        }

        Cell cell = table.Rows[operation.Row - 1].Cells[operation.Column - 1];
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
        return default;
    }

    public ValueTuple Apply(RepeatTableRowOp operation)
    {
        if (Nodes.Count != 1 || Nodes[0] is not Table table)
        {
            throw Invalid("repeat_table_row must target one table block");
        }

        Row template = TemplateRow(table, operation.Row);
        IReadOnlyList<IReadOnlyDictionary<string, string?>> items = WordsMergeData.Rows(operation.Items, operation.Path, _inputs);
        string[] keys = Placeholder.Matches(WordsText.Of(template))
            .Select(static match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < items.Count; index++)
        {
            string[] missing = keys.Where(key => !items[index].ContainsKey(key)).ToArray();
            if (missing.Length > 0)
            {
                string[] unused = items[index].Keys.Where(key => !keys.Contains(key, StringComparer.Ordinal)).ToArray();
                throw new OperationInvalidException(
                    $"item {index + 1} has no value for {string.Join(", ", missing.Select(key => "{{" + key + "}}" + WordsMergeGaps.Suggestion(key, unused, "its unused key")))} in the template row",
                    "Give every item a value for each placeholder of the template row; an empty string leaves the cell text empty.");
            }
        }

        _loader.EnsureNodeCapacity(_document, items.Count * (1L + template.GetChildNodes(NodeType.Any, true).Count));
        var rows = new Row[items.Count];
        // The copies are filled while detached and untracked, so a tracked batch records each
        // one as a single row insertion rather than placeholder deletions inside it.
        _tracking?.Stop();
        try
        {
            for (int index = 0; index < items.Count; index++)
            {
                rows[index] = (Row)template.Clone(true);
                rows[index].Range.Replace(Placeholder, string.Empty, new FindReplaceOptions
                {
                    IgnoreFieldCodes = true,
                    IgnoreDeleted = true,
                    ReplacingCallback = new PlaceholderValues(items[index]),
                });
            }
        }
        finally
        {
            _tracking?.Start();
        }

        foreach (Row row in rows)
        {
            table.InsertBefore(row, template);
        }

        // A table cannot exist without rows, so removing its only row removes the table.
        if (rows.Length == 0 && table.Rows.Count == 1)
        {
            DocumentBlockIndex.Remove(table);
        }
        else
        {
            template.Remove();
        }

        return default;
    }

    /// <summary>A <c>{{key}}</c> placeholder in one paragraph; spaces may surround the key.</summary>
    private static readonly Regex Placeholder = new(
        @"\{\{[ \t]*([^{}\s](?:[^{}\r\n\a\v\f]*[^{}\s])?)[ \t]*\}\}",
        RegexOptions.CultureInvariant,
        SafeRegex.DefaultTimeout);

    private static Row TemplateRow(Table table, int? number)
    {
        if (number is int row)
        {
            return row <= table.Rows.Count
                ? table.Rows[row - 1]
                : throw Invalid($"row {row} is outside the table, which has {table.Rows.Count} rows");
        }

        int[] rows = Enumerable.Range(1, table.Rows.Count)
            .Where(index => Placeholder.IsMatch(WordsText.Of(table.Rows[index - 1])))
            .ToArray();
        return rows switch
        {
            [int only] => table.Rows[only - 1],
            [] => throw new OperationInvalidException(
                "the table has no row with a {{key}} placeholder",
                "Pass row to name the 1-based template row."),
            _ => throw new OperationInvalidException(
                $"rows {string.Join(", ", rows)} of the table contain {{{{key}}}} placeholders",
                "Pass row to name the 1-based template row."),
        };
    }

    /// <summary>
    /// Replaces each visible placeholder with its item value. The replacement takes the
    /// formatting of the run where the placeholder starts. Range.Replace reads '&amp;' as the start
    /// of a break meta-character (&amp;p, &amp;l, &amp;m, &amp;b) and "&amp;&amp;" as '&amp;',
    /// so doubling every '&amp;' inserts the value literally; substitutions such as $1 are off.
    /// </summary>
    private sealed class PlaceholderValues(IReadOnlyDictionary<string, string?> item) : IReplacingCallback
    {
        public ReplaceAction Replacing(ReplacingArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            if (args.MatchNode.GetAncestor(NodeType.Comment) is not null
                || args.MatchNode.GetAncestor(NodeType.Footnote) is not null
                || !item.TryGetValue(args.Match.Groups[1].Value, out string? value))
            {
                return ReplaceAction.Skip;
            }

            args.Replacement = (value ?? string.Empty).Replace("&", "&&", StringComparison.Ordinal);
            return ReplaceAction.Replace;
        }
    }

    /// <summary>
    /// Sets page-break behaviour through RowFormat and ParagraphFormat. Keeping a table together
    /// follows the Aspose.Words guidance: rows do not break, and every paragraph keeps with the
    /// next except the end-of-cell paragraphs of the last row, which keepWithNext owns.
    /// </summary>
    public ValueTuple Apply(SetTablePaginationOp operation)
    {
        if (Nodes.Count != 1 || Nodes[0] is not Table table)
        {
            throw Invalid("set_table_pagination must target one table block");
        }

        Row[] rows = table.Rows.Cast<Row>().ToArray();
        if (operation.HeaderRowCount > rows.Length)
        {
            throw Invalid($"headerRowCount {operation.HeaderRowCount} is more than the table's {rows.Length} rows");
        }

        for (int index = 0; index < rows.Length; index++)
        {
            Row row = rows[index];
            bool isLast = index == rows.Length - 1;
            if (operation.KeepTogether is bool keepTogether)
            {
                if (keepTogether)
                {
                    row.RowFormat.AllowBreakAcrossPages = false;
                }

                foreach (Paragraph paragraph in row.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>())
                {
                    if (!(isLast && IsCellEnd(row, paragraph)))
                    {
                        paragraph.ParagraphFormat.KeepWithNext = keepTogether;
                    }
                }
            }

            if (operation.AllowRowBreakAcrossPages is bool allowBreak)
            {
                row.RowFormat.AllowBreakAcrossPages = allowBreak;
            }

            if (operation.HeaderRowCount is int headerRowCount)
            {
                row.RowFormat.HeadingFormat = index < headerRowCount;
            }

            if (isLast && operation.KeepWithNext is bool keepWithNext)
            {
                foreach (Cell cell in row.Cells)
                {
                    if (cell.LastParagraph is Paragraph end)
                    {
                        end.ParagraphFormat.KeepWithNext = keepWithNext;
                    }
                }
            }
        }

        return default;

        static bool IsCellEnd(Row row, Paragraph paragraph) => paragraph.ParentNode is Cell cell
            && cell.ParentRow == row
            && cell.LastParagraph == paragraph;
    }

    public ValueTuple Apply(SetListOp operation)
    {
        Paragraph[] paragraphs = [.. Nodes.SelectMany(Paragraphs)];
        ListTemplate template = operation.Kind == "bullet" ? ListTemplate.BulletDefault : ListTemplate.NumberDefault;
        int level = operation.Level - 1;
        ListNumbering numbering = TemplateLevels.Value[template][level];
        numbering = numbering with
        {
            Style = operation.NumberStyle is { } name ? WordsReadBack.NumberStyleOf(name) : numbering.Style,
            Start = operation.Start ?? numbering.Start,
        };
        if (IsAlreadyTheList(paragraphs, level, numbering))
        {
            return default;
        }

        List list = _document.Lists.Add(template);
        list.ListLevels[level].NumberStyle = numbering.Style;
        list.ListLevels[level].StartAt = numbering.Start;
        foreach (Paragraph paragraph in paragraphs)
        {
            paragraph.ListFormat.List = list;
            paragraph.ListFormat.ListLevelNumber = level;
        }

        return default;
    }

    public ValueTuple Apply(ClearListOp operation)
    {
        foreach (Paragraph paragraph in Nodes.SelectMany(Paragraphs))
        {
            paragraph.ListFormat.RemoveNumbers();
        }

        return default;
    }

    /// <summary>
    /// Whether the paragraphs already are the list set_list would make of them: all at the level
    /// of one list that no other paragraph uses, which numbers that level as
    /// <paramref name="numbering"/> does. Making them a new list again would change nothing a
    /// reader sees.
    /// </summary>
    private bool IsAlreadyTheList(Paragraph[] paragraphs, int level, ListNumbering numbering)
    {
        if (paragraphs.Length == 0 || !paragraphs[0].ListFormat.IsListItem)
        {
            return false;
        }

        int listId = paragraphs[0].ListFormat.List.ListId;
        if (paragraphs.Any(paragraph => !paragraph.ListFormat.IsListItem
            || paragraph.ListFormat.List.ListId != listId
            || paragraph.ListFormat.ListLevelNumber != level))
        {
            return false;
        }

        var targets = new HashSet<Paragraph>(paragraphs, ReferenceEqualityComparer.Instance);
        if (_document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>()
            .Any(paragraph => paragraph.ListFormat.IsListItem && paragraph.ListFormat.List.ListId == listId && !targets.Contains(paragraph)))
        {
            return false;
        }

        ListLevel current = paragraphs[0].ListFormat.List.ListLevels[level];
        return numbering == new ListNumbering(current.NumberStyle, current.NumberFormat, current.StartAt);
    }

    /// <summary>How a list level numbers its items: its number style, its number format and its first number.</summary>
    private readonly record struct ListNumbering(NumberStyle Style, string Format, int Start);

    /// <summary>How a new list from each template set_list uses numbers each of its levels.</summary>
    private static readonly Lazy<Dictionary<ListTemplate, ListNumbering[]>> TemplateLevels = new(static () =>
    {
        var scratch = new Document();
        return new[] { ListTemplate.BulletDefault, ListTemplate.NumberDefault }.ToDictionary(
            static template => template,
            template => scratch.Lists.Add(template).ListLevels.Select(static level => new ListNumbering(level.NumberStyle, level.NumberFormat, level.StartAt)).ToArray());
    });
}

