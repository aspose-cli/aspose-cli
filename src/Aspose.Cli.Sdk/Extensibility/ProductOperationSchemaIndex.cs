using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Builds exact, self-contained operation views from one ops schema.</summary>
internal static class ProductOperationSchemaIndex
{
    public static IReadOnlyDictionary<string, string> Build(
        string schemaId,
        string document,
        IReadOnlyCollection<string> expectedOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        ArgumentNullException.ThrowIfNull(expectedOperations);

        JsonObject root;
        try
        {
            root = JsonNode.Parse(document) as JsonObject
                ?? throw new InvalidOperationException("the root is not an object");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Operation schema '{schemaId}' is not valid JSON.",
                exception);
        }

        JsonObject items = root["properties"]?["ops"]?["items"] as JsonObject
            ?? throw Invalid(schemaId, "properties.ops.items is missing");
        (JsonArray alternatives, string? definitionPointer, bool conditional) =
            Alternatives(root, items, schemaId);
        var branches = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (JsonNode? candidate in alternatives)
        {
            JsonNode branch = candidate
                ?? throw Invalid(schemaId, "an operation branch is null");
            JsonObject branchObject = branch as JsonObject
                ?? throw Invalid(schemaId, "an operation branch is not an object");
            JsonObject resolved = Resolve(root, branch, schemaId);
            string? operation = ReadOperation(branchObject) ?? ReadOperation(resolved);
            if (string.IsNullOrWhiteSpace(operation))
            {
                throw Invalid(
                    schemaId,
                    "each operation branch must declare properties.op.const directly or through its local $ref");
            }
            if (!branches.TryAdd(operation, branch))
            {
                throw Invalid(schemaId, $"operation '{operation}' has multiple schema branches");
            }
        }

        string[] expected = expectedOperations
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actual = branches.Keys.Order(StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            string[] missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
            string[] orphaned = actual.Except(expected, StringComparer.Ordinal).ToArray();
            throw Invalid(
                schemaId,
                $"operation manifest/schema drift; missing=[{string.Join(", ", missing)}], "
                + $"orphaned=[{string.Join(", ", orphaned)}]");
        }

        var indexed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string operation, JsonNode branch) in branches)
        {
            JsonObject narrowed = (JsonObject)root.DeepClone();
            JsonObject narrowedItems = (JsonObject)narrowed["properties"]!["ops"]!["items"]!;
            JsonArray selected = [branch.DeepClone()];
            if (definitionPointer is null)
            {
                narrowedItems["oneOf"] = selected;
            }
            else
            {
                JsonObject definition = ResolvePointer(narrowed, definitionPointer, schemaId);
                if (conditional)
                {
                    definition["allOf"] = selected;
                    definition["properties"]!["op"]!["enum"] =
                        new JsonArray(operation);
                }
                else
                {
                    definition["oneOf"] = selected;
                }
            }

            indexed.Add(operation, narrowed.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }

        return indexed;
    }

    private static (JsonArray Alternatives, string? DefinitionPointer, bool Conditional) Alternatives(
        JsonObject root,
        JsonObject items,
        string schemaId)
    {
        if (items["oneOf"] is JsonArray direct)
        {
            return (direct, null, false);
        }

        string? reference = items["$ref"]?.GetValue<string>();
        if (reference is null)
        {
            throw Invalid(schemaId, "properties.ops.items must expose oneOf directly or through a local $ref");
        }

        JsonObject definition = ResolvePointer(root, reference, schemaId);
        if (definition["oneOf"] is JsonArray oneOf)
        {
            return (oneOf, reference, false);
        }
        if (definition["allOf"] is JsonArray allOf)
        {
            return (allOf, reference, true);
        }
        throw Invalid(
            schemaId,
            $"referenced operation definition '{reference}' has no oneOf or conditional allOf");
    }

    private static JsonObject Resolve(JsonObject root, JsonNode branch, string schemaId)
    {
        if (branch is not JsonObject candidate)
        {
            throw Invalid(schemaId, "an operation branch is not an object");
        }
        string? reference = candidate["$ref"]?.GetValue<string>();
        return reference is null
            ? candidate
            : ResolvePointer(root, reference, schemaId);
    }

    private static string? ReadOperation(JsonObject branch) =>
        branch["properties"]?["op"]?["const"]?.GetValue<string>()
        ?? branch["if"]?["properties"]?["op"]?["const"]?.GetValue<string>();

    private static JsonObject ResolvePointer(JsonObject root, string pointer, string schemaId)
    {
        if (!pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            throw Invalid(schemaId, $"operation reference '{pointer}' is not a local JSON pointer");
        }

        JsonNode? current = root;
        foreach (string token in pointer[2..].Split('/'))
        {
            string property = token.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            current = current?[property];
        }

        return current as JsonObject
            ?? throw Invalid(schemaId, $"operation reference '{pointer}' does not resolve to an object");
    }

    private static InvalidOperationException Invalid(string schemaId, string reason) =>
        new($"Operation schema '{schemaId}' is invalid: {reason}.");
}
