using Aspose.Cli.Sdk.Serialization;
using Json.Schema;

namespace Aspose.Cli.TestKit;

/// <summary>Build options with every common SDK schema registered by identity.</summary>
public static class SchemaTestRegistry
{
    /// <summary>Registers every schema the SDK publishes under <c>v2/common</c>.</summary>
    public static BuildOptions CreateOptions() => CreateOptions(SdkSchemaCatalog.Ids, SdkSchemaCatalog.Read);

    /// <summary>Registers the schemas <paramref name="read"/> returns for <paramref name="ids"/>, such as the host's common schemas.</summary>
    public static BuildOptions CreateOptions(IEnumerable<string> ids, Func<string, string> read)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(read);
        var registry = new SchemaRegistry();
        var options = new BuildOptions { SchemaRegistry = registry };

        // Building with this local registry registers the schema by its own $id. Parsing against
        // the global registry first would make a later local Register look like an attempted
        // overwrite. A schema can reference another one; build them until every reference resolves.
        var pending = new List<string>(ids);
        while (pending.Count > 0)
        {
            var failed = new List<string>();
            Exception? last = null;
            foreach (string id in pending)
            {
                try
                {
                    _ = JsonSchema.FromText(read(id), options);
                }
                catch (RefResolutionException exception)
                {
                    failed.Add(id);
                    last = exception;
                }
            }

            if (failed.Count == pending.Count)
            {
                throw new InvalidOperationException("Common schemas reference schemas that are not common: " + string.Join(", ", failed), last);
            }

            pending = failed;
        }

        return options;
    }
}
