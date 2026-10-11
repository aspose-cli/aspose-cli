using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// C-R21 (D9): a state an operation writes reads back under the same name and value domain. Which
// op fields are state is a human decision the schemas cannot make, so each op record declares it
// (ReadBackAttribute and its siblings in the SDK) and the ops schema publishes it as the field's
// x-readBack: a read path (a read result's schema URI and a JSON Pointer to the property that
// reports the field, a dictionary's additionalProperties being the entry named after the field),
// "unread" for state no read reports, "position" for state read back by
// position, or "not-state" for an address, a source, a selector or a parameter of the action. A
// field without one is classified field by field in its record; the generator refuses an op field
// that is classified neither way, so a field the schema leaves unclassified is reported.
internal static partial class ContractRules
{
    /// <summary>
    /// The state fields of an op with a read path, by <c>&lt;field&gt;[.&lt;field&gt;]</c>, each
    /// with its read path <c>&lt;schema URI&gt;#&lt;JSON Pointer&gt;</c>.
    /// </summary>
    private static IEnumerable<(string Field, string Path)> ReadPaths(CliProduct product, string op) =>
        Declarations(product, op).Where(static item => item.ReadBack?.Contains('#', StringComparison.Ordinal) == true)
            .Select(static item => (item.Field, item.ReadBack!));

    /// <summary>
    /// Every field of an op with its published <c>x-readBack</c>, descending into a field that has
    /// none when its record classifies its own fields; null for a field classified neither way.
    /// </summary>
    private static IEnumerable<(string Field, string? ReadBack, string SchemaId, JsonObject Schema)> Declarations(CliProduct product, string op)
    {
        string id = product.OpsSchemaId!;
        var fields = new List<(string, string?, string, JsonObject)>();
        Classify(PublishedSchema.Definition(id, op), id, string.Empty);
        return fields;

        void Classify(JsonObject owner, string schemaId, string fieldPrefix)
        {
            if (owner["properties"] is not JsonObject properties)
            {
                return;
            }
            foreach ((string name, JsonNode? value) in properties)
            {
                if ((fieldPrefix.Length == 0 && name is "id" or "op") || value is not JsonObject schema)
                {
                    continue;
                }
                string field = fieldPrefix + name;
                if (schema["x-readBack"]?.GetValue<string>() is { } readBack)
                {
                    fields.Add((field, readBack, schemaId, schema));
                    continue;
                }
                (string nestedId, JsonObject nested) = PublishedSchema.Resolve(schemaId, schema);
                if (nested["items"] is JsonObject items)
                {
                    (nestedId, nested) = PublishedSchema.Resolve(nestedId, items);
                }
                if (nested["properties"] is JsonObject inner && inner.Any(static pair => pair.Value?["x-readBack"] is not null))
                {
                    Classify(nested, nestedId, field + ".");
                    continue;
                }
                fields.Add((field, null, schemaId, schema));
            }
        }
    }

    private static IEnumerable<InvariantCase> R21Declared(CliCatalog catalog)
    {
        CliProduct[] products = [.. PublishedSchema.OpsProducts(catalog)];
        foreach (CliProduct product in products)
        {
            foreach (string op in product.Ops)
            {
                string subject = PublishedSchema.OpSubject(product, op).Name;
                yield return Check("C-R21", subject, () => ReadBack(product, op).Problems);
                yield return Check("C-R21", $"{subject} unclassified", () =>
                    ReadBack(product, op).Unclassified is { Count: > 0 } fields ? [$"unclassified: {string.Join(", ", fields)}"] : []);
            }
        }
    }

    /// <summary>The read-back problems of one op's classified state fields, and the fields nobody has classified.</summary>
    private static (List<string> Problems, List<string> Unclassified) ReadBack(CliProduct product, string op)
    {
        var unclassified = new List<string>();
        var problems = new List<string>();
        foreach ((string field, string? readBack, string schemaId, JsonObject schema) in Declarations(product, op))
        {
            switch (readBack)
            {
                case null:
                    unclassified.Add(field);
                    break;
                case "not-state" or "position":
                    break;
                case "unread":
                    problems.Add($"{field}: no read reports it");
                    break;
                default:
                    problems.AddRange(StateField(field, schemaId, schema, readBack));
                    break;
            }
        }
        return (problems, unclassified);
    }

    /// <summary>The ways one state field fails to read back: no read path, a missing path, another name or another value domain.</summary>
    private static IEnumerable<string> StateField(string field, string schemaId, JsonObject schema, string path)
    {
        if (ReadField(path) is not { } read)
        {
            yield return $"{field}: read path {path} does not exist";
            yield break;
        }
        string name = field[(field.LastIndexOf('.') + 1)..];
        string readName = ReadTarget(field, path).Name;
        if (readName != name)
        {
            yield return $"{field}: read back as {readName}";
        }
        string written = PublishedSchema.Shape(schemaId, schema);
        string reported = PublishedSchema.Shape(read.SchemaId, read.Node);
        if (written != reported)
        {
            yield return $"{field}: writes {written}, reads {reported}";
        }
    }

    /// <summary>
    /// The id of a read path's schema, such as <c>v2/pdf/pdf-info</c>, and the name the read
    /// reports the field under: the pointer's last property, or the field's own name for a
    /// dictionary's entry.
    /// </summary>
    private static (string SchemaId, string Name) ReadTarget(string field, string path)
    {
        int hash = path.IndexOf('#', StringComparison.Ordinal);
        string last = path[(path.LastIndexOf('/') + 1)..];
        return (PublishedSchemas.IdOf(path[..hash]), last == "additionalProperties" ? field[(field.LastIndexOf('.') + 1)..] : last);
    }

    /// <summary>
    /// The schema a read path points to, resolved as a <c>$ref</c>, or null when the read schema
    /// has no such property.
    /// </summary>
    private static (string SchemaId, JsonObject Node)? ReadField(string path)
    {
        if (path.IndexOf('#', StringComparison.Ordinal) is var hash && (hash < 0 || !path[(hash + 1)..].StartsWith('/')))
        {
            return null;
        }

        try
        {
            return PublishedSchema.Resolve(string.Empty, new JsonObject { ["$ref"] = path });
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException or InvalidCastException)
        {
            return null;
        }
    }
}
