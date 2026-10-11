using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The part of a published schema one schema-rule case judges: a whole result schema, or one
/// definition (or the root) of a product's ops schema, so that renaming one operation's fields
/// clears the cases of that operation alone.
/// </summary>
internal sealed record SchemaSubject(string SchemaId, string? Definition, bool OpsRoot = false)
{
    /// <summary>The case subject, such as <c>v2/cells/ops#set_validation</c>.</summary>
    public string Name => Definition is null ? SchemaId : $"{SchemaId}#{Definition}";

    /// <summary>Every schema node of the subject, with its display path; another subject's definitions are left out.</summary>
    public IEnumerable<SchemaNode> Nodes() =>
        Definition is not null
            ? PublishedSchema.Walk(PublishedSchema.Definition(SchemaId, Definition), string.Empty, skipDefinitions: true)
            : PublishedSchema.Walk(PublishedSchema.Document(SchemaId), string.Empty, skipDefinitions: OpsRoot);

    /// <summary>Every property the subject declares, wherever it nests.</summary>
    public IEnumerable<SchemaProperty> Properties() =>
        Nodes().SelectMany(static node => PublishedSchema.PropertiesOf(node));
}

/// <summary>One schema object inside a published schema; <see cref="Path"/> reads like <c>validationInfo.type.anyOf[0]</c>.</summary>
internal sealed record SchemaNode(string Path, JsonObject Node);

/// <summary>One declared property: its owner's path, its name and its schema.</summary>
internal sealed record SchemaProperty(string OwnerPath, string Name, JsonObject Schema, JsonObject Owner)
{
    public string Path => PublishedSchema.Join(OwnerPath, Name);
}

/// <summary>The published schemas as the schema rules read them: by id from capabilities, parsed once.</summary>
internal static class PublishedSchema
{
    private static readonly ConcurrentDictionary<string, JsonObject> Documents = new(StringComparer.Ordinal);

    private static readonly string[] SchemaChildren = ["items", "additionalProperties", "not", "if", "then", "else", "contains", "propertyNames"];
    private static readonly string[] SchemaArrays = ["anyOf", "oneOf", "allOf", "prefixItems"];

    /// <summary>Every schema id capabilities lists, in its order.</summary>
    public static IReadOnlyList<string> Ids(CliCatalog catalog) =>
        [.. catalog.Document["schemas"]!.AsArray().Select(static id => id!.GetValue<string>())];

    /// <summary>Whether <paramref name="id"/> is a product's operation (input) schema.</summary>
    public static bool IsOps(string id) => id.EndsWith("/ops", StringComparison.Ordinal);

    /// <summary>The ids of the result schemas.</summary>
    public static IEnumerable<string> ResultIds(CliCatalog catalog) => Ids(catalog).Where(static id => !IsOps(id));

    /// <summary>The subjects of every published schema: each result schema whole, each ops schema by root and definition.</summary>
    public static IEnumerable<SchemaSubject> Subjects(CliCatalog catalog)
    {
        foreach (string id in Ids(catalog))
        {
            if (!IsOps(id))
            {
                yield return new SchemaSubject(id, null);
                continue;
            }
            yield return new SchemaSubject(id, null, OpsRoot: true);
            foreach (string definition in Definitions(id))
            {
                yield return new SchemaSubject(id, definition);
            }
        }
    }

    /// <summary>The ops schema subject of one operation.</summary>
    public static SchemaSubject OpSubject(CliProduct product, string op) => new(product.OpsSchemaId!, op);

    /// <summary>The products whose edit command takes an ops document.</summary>
    public static IEnumerable<CliProduct> OpsProducts(CliCatalog catalog) =>
        catalog.Products.Where(static product => product.OpsSchemaId is not null);

    /// <summary>The parsed schema document; callers must not change it.</summary>
    public static JsonObject Document(string id) => Documents.GetOrAdd(id, PublishedSchemas.Document);

    public static IEnumerable<string> Definitions(string id) =>
        Document(id)["$defs"] is JsonObject definitions ? definitions.Select(static pair => pair.Key) : [];

    public static JsonObject Definition(string id, string name) =>
        Document(id)["$defs"]?[name] as JsonObject
            ?? throw new InvalidOperationException($"Schema '{id}' has no definition '{name}'.");

    /// <summary>Joins a display path and a property name.</summary>
    public static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>Every schema object under <paramref name="node"/>, itself first; values such as defaults and examples are not walked.</summary>
    public static IEnumerable<SchemaNode> Walk(JsonObject node, string path, bool skipDefinitions)
    {
        yield return new SchemaNode(path, node);
        foreach ((string key, JsonNode? value) in node)
        {
            switch (key)
            {
                case "properties" when value is JsonObject properties:
                    foreach ((string name, JsonNode? schema) in properties)
                    {
                        if (schema is JsonObject child)
                        {
                            foreach (SchemaNode nested in Walk(child, Join(path, name), true))
                            {
                                yield return nested;
                            }
                        }
                    }
                    break;
                case "$defs" when !skipDefinitions && value is JsonObject definitions:
                    foreach ((string name, JsonNode? schema) in definitions)
                    {
                        if (schema is JsonObject child)
                        {
                            foreach (SchemaNode nested in Walk(child, name, true))
                            {
                                yield return nested;
                            }
                        }
                    }
                    break;
                default:
                    if (SchemaChildren.Contains(key) && value is JsonObject single)
                    {
                        string suffix = key == "items" ? "[]" : key == "additionalProperties" ? ".*" : "." + key;
                        foreach (SchemaNode nested in Walk(single, path + suffix, true))
                        {
                            yield return nested;
                        }
                    }
                    else if (SchemaArrays.Contains(key) && value is JsonArray array)
                    {
                        for (int index = 0; index < array.Count; index++)
                        {
                            if (array[index] is JsonObject child)
                            {
                                foreach (SchemaNode nested in Walk(child, $"{path}.{key}[{index}]", true))
                                {
                                    yield return nested;
                                }
                            }
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>The properties a schema object declares directly.</summary>
    public static IEnumerable<SchemaProperty> PropertiesOf(SchemaNode node) =>
        node.Node["properties"] is JsonObject properties
            ? properties.Where(static pair => pair.Value is JsonObject)
                .Select(pair => new SchemaProperty(node.Path, pair.Key, (JsonObject)pair.Value!, node.Node))
            : [];

    /// <summary>
    /// The schema a <c>$ref</c> names, followed until a node without one, in the schema that holds it;
    /// sibling keywords of a reference (such as an extra <c>required</c>) are not merged.
    /// </summary>
    public static (string SchemaId, JsonObject Node) Resolve(string schemaId, JsonObject node)
    {
        for (int depth = 0; depth < 16 && node["$ref"]?.GetValue<string>() is { } reference; depth++)
        {
            string target = reference;
            string fragment = string.Empty;
            int hash = reference.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                target = reference[..hash];
                fragment = reference[(hash + 1)..];
            }
            if (target.Length > 0)
            {
                schemaId = PublishedSchemas.IdOf(target);
            }
            JsonNode current = Document(schemaId);
            foreach (string segment in fragment.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current[segment] ?? throw new InvalidOperationException($"'{reference}' does not resolve in '{schemaId}'.");
            }
            node = (JsonObject)current;
        }
        return (schemaId, node);
    }

    /// <summary>The JSON types a schema object allows, null left out.</summary>
    public static string[] Types(JsonObject node) => node["type"] switch
    {
        JsonValue value => value.GetValue<string>() == "null" ? [] : [value.GetValue<string>()],
        JsonArray array => [.. array.Select(static type => type!.GetValue<string>()).Where(static type => type != "null")],
        _ => [],
    };

    /// <summary>The string values of a schema object's <c>enum</c>, or null when it has none.</summary>
    public static string[]? Enum(JsonObject node) =>
        node["enum"] is JsonArray values
            ? [.. values.Where(static value => value is JsonValue item && item.TryGetValue(out string? _)).Select(static value => value!.GetValue<string>())]
            : null;

    /// <summary>The names in a schema object's <c>required</c>.</summary>
    public static string[] Required(JsonObject node) =>
        node["required"] is JsonArray names ? [.. names.Select(static name => name!.GetValue<string>())] : [];

    /// <summary>
    /// A comparable description of a value's shape: its types, enum values and, for an object, its
    /// property names, so that two definitions with the same shape compare equal across schemas.
    /// </summary>
    public static string Shape(string schemaId, JsonObject node, bool values = true, int depth = 0)
    {
        (schemaId, node) = Resolve(schemaId, node);
        foreach (string combinator in new[] { "oneOf", "anyOf" })
        {
            if (node[combinator] is JsonArray alternatives)
            {
                string[] shapes = [.. alternatives.OfType<JsonObject>().Select(item => Shape(schemaId, item, values, depth + 1)).Where(static shape => shape.Length > 0).Distinct().Order(StringComparer.Ordinal)];
                if (shapes.Length > 0)
                {
                    return string.Join(" | ", shapes);
                }
            }
        }
        if (node["const"] is JsonValue constant)
        {
            return values ? $"const {constant.ToJsonString()}" : "enum";
        }
        if (Enum(node) is { } enumeration)
        {
            return values ? $"enum({string.Join(",", enumeration.Order(StringComparer.Ordinal))})" : "enum";
        }
        string[] types = Types(node);
        if (types.Contains("array"))
        {
            return node["items"] is JsonObject items ? $"array of {Shape(schemaId, items, values, depth + 1)}" : "array";
        }
        if (types.Contains("object") || node["properties"] is not null)
        {
            if (depth > 2 || node["properties"] is not JsonObject properties)
            {
                return "object";
            }
            return "object{" + string.Join(",", properties.Select(static pair => pair.Key).Order(StringComparer.Ordinal)) + "}";
        }
        return string.Join("/", types.Order(StringComparer.Ordinal));
    }
}
