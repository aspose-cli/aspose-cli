using System.Globalization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Words;
using Aspose.Words.Fields;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// Finds the template merge fields a mail merge has no value for: a record whose value
/// for the field is null or that has no such key. An empty string is a value. Field names match
/// the data case-insensitively, as the SDK's mail merge does.
/// </summary>
internal static class WordsMergeGaps
{
    // The most record numbers listed for one field; the rest are counted.
    private const int RecordLimit = 10;

    /// <summary>
    /// The data field names the template's merge fields use, in document order: every merge
    /// field's, or with <paramref name="region"/> only those between its TableStart and
    /// TableEnd fields. The SDK names the data field, without a prefix such as Image:.
    /// </summary>
    internal static IReadOnlyList<string> TemplateFields(Document document, string? region)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inside = region is null;
        foreach (FieldMergeField field in document.Range.Fields.Cast<Field>().OfType<FieldMergeField>())
        {
            string name = field.FieldName ?? string.Empty;
            bool start = name.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase);
            if (start || name.StartsWith("TableEnd:", StringComparison.OrdinalIgnoreCase))
            {
                if (region is not null && string.Equals(field.FieldNameNoPrefix, region, StringComparison.OrdinalIgnoreCase))
                {
                    inside = start;
                }

                continue;
            }

            string data = field.FieldNameNoPrefix ?? string.Empty;
            if (inside && data.Length > 0 && seen.Add(data))
            {
                names.Add(data);
            }
        }

        return names;
    }

    /// <summary>
    /// A <c>MERGE_VALUE_MISSING</c> warning listing each template field with the 1-based
    /// records that give it no value, which are the merged copies or region repetitions in
    /// the same order, or null when every record has a value for every field.
    /// </summary>
    internal static Warning? Find(
        IReadOnlyList<string> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        bool regions)
    {
        var gaps = new List<string>();
        foreach (string field in fields)
        {
            int[] records = Enumerable.Range(0, rows.Count)
                .Where(index => Value(rows[index], field) is null)
                .Select(static index => index + 1)
                .ToArray();
            if (records.Length > 0)
            {
                gaps.Add($"{field}: {Records(records)}");
            }
        }

        return gaps.Count == 0 ? null : new Warning
        {
            Code = WordsDiagnostics.MergeValueMissing,
            Message = string.Create(
                CultureInfo.InvariantCulture,
                $"mail_merge had no value for {gaps.Count} template merge field(s) in some records: {string.Join("; ", gaps)}."),
            Hint = (regions
                ? "In the region, a null value, or a key that only some records lack, merges as blank text; a key no record has leaves the field as its «Name» placeholder. "
                : "A null value merges as blank text, and a key a record lacks leaves the field as its «Name» placeholder in that record's copy. ")
                + "Supply the missing values in the merge data, or confirm that the result is acceptable.",
        };
    }

    private static string? Value(IReadOnlyDictionary<string, string?> row, string field) =>
        row.TryGetValue(field, out string? value)
            ? value
            : row.FirstOrDefault(pair => string.Equals(pair.Key, field, StringComparison.OrdinalIgnoreCase)).Value;

    private static string Records(int[] records)
    {
        string listed = string.Join(", ", records.Take(RecordLimit));
        string more = records.Length > RecordLimit
            ? string.Create(CultureInfo.InvariantCulture, $" and {records.Length - RecordLimit} more")
            : string.Empty;
        return (records.Length == 1 ? "record " : "records ") + listed + more;
    }
}
