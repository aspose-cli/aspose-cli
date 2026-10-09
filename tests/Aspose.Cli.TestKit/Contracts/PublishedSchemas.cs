using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The JSON Schemas the built CLI publishes, read only through its <c>schema</c> command, so a
/// test sees what an agent sees, whatever produces the schemas inside the CLI.
/// </summary>
public static class PublishedSchemas
{
    public const string UriPrefix = "https://schemas.aspose.com/aspose-cli/";
    public const string UriSuffix = ".schema.json";
    private static readonly Lazy<Published> Current = new(Load);

    /// <summary>Every schema id <c>aspose-cli schema</c> lists, in its order.</summary>
    public static IReadOnlyList<string> Ids => Current.Value.Ids;

    /// <summary>
    /// The ids of the result schemas: every published schema except a product's operation
    /// (input) schema, whose id ends in <c>/ops</c>.
    /// </summary>
    public static IEnumerable<string> ResultIds => Ids.Where(static id => !id.EndsWith("/ops", StringComparison.Ordinal));

    /// <summary>A copy of the raw schema document <c>aspose-cli schema &lt;id&gt;</c> prints.</summary>
    public static JsonObject Document(string id) => (JsonObject)Current.Value.Documents[id].DeepClone();

    /// <summary>The schema of <paramref name="id"/>, with every published schema registered for its references.</summary>
    public static JsonSchema Schema(string id) =>
        Current.Value.Options.SchemaRegistry.Get(new Uri(UriPrefix + id + UriSuffix)) as JsonSchema
        ?? throw new InvalidOperationException($"Schema '{id}' is not registered.");

    /// <summary>The id of a canonical schema URI, such as the <c>schema</c> member of an envelope.</summary>
    public static string IdOf(string uri)
    {
        if (!uri.StartsWith(UriPrefix, StringComparison.Ordinal) || !uri.EndsWith(UriSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{uri}' is not a canonical schema URI.", nameof(uri));
        }

        return uri[UriPrefix.Length..^UriSuffix.Length];
    }

    private static Published Load()
    {
        using var workspace = new TempWorkspace();
        CliResult list = workspace.Run("schema", "--output", "json");
        if (list.ExitCode != 0)
        {
            throw new InvalidOperationException("aspose-cli schema failed: " + list.StdErr);
        }

        string[] ids = [.. JsonNode.Parse(list.StdOut)!["schemas"]!.AsArray().Select(static id => id!.GetValue<string>())];
        var documents = new ConcurrentDictionary<string, JsonObject>(StringComparer.Ordinal);
        Parallel.ForEach(ids, new ParallelOptions { MaxDegreeOfParallelism = 8 }, id =>
        {
            CliResult schema = workspace.Run("schema", id);
            if (schema.ExitCode != 0)
            {
                throw new InvalidOperationException($"aspose-cli schema {id} failed: {schema.StdErr}");
            }

            documents[id] = JsonNode.Parse(schema.StdOut)!.AsObject();
        });

        // A schema can reference another one; build them until every reference resolves.
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var pending = new List<string>(ids);
        while (pending.Count > 0)
        {
            var failed = new List<string>();
            Exception? last = null;
            foreach (string id in pending)
            {
                try
                {
                    _ = JsonSchema.FromText(documents[id].ToJsonString(), options);
                }
                catch (RefResolutionException exception)
                {
                    failed.Add(id);
                    last = exception;
                }
            }

            if (failed.Count == pending.Count)
            {
                throw new InvalidOperationException("Published schemas reference schemas that are not published: " + string.Join(", ", failed), last);
            }

            pending = failed;
        }

        return new Published(ids, documents, options);
    }

    private sealed record Published(IReadOnlyList<string> Ids, IReadOnlyDictionary<string, JsonObject> Documents, BuildOptions Options);
}
