using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Splits the capabilities document into one contract snapshot per owner, so a change to one
/// product touches one file. <c>capabilities/&lt;product&gt;.json</c> holds the product's entry
/// (with its commands, relative to the executable), its routes, its diagnostics and its schemas
/// (operations and results); <c>capabilities/host.json</c> holds everything else: the Host
/// commands, the common diagnostics, the routing defaults, budgets, engine pins and the common
/// schemas. Each command is listed once: a product command only in its product's entry.
/// </summary>
internal static class CapabilitiesSnapshots
{
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The ids of the products the capabilities document lists, in its order.</summary>
    public static IReadOnlyList<string> Products(JsonNode capabilities) =>
        [.. capabilities["products"]!.AsArray().Select(static product => product!["id"]!.GetValue<string>())];

    /// <summary>The snapshot of one product.</summary>
    /// <param name="capabilities">The capabilities document of the whole build.</param>
    /// <param name="product">The product id.</param>
    /// <param name="schema">Reads the raw JSON Schema document of a schema id.</param>
    public static string Product(JsonNode capabilities, string product, Func<string, JsonNode> schema)
    {
        JsonNode entry = capabilities["products"]!.AsArray().Single(candidate => Id(candidate!) == product)!;
        var snapshot = new JsonObject
        {
            ["product"] = entry.DeepClone(),
            ["routes"] = Clone(capabilities["routing"]!["routes"]!.AsArray()
                .Where(route => route!["product"]!.GetValue<string>() == product)),
            ["diagnostics"] = Clone(capabilities["diagnostics"]!.AsArray()
                .Where(diagnostic => diagnostic!["owner"]!.GetValue<string>() == product)),
            ["schemas"] = Schemas(capabilities, id => SchemaOwner(capabilities, id) == product, schema),
        };
        return Write(snapshot);
    }

    /// <summary>The snapshot of everything the products do not own.</summary>
    /// <param name="capabilities">The capabilities document of the whole build.</param>
    /// <param name="schema">Reads the raw JSON Schema document of a schema id.</param>
    public static string Host(JsonNode capabilities, Func<string, JsonNode> schema)
    {
        HashSet<string> products = [.. Products(capabilities)];
        HashSet<string> productCommands =
        [
            .. capabilities["products"]!.AsArray().SelectMany(static product => product!["commands"]!.AsArray()
                .Select(static command => command!["path"]!.GetValue<string>())),
        ];
        var snapshot = new JsonObject();
        foreach ((string key, JsonNode? value) in capabilities.AsObject())
        {
            snapshot[key] = key switch
            {
                "products" => new JsonArray([.. Products(capabilities).Select(static id => (JsonNode)id)]),
                "routing" => Without(value!.AsObject(), "routes"),
                "commands" => Clone(value!.AsArray().Where(command =>
                    !productCommands.Contains(Relative(command!["path"]!.GetValue<string>())))),
                "diagnostics" => Clone(value!.AsArray().Where(diagnostic =>
                    !products.Contains(diagnostic!["owner"]!.GetValue<string>()))),
                "schemas" => Schemas(capabilities, id => SchemaOwner(capabilities, id) is null, schema),
                _ => value?.DeepClone(),
            };
        }
        return Write(snapshot);
    }

    /// <summary>The product a schema id names after its version segment, or none for a common schema.</summary>
    private static string? SchemaOwner(JsonNode capabilities, string id)
    {
        string[] segments = id.Split('/');
        return segments.Length > 2 && Products(capabilities).Contains(segments[1], StringComparer.Ordinal)
            ? segments[1]
            : null;
    }

    private static JsonObject Schemas(JsonNode capabilities, Func<string, bool> owned, Func<string, JsonNode> schema)
    {
        string[] ids = [.. capabilities["schemas"]!.AsArray().Select(static id => id!.GetValue<string>()).Where(owned)];
        var documents = new JsonNode[ids.Length];
        Parallel.For(0, ids.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index => documents[index] = schema(ids[index]));
        var result = new JsonObject();
        for (int index = 0; index < ids.Length; index++)
        {
            result[ids[index]] = documents[index];
        }
        return result;
    }

    private static string Id(JsonNode product) => product["id"]!.GetValue<string>();

    /// <summary>A command path relative to the executable: without the root command's name.</summary>
    private static string Relative(string path) =>
        path.IndexOf(' ', StringComparison.Ordinal) is var separator and >= 0 ? path[(separator + 1)..] : string.Empty;

    private static JsonArray Clone(IEnumerable<JsonNode?> nodes) => new([.. nodes.Select(static node => node?.DeepClone())]);

    private static JsonObject Without(JsonObject value, string property)
    {
        var result = new JsonObject();
        foreach ((string key, JsonNode? member) in value)
        {
            if (key != property)
            {
                result[key] = member?.DeepClone();
            }
        }
        return result;
    }

    private static string Write(JsonNode snapshot) => snapshot.ToJsonString(Format) + "\n";
}
