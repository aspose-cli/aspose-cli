using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Widens a generated value schema so that JSON null is valid too, for the result and operation
/// schema writers alike.
/// </summary>
internal static class SchemaNull
{
    /// <summary>
    /// <paramref name="schema"/> accepting null as well: a typed schema adds <c>null</c> to its
    /// type and to the list of values it allows, including the list of an open enumeration's
    /// <c>anyOf</c>; an untyped schema that lists its values adds null to them; a reference is
    /// wrapped in an <c>anyOf</c> with the null type. Any other untyped schema accepts null
    /// already and is returned as it is.
    /// </summary>
    internal static JsonObject Allow(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.ContainsKey("$ref"))
        {
            return new JsonObject { ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" }) };
        }

        if (schema["type"] is JsonValue type)
        {
            schema["type"] = new JsonArray(type.GetValue<string>(), "null");
        }

        AddNull(schema["enum"]);
        if (schema["anyOf"] is JsonArray branches)
        {
            foreach (JsonNode? branch in branches)
            {
                AddNull(branch?["enum"]);
            }
        }

        return schema;
    }

    private static void AddNull(JsonNode? values)
    {
        if (values is JsonArray list && !list.Any(static value => value is null))
        {
            list.Add(null);
        }
    }
}
