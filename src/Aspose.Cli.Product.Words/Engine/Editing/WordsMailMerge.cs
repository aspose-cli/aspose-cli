using System.Data;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.MailMerging;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// mail_merge: one copy of the document per row, or the rows repeated through the template's
/// one merge region. The node budget is charged before any copy is made.
/// </summary>
internal static class WordsMailMerge
{
    private const string TableStart = "TableStart:";

    /// <summary>Merges the rows into the document; returns the disclosure of the template fields a row leaves blank.</summary>
    internal static Warning? Merge(
        Document document,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        bool regions,
        WordsDocumentLoader loader,
        OutputPipeline<Document> outputs)
    {
        if (rows.Count == 0)
        {
            throw WordsMergeData.Invalid(
                "mail_merge needs at least one row",
                "Supply a JSON array with at least one object, or a CSV file with a data row after its header.");
        }

        return regions
            ? MergeRegion(document, rows, loader)
            : MergeCopies(document, rows, loader, outputs);
    }

    private static Warning? MergeRegion(
        Document document,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        WordsDocumentLoader loader)
    {
        string region = SingleRegion(document);
        loader.EnsureNodeCapacity(document, rows.Count * RegionNodeCount(document, region));
        Warning? gaps = WordsMergeGaps.Find(WordsMergeGaps.TemplateFields(document, region), rows);
        ExecuteRegionMerge(document, region, rows);
        return gaps;
    }

    private static Warning? MergeCopies(
        Document document,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        WordsDocumentLoader loader,
        OutputPipeline<Document> outputs)
    {
        // Every further row appends one copy of the whole template.
        loader.EnsureNodeCapacity(document, (rows.Count - 1L) * document.GetChildNodes(NodeType.Any, true).Count);
        Warning? gaps = WordsMergeGaps.Find(WordsMergeGaps.TemplateFields(document, region: null), rows);
        Document template = document.Clone();
        ExecuteMergeRow(document, rows[0]);
        for (int index = 1; index < rows.Count; index++)
        {
            Document letter = template.Clone();
            ExecuteMergeRow(letter, rows[index]);
            // Under evaluation a letter starts with a banner: the template's own, or the one
            // the evaluation merge writes into it.
            outputs.Neutralize(letter);

            document.AppendDocument(letter, ImportFormatMode.KeepSourceFormatting);
        }

        return gaps;
    }

    // A template field the row has no key for merges as blank text, as a null value does.
    private static void ExecuteMergeRow(Document document, IReadOnlyDictionary<string, string?> row)
    {
        document.MailMerge.CleanupOptions = MailMergeCleanupOptions.RemoveUnusedFields;
        document.MailMerge.Execute(row.Keys.ToArray(), row.Values.Cast<object?>().ToArray());
    }

    /// <summary>The template's merge regions cannot take the flat merge rows.</summary>
    private static CliException RegionInvalid(string reason, string hint) => new(
        WordsDiagnostics.MergeDataInvalid,
        $"The mail merge template cannot be merged with regions: {reason}.",
        hint: hint);

    /// <summary>
    /// The one merge region the flat rows feed. Several regions would need hierarchical data,
    /// so a template with more than one region name is rejected rather than half merged.
    /// </summary>
    private static string SingleRegion(Document document)
    {
        string[] regions = MergeFields(document)
            .Select(static field => field.FieldName)
            .Where(static name => name.StartsWith(TableStart, StringComparison.OrdinalIgnoreCase))
            .Select(static name => name[TableStart.Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return regions switch
        {
            [] => throw RegionInvalid(
                "it has no TableStart merge field",
                "Put TableStart:Name and TableEnd:Name merge fields around the content to repeat, or set regions to false to merge one copy of the document per row."),
            [var region] when !string.IsNullOrWhiteSpace(region) => region,
            [_] => throw RegionInvalid(
                "its TableStart merge field has no region name",
                "Name the region in both fields, such as TableStart:Items and TableEnd:Items."),
            _ => throw RegionInvalid(
                $"it has {regions.Length} merge regions ({string.Join(", ", regions)}) but flat merge rows feed exactly one",
                "Split the template so each copy has one region, and merge each copy with its own rows."),
        };
    }

    /// <summary>The nodes one region repetition adds: its table rows, or its body blocks.</summary>
    private static long RegionNodeCount(Document document, string region)
    {
        FieldMergeField[] fields = MergeFields(document).ToArray();
        FieldMergeField start = fields.First(field =>
            string.Equals(field.FieldName, TableStart + region, StringComparison.OrdinalIgnoreCase));
        FieldMergeField end = fields.FirstOrDefault(field =>
                string.Equals(field.FieldName, "TableEnd:" + region, StringComparison.OrdinalIgnoreCase))
            ?? throw RegionInvalid(
                $"region '{region}' has no TableEnd:{region} merge field",
                $"Add a TableEnd:{region} merge field after the content the region repeats.");
        if (start.Start.GetAncestor(NodeType.Row) is Row first
            && end.End.GetAncestor(NodeType.Row) is Row last
            && ReferenceEquals(first.ParentTable, last.ParentTable))
        {
            return Span(first, last);
        }

        return Span(start.Start.GetAncestor(NodeType.Paragraph), end.End.GetAncestor(NodeType.Paragraph));

        static long Span(Node first, Node last)
        {
            long count = 0;
            for (Node? node = first; node is not null; node = node.NextSibling)
            {
                count += 1 + (node is CompositeNode composite ? composite.GetChildNodes(NodeType.Any, true).Count : 0);
                if (ReferenceEquals(node, last))
                {
                    break;
                }
            }

            return count;
        }
    }

    private static IEnumerable<FieldMergeField> MergeFields(Document document) =>
        document.Range.Fields.Cast<Field>().OfType<FieldMergeField>();

    private static void ExecuteRegionMerge(
        Document document,
        string regionName,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        string[] columns = rows.SelectMany(static row => row.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var table = new DataTable(regionName);
        foreach (string column in columns)
        {
            table.Columns.Add(column, typeof(string));
        }

        foreach (IReadOnlyDictionary<string, string?> row in rows)
        {
            DataRow data = table.NewRow();
            foreach (string column in columns)
            {
                data[column] = row.TryGetValue(column, out string? value)
                    ? value ?? (object)DBNull.Value
                    : DBNull.Value;
            }

            table.Rows.Add(data);
        }

        // A region field no record has a key for merges as blank text; fields outside the region stay.
        document.MailMerge.CleanupOptions = MailMergeCleanupOptions.RemoveUnusedFields;
        document.MailMerge.ExecuteWithRegions(table);
    }
}
