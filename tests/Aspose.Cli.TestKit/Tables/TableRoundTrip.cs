using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Xunit;

namespace Aspose.Cli.TestKit.Tables;

/// <summary>
/// The round trip any object table product runs from its table: for every settable row, a value
/// different from the one read is written through an <c>--ops</c> document, the edit saves and
/// reopens the file, and the value its report reads back must equal the one written; for every
/// read-only row, a read of an object that has it gives it. One test case per row, named by kind
/// and row, so a run counts how many rows pass.
/// </summary>
public static class TableRoundTrip
{
    /// <summary>The cases of a table: each kind's rows, settable or read-only, as (kind, row).</summary>
    public static TheoryData<string, string> Cases(DocumentTable table, bool settable)
    {
        ArgumentNullException.ThrowIfNull(table);
        var cases = new TheoryData<string, string>();
        foreach (ObjectKind kind in table.Kinds)
        {
            foreach (string row in kind.Rows.Where(row => row.Settable == settable).Select(static row => row.Name).Distinct(StringComparer.Ordinal))
            {
                cases.Add(kind.Name, row);
            }
        }

        return cases;
    }

    /// <summary>The address of the first object of <paramref name="kind"/>, in read order, that has <paramref name="row"/>; null when the document has none.</summary>
    public static string? AddressOf(DocumentTable table, object document, string kind, string row)
    {
        ArgumentNullException.ThrowIfNull(table);
        return TableRead.Walk(table, document, table.Root, table.Root.Name)
            .Where(found => found.Kind.Name == kind && found.Kind.RowsOf(found.Item).Any(candidate => candidate.Name == row))
            .Select(static found => found.Address)
            .FirstOrDefault();
    }

    /// <summary>Every row of the object at <paramref name="address"/>, as read gives them.</summary>
    public static JsonObject Read(DocumentTable table, object document, string address)
    {
        ArgumentNullException.ThrowIfNull(table);
        Located located = AddressResolver.Resolve(table, document, TableAddress.Parse(table, address));
        return located.Kind.Read(located.Item);
    }

    /// <summary>The kind <paramref name="name"/> of a table.</summary>
    public static ObjectKind Kind(DocumentTable table, string name) =>
        table.Kinds.Single(kind => kind.Name == name);

    /// <summary>The value kind of a row.</summary>
    public static ValueKind ValueOf(DocumentTable table, string kind, string row) =>
        Kind(table, kind).Rows.First(candidate => candidate.Name == row).Kind;

    /// <summary>
    /// A value of <paramref name="kind"/> that differs from <paramref name="current"/>, written as
    /// an operation document writes it: every kind has one fixed sample and a second for when the
    /// object already holds the first; an enumeration takes its first member that is not the
    /// current one and not <c>NotDefined</c>. A text sample has at most five characters, so the
    /// round trip holds in an engine's evaluation mode, which reads longer text cut short.
    /// </summary>
    public static JsonNode Sample(ValueKind kind, JsonNode? current)
    {
        ArgumentNullException.ThrowIfNull(kind);
        JsonNode[] candidates = kind.Name switch
        {
            "text" => ["Trip", "Other"],
            "integer" => [1500, 2500],
            "short-integer" => [1, 2],
            "number" => [2.5, 3.5],
            "length" => [33.5, 44.5],
            "single" => [12.5, 22.5],
            "boolean" => [current is JsonValue value && value.GetValue<bool>() ? false : true],
            "color" => ["#12AB34", "#345678"],
            "character" => ["*", "+"],
            "chart-value" => [42.5, 43.5],
            _ when kind.Name.StartsWith("enum:", StringComparison.Ordinal) => [.. kind.Schema()["enum"]!.AsArray()
                .Select(static member => member!.GetValue<string>())
                .Where(member => member != "NotDefined" && member != current?.GetValue<string>())
                .Take(1)
                .Select(static member => (JsonNode)member)],
            _ => throw new InvalidOperationException($"The round trip has no sample of the value kind '{kind.Name}'; the product gives the row its own."),
        };
        return candidates.FirstOrDefault(candidate => !JsonNode.DeepEquals(candidate, current))?.DeepClone()
            ?? throw new InvalidOperationException($"The value kind '{kind.Name}' has no sample other than the current value.");
    }
}
