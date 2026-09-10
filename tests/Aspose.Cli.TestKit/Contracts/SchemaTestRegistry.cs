using Aspose.Cli.Sdk.Resources;
using Json.Schema;

namespace Aspose.Cli.TestKit;

/// <summary>Build options with every common SDK schema registered by identity.</summary>
public static class SchemaTestRegistry
{
    public static BuildOptions CreateOptions()
    {
        var registry = new SchemaRegistry();
        var options = new BuildOptions { SchemaRegistry = registry };
        foreach (string id in SdkSchemaCatalog.Ids)
        {
            // Building with this local registry registers the schema by its
            // own $id. Parsing against the global registry first would make a
            // later local Register look like an attempted overwrite.
            _ = JsonSchema.FromText(SdkSchemaCatalog.Read(id), options);
        }

        return options;
    }
}
