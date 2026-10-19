using System.Text.Json;
using Aspose.Cli.Sdk.Extensibility;
using Json.Schema;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The published <c>v2/&lt;product&gt;/ops</c> schema of a product, so a test can judge it against
/// the operations the product's parser accepts and rejects.
/// </summary>
public static class OpsSchema<TModule>
    where TModule : IProductModule, new()
{
    private static readonly Lazy<JsonSchema> Schema = new(static () =>
    {
        var module = new TModule();
        return JsonSchema.FromText(
            ProductCatalog.Build([module]).Resources.Read($"v2/{module.Define().Manifest.Id}/ops"),
            SchemaTestRegistry.CreateOptions());
    });

    /// <summary>Whether the schema accepts the ops document <paramref name="json"/>.</summary>
    public static bool Accepts(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }
}
