using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Splits what <c>schema</c> publishes into one contract snapshot per owner, so a change to one
/// product touches one file. <c>schema/&lt;product&gt;.json</c> holds the product's catalog entry and
/// every document its ids name: its commands, results, object table and formats.
/// <c>schema/common.json</c> holds the rest of the catalog, its ids among them, and every common
/// document. The parts of an object table's schema are views of the whole one, which the product's
/// snapshot holds.
/// </summary>
internal static class SchemaSnapshots
{
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The ids of the products the catalog lists, in its order.</summary>
    public static IReadOnlyList<string> Products(JsonNode catalog) =>
        [.. catalog["products"]!.AsArray().Select(static product => product!["id"]!.GetValue<string>())];

    /// <summary>The snapshot of one product.</summary>
    /// <param name="catalog">The catalog of the whole build.</param>
    /// <param name="product">The product id.</param>
    /// <param name="document">Reads the document of an id.</param>
    public static string Product(JsonNode catalog, string product, Func<string, JsonNode> document) =>
        Write(new JsonObject
        {
            ["product"] = catalog["products"]!.AsArray().Single(candidate => candidate!["id"]!.GetValue<string>() == product)!.DeepClone(),
            ["documents"] = Documents(catalog, product, document),
        });

    /// <summary>The snapshot of everything the products do not own.</summary>
    /// <param name="catalog">The catalog of the whole build.</param>
    /// <param name="document">Reads the document of an id.</param>
    public static string Common(JsonNode catalog, Func<string, JsonNode> document)
    {
        var snapshot = new JsonObject();
        foreach ((string key, JsonNode? value) in catalog.AsObject())
        {
            snapshot[key] = key == "products"
                ? new JsonArray([.. Products(catalog).Select(static id => (JsonNode)id)])
                : value?.DeepClone();
        }

        snapshot["documents"] = Documents(catalog, "common", document);
        return Write(snapshot);
    }

    private static JsonObject Documents(JsonNode catalog, string owner, Func<string, JsonNode> document)
    {
        string[] all = [.. catalog["ids"]!.AsArray().Select(static id => id!.GetValue<string>())];
        HashSet<string> whole = [.. all];
        string[] ids =
        [
            .. all.Where(id => id.Split('/')[1] == owner
                && !(id.LastIndexOf('/') is var slash && id[..slash].EndsWith("/ops", StringComparison.Ordinal) && whole.Contains(id[..slash]))),
        ];
        var documents = new JsonNode[ids.Length];
        Parallel.For(0, ids.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, index => documents[index] = document(ids[index]));
        var result = new JsonObject();
        for (int index = 0; index < ids.Length; index++)
        {
            result[ids[index]] = documents[index];
        }

        return result;
    }

    private static string Write(JsonNode snapshot) => snapshot.ToJsonString(Format) + "\n";
}
