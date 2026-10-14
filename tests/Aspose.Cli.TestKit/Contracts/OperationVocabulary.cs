using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Compares the fields of the objects results list with the operation fields of the same name
/// (see <c>ProductContractTests.ListedObjects_ShareTheOperationVocabulary</c>).
/// </summary>
public static class OperationVocabulary
{
    /// <summary>
    /// The fields of listed objects in <paramref name="resultSchemas"/> whose shape no operation
    /// field of that name in <paramref name="operationSchemas"/> accepts, each by the place that
    /// defines it, and the exemptions in <paramref name="homonyms"/> that cover none of them. An
    /// exemption names one property by its result schema and JSON pointer, such as
    /// <c>#/$defs/slidesShapeData/properties/kind</c>, so another field of that name, in the same
    /// schema or another, is still compared.
    /// </summary>
    public static IEnumerable<string> Mismatches(
        IEnumerable<JsonObject> operationSchemas,
        IEnumerable<(string Id, JsonObject Document)> resultSchemas,
        IReadOnlyDictionary<(string Schema, string Pointer), string> homonyms)
    {
        ArgumentNullException.ThrowIfNull(operationSchemas);
        ArgumentNullException.ThrowIfNull(resultSchemas);
        ArgumentNullException.ThrowIfNull(homonyms);
        var editFields = new List<Field>();
        foreach (JsonObject document in operationSchemas)
        {
            CollectFields(document, document, "#", listed: true, editFields, []);
        }
        Dictionary<string, FieldShape[]> edit = editFields.GroupBy(static field => field.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Select(static field => field.Shape).ToArray(), StringComparer.Ordinal);

        var used = new HashSet<(string Schema, string Pointer)>();
        var mismatches = new List<string>();
        foreach ((string id, JsonObject document) in resultSchemas)
        {
            var read = new List<Field>();
            CollectFields(document, document, "#", listed: false, read, []);
            foreach (IGrouping<string, Field> place in read.GroupBy(static field => field.Pointer, StringComparer.Ordinal))
            {
                string name = place.First().Name;
                if (name is "op" or "id" || !edit.TryGetValue(name, out FieldShape[]? accepted))
                {
                    continue;
                }

                FieldShape[] unfit = [.. place.Select(static field => field.Shape).Where(shape => !accepted.Any(shape.FitsIn)).Distinct()];
                if (unfit.Length > 0 && homonyms.ContainsKey((id, place.Key)))
                {
                    used.Add((id, place.Key));
                    continue;
                }

                mismatches.AddRange(unfit.Select(shape =>
                    $"{id}: '{name}' at {place.Key} is {shape}, but operations accept {string.Join(" or ", accepted.Distinct())}"));
            }
        }

        mismatches.AddRange(homonyms.Keys.Where(key => !used.Contains(key))
            .Select(static key => $"{key.Schema}: {key.Pointer} is exempted as a homonym, but it matches the operations or is no listed field; delete the exemption"));
        return mismatches;
    }

    /// <summary>One named property: where it is defined and the shape it states.</summary>
    private sealed record Field(string Name, string Pointer, FieldShape Shape);

    /// <summary>The JSON types and allowed values one schema states for a named field.</summary>
    private sealed record FieldShape(string Types, string? Values)
    {
        public bool FitsIn(FieldShape accepted)
        {
            string[] offered = accepted.Types.Split('|');
            bool typeFits = Types.Length == 0 || accepted.Types.Length == 0
                || Types.Split('|').All(type =>
                    offered.Contains(type) || (type == "integer" && offered.Contains("number")));
            return typeFits
                && (Values is null || accepted.Values is null
                    || Values.Split('|').All(accepted.Values.Split('|').Contains));
        }

        public override string ToString() =>
            (Types.Length == 0 ? "untyped" : Types) + (Values is null ? string.Empty : $" [{Values}]");

        public static FieldShape Of(JsonObject schema)
        {
            IEnumerable<string> types = schema["type"] switch
            {
                JsonValue single => [single.GetValue<string>()],
                JsonArray several => several.Select(static type => type!.GetValue<string>()),
                _ => [],
            };
            JsonNode?[]? values = schema["enum"] is JsonArray allowed ? [.. allowed]
                : schema["const"] is JsonNode fixedValue ? [fixedValue] : null;
            return new FieldShape(
                string.Join('|', types.Where(static type => type != "null").Order(StringComparer.Ordinal)),
                values is null ? null : string.Join('|', values
                    .Where(static value => value is not null)
                    .Select(static value => value!.ToJsonString())
                    .Order(StringComparer.Ordinal)));
        }
    }

    /// <summary>
    /// Collects the named properties of <paramref name="node"/> and everything below it,
    /// following local references once each; a property counts only once the walk is inside a
    /// listed object (below an array's items) unless <paramref name="listed"/> starts true.
    /// </summary>
    private static void CollectFields(
        JsonObject document,
        JsonNode node,
        string pointer,
        bool listed,
        List<Field> fields,
        HashSet<string> followed)
    {
        if (node is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                if (array[index] is { } child)
                {
                    CollectFields(document, child, $"{pointer}/{index}", listed, fields, followed);
                }
            }

            return;
        }

        if (node is not JsonObject schema)
        {
            return;
        }

        if (schema["$ref"] is JsonValue reference
            && reference.GetValue<string>() is { } target
            && target.StartsWith("#/$defs/", StringComparison.Ordinal)
            && followed.Add($"{listed}:{target}"))
        {
            CollectFields(document, document["$defs"]![target["#/$defs/".Length..]]!, target, listed, fields, followed);
        }

        foreach ((string key, JsonNode? child) in schema)
        {
            if (child is null || key is "$defs" or "$ref")
            {
                continue;
            }

            if (key == "properties" && child is JsonObject properties)
            {
                foreach ((string name, JsonNode? value) in properties)
                {
                    if (value is not JsonObject property)
                    {
                        continue;
                    }

                    string place = $"{pointer}/properties/{Escape(name)}";
                    if (listed)
                    {
                        fields.Add(new Field(name, place, FieldShape.Of(Resolve(document, property))));
                    }

                    CollectFields(document, property, place, listed, fields, followed);
                }

                continue;
            }

            CollectFields(document, child, $"{pointer}/{Escape(key)}", listed || key is "items" or "prefixItems", fields, followed);
        }
    }

    private static string Escape(string key) =>
        key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static JsonObject Resolve(JsonObject document, JsonObject property) =>
        property["$ref"] is JsonValue reference
        && reference.GetValue<string>() is { } target
        && target.StartsWith("#/$defs/", StringComparison.Ordinal)
            ? document["$defs"]![target["#/$defs/".Length..]]!.AsObject()
            : property;
}
