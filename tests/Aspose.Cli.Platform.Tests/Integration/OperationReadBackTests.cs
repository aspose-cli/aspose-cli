using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The <c>x-readBack</c> of every operation field a product publishes says where a read reports
/// what the field writes in a form any JSON Schema tool resolves, the form of the schemas' own
/// <c>$ref</c>: a canonical schema URI and a JSON Pointer into that schema. The ops schema says
/// what the keyword means.
/// </summary>
public sealed class OperationReadBackTests
{
    private static readonly string[] Kinds = ["unread", "position", "not-state"];

    [Fact]
    public void EveryReadPath_IsACanonicalSchemaUriWithAJsonPointerThatResolves()
    {
        var problems = new List<string>();
        string[] opsSchemas = [.. PublishedSchemas.Ids.Where(static id => id.EndsWith("/ops", StringComparison.Ordinal))];
        Assert.NotEmpty(opsSchemas);
        int paths = 0;
        foreach (string id in opsSchemas)
        {
            JsonObject document = PublishedSchemas.Document(id);
            if (document["description"]?.GetValue<string>() is not { } description
                || !description.Contains("x-readBack", StringComparison.Ordinal)
                || !Kinds.All(kind => description.Contains(kind, StringComparison.Ordinal)))
            {
                problems.Add($"{id}: the description does not say what x-readBack and its values {string.Join(", ", Kinds)} mean");
            }

            foreach ((string location, string value) in ReadBacks(document, string.Empty))
            {
                if (Kinds.Contains(value))
                {
                    continue;
                }

                paths++;
                if (Resolve(value) is { } problem)
                {
                    problems.Add($"{id} {location}: {value} {problem}");
                }
            }
        }

        Assert.True(paths > 0, "No published operation field has a read path.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>Why a read path does not resolve, or null when it resolves to a schema.</summary>
    private static string? Resolve(string path)
    {
        int hash = path.IndexOf('#', StringComparison.Ordinal);
        if (hash < 0 || !path[(hash + 1)..].StartsWith('/'))
        {
            return "is not a schema URI with a JSON Pointer fragment";
        }

        string target;
        try
        {
            target = PublishedSchemas.IdOf(path[..hash]);
        }
        catch (ArgumentException)
        {
            return "does not name a canonical schema URI";
        }

        if (!PublishedSchemas.Ids.Contains(target))
        {
            return $"names the schema {target}, which is not published";
        }

        JsonNode? node = PublishedSchemas.Document(target);
        foreach (string segment in path[(hash + 2)..].Split('/'))
        {
            node = node is JsonObject owner ? owner[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)] : null;
        }

        return node is JsonObject ? null : "does not resolve to a schema";
    }

    private static IEnumerable<(string Location, string Value)> ReadBacks(JsonNode? node, string location)
    {
        switch (node)
        {
            case JsonObject owner:
                foreach ((string name, JsonNode? value) in owner)
                {
                    if (name == "x-readBack")
                    {
                        yield return (location, value!.GetValue<string>());
                        continue;
                    }

                    foreach ((string Location, string Value) item in ReadBacks(value, $"{location}/{name}"))
                    {
                        yield return item;
                    }
                }

                break;
            case JsonArray items:
                for (int index = 0; index < items.Count; index++)
                {
                    foreach ((string Location, string Value) item in ReadBacks(items[index], $"{location}/{index}"))
                    {
                        yield return item;
                    }
                }

                break;
        }
    }
}
