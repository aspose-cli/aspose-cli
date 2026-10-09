using System.Text.Json.Nodes;

namespace Aspose.Cli.Tests;

/// <summary>
/// Every result schema the CLI publishes is closed and described: each object schema that
/// declares properties refuses undeclared ones, and each declared property says what it means.
/// A loose schema lets a result gain or rename a field unnoticed by every test that validates
/// real output against it, and an undescribed field leaves an agent guessing.
/// </summary>
/// <remarks>
/// <para>
/// The result schemas are every schema <c>aspose-cli schema</c> lists except a product's
/// <c>ops</c> input schema. An object schema is closed by <c>"additionalProperties": false</c> or
/// <c>"unevaluatedProperties": false</c>. An object without declared properties, such as an open
/// dictionary (<c>"additionalProperties": { … }</c>), is not affected. A property is described by
/// a non-empty <c>description</c> on its schema or on the schema its <c>$ref</c> names.
/// </para>
/// <para>
/// A branch of <c>allOf</c>, <c>anyOf</c>, <c>oneOf</c>, <c>not</c> or <c>if</c>/<c>then</c>/<c>else</c>
/// that only narrows properties its enclosing object declares, such as the per-type cases of a
/// value discriminated by <c>t</c>, is a refinement: the enclosing object closes and describes them.
/// </para>
/// </remarks>
public sealed class ResultSchemaStrictnessTests
{
    /// <summary>
    /// Objects allowed to stay open or undescribed, as <c>&lt;id&gt;#&lt;JSON pointer&gt;</c> with
    /// the reason. Only tighten: an entry that no longer matches a problem fails the test.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Exemptions = new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly string[] SubschemaArrays = ["allOf", "anyOf", "oneOf", "prefixItems"];
    private static readonly string[] Refinements = ["not", "if", "then", "else"];
    private static readonly string[] Subschemas = ["items", "contains", "additionalProperties", "unevaluatedProperties", "unevaluatedItems", "propertyNames"];
    private static readonly string[] SubschemaMaps = ["$defs", "definitions", "patternProperties", "dependentSchemas"];

    [Fact]
    public void EveryResultObject_IsClosedAndEveryPropertyDescribed()
    {
        string[] ids = [.. PublishedSchemas.ResultIds];
        Assert.Contains("v2/pdf/render-result", ids);
        var problems = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            JsonObject document = PublishedSchemas.Document(id);
            Visit(id, document, document, "#", refined: null, problems);
        }

        string[] stale = [.. Exemptions.Keys.Where(key => !problems.ContainsKey(key))];
        string[] open = [.. problems.Where(problem => !Exemptions.ContainsKey(problem.Key)).Select(static problem => $"{problem.Key}: {problem.Value}")];

        Assert.True(stale.Length == 0, "Exemptions that no longer match a problem; delete them: " + string.Join(", ", stale));
        Assert.True(
            open.Length == 0,
            $"{open.Length} result schema problems; close each object (additionalProperties or unevaluatedProperties false) "
            + "and describe each property through the record's documentation comment:"
            + Environment.NewLine + string.Join(Environment.NewLine, open));
    }

    private static void Visit(
        string id,
        JsonObject document,
        JsonNode? node,
        string pointer,
        IReadOnlySet<string>? refined,
        SortedDictionary<string, string> problems)
    {
        if (node is not JsonObject schema)
        {
            return;
        }

        HashSet<string>? declared = null;
        if (schema["properties"] is JsonObject properties)
        {
            declared = [.. properties.Select(static property => property.Key)];
            bool refinement = refined is not null && declared.IsSubsetOf(refined);
            if (!refinement)
            {
                if (!IsFalse(schema["additionalProperties"]) && !IsFalse(schema["unevaluatedProperties"]))
                {
                    problems[$"{id}{pointer}"] = "declares properties but accepts undeclared ones";
                }

                foreach ((string name, JsonNode? property) in properties)
                {
                    if (!IsDescribed(document, property))
                    {
                        problems[$"{id}{pointer}/properties/{Escape(name)}"] = "has no description";
                    }
                }
            }

            foreach ((string name, JsonNode? property) in properties)
            {
                Visit(id, document, property, $"{pointer}/properties/{Escape(name)}", refined: null, problems);
            }
        }

        IReadOnlySet<string>? enclosing = declared is null ? refined : refined is null ? declared : [.. declared, .. refined];
        foreach (string keyword in SubschemaArrays)
        {
            if (schema[keyword] is JsonArray branches)
            {
                for (int index = 0; index < branches.Count; index++)
                {
                    Visit(id, document, branches[index], $"{pointer}/{keyword}/{index}", keyword == "prefixItems" ? null : enclosing, problems);
                }
            }
        }

        foreach (string keyword in Refinements)
        {
            Visit(id, document, schema[keyword], $"{pointer}/{keyword}", enclosing, problems);
        }

        foreach (string keyword in Subschemas)
        {
            Visit(id, document, schema[keyword], $"{pointer}/{keyword}", refined: null, problems);
        }

        foreach (string keyword in SubschemaMaps)
        {
            if (schema[keyword] is JsonObject map)
            {
                foreach ((string name, JsonNode? child) in map)
                {
                    Visit(id, document, child, $"{pointer}/{keyword}/{Escape(name)}", refined: null, problems);
                }
            }
        }
    }

    private static bool IsFalse(JsonNode? node) => node is JsonValue value && value.TryGetValue(out bool flag) && !flag;

    private static bool IsDescribed(JsonObject document, JsonNode? property)
    {
        if (property is not JsonObject schema)
        {
            return false;
        }

        if (schema["description"] is JsonValue description
            && description.TryGetValue(out string? text)
            && !string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return schema["$ref"] is JsonValue reference
            && reference.TryGetValue(out string? target)
            && Resolve(document, target) is JsonObject referenced
            && referenced["description"] is JsonValue referencedDescription
            && referencedDescription.TryGetValue(out string? referencedText)
            && !string.IsNullOrWhiteSpace(referencedText);
    }

    /// <summary>The schema a local (<c>#/…</c>) or published-schema reference names, or null.</summary>
    private static JsonNode? Resolve(JsonObject document, string reference)
    {
        int hash = reference.IndexOf('#', StringComparison.Ordinal);
        string resource = hash < 0 ? reference : reference[..hash];
        string fragment = hash < 0 ? string.Empty : reference[(hash + 1)..];
        JsonNode? current = resource.Length == 0
            ? document
            : resource.StartsWith(PublishedSchemas.UriPrefix, StringComparison.Ordinal)
                && resource.EndsWith(PublishedSchemas.UriSuffix, StringComparison.Ordinal)
                && PublishedSchemas.Ids.Contains(PublishedSchemas.IdOf(resource))
                    ? PublishedSchemas.Document(PublishedSchemas.IdOf(resource))
                    : null;
        foreach (string segment in fragment.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current is JsonObject container
                ? container[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)]
                : null;
        }

        return current;
    }

    private static string Escape(string name) =>
        name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
