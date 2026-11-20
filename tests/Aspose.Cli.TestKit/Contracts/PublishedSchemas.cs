using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The JSON Schemas the built CLI publishes, read only through its <c>schema</c> command, so a
/// test sees what an agent sees, whatever produces the schemas inside the CLI. What one build
/// printed is kept by <see cref="PublishedSchemaCache"/> for the other test processes of a run.
/// </summary>
public static class PublishedSchemas
{
    public const string UriPrefix = "https://schemas.aspose.com/aspose-cli/";
    public const string UriSuffix = ".schema.json";
    private static readonly Lazy<Published> Current = new(Load);

    /// <summary>
    /// Every id of a whole JSON Schema <c>aspose-cli schema</c> lists, in its order: not a command's
    /// or another document that is a result, and not a part of an object table's schema.
    /// </summary>
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
        PublishedSchemaSnapshot snapshot = PublishedSchemaCache.GetOrAdd(
            CliRunner.ExecutablePath, PublishedSchemaCache.DefaultDirectory, Read);
        IReadOnlyList<string> ids = snapshot.Ids;
        Dictionary<string, JsonObject> documents = ids.ToDictionary(
            static id => id, id => JsonNode.Parse(snapshot.Documents[id])!.AsObject(), StringComparer.Ordinal);

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

    /// <summary>
    /// Asks the built CLI for every document its catalog lists, as an agent would, and keeps the
    /// whole JSON Schemas: a document with <c>$schema</c> whose <c>$id</c> is the id's own URI.
    /// </summary>
    private static PublishedSchemaSnapshot Read()
    {
        using var workspace = new TempWorkspace();
        CliResult list = workspace.Run("schema", "--output-format", "json");
        if (list.ExitCode != 0)
        {
            throw new InvalidOperationException("aspose-cli schema failed: " + list.StdErr);
        }

        string[] listed = [.. JsonNode.Parse(list.StdOut)!["ids"]!.AsArray().Select(static id => id!.GetValue<string>())];
        HashSet<string> all = [.. listed];
        // A part of an object table's schema is a view of the whole one, so it is not read.
        string[] candidates = [.. listed.Where(id => !(id.LastIndexOf('/') is var slash and > 0 && id[..slash].EndsWith("/ops", StringComparison.Ordinal) && all.Contains(id[..slash])))];
        var documents = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        Parallel.ForEach(candidates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, id =>
        {
            CliResult schema = workspace.Run("schema", id);
            if (schema.ExitCode != 0)
            {
                throw new InvalidOperationException($"aspose-cli schema {id} failed: {schema.StdErr}");
            }

            JsonNode? document = JsonNode.Parse(schema.StdOut);
            if (document?["$schema"] is not null && document["$id"]?.GetValue<string>() == UriPrefix + id + UriSuffix)
            {
                documents[id] = schema.StdOut;
            }
        });

        string[] ids = [.. candidates.Where(documents.ContainsKey)];
        return new PublishedSchemaSnapshot(ids, documents);
    }

    private sealed record Published(IReadOnlyList<string> Ids, IReadOnlyDictionary<string, JsonObject> Documents, BuildOptions Options);
}
