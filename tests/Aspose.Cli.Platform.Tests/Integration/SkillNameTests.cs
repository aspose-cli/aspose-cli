using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every JSON document a Skill or the README shows validates against the schema the CLI serves
/// (see <see cref="JsonBlocks_ValidateAgainstTheSchemaTheyShow"/>). The names a Skill, the README
/// and the C# sources write (codes, options and their values, operations and their fields,
/// commands) are checked by one reader, the C-R38 invariant cases of the invariant tests.
/// </summary>
public sealed partial class SkillNameTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>The project whose Skills hold a file, such as <c>Aspose.Cli.Product.Pdf</c>, or the README's name.</summary>
    private static string SkillSetOf(string file) =>
        Path.GetRelativePath(RepositoryPaths.Root, file).Split(Path.DirectorySeparatorChar) is ["src", { } project, ..]
            ? project
            : Path.GetFileName(file);

    /// <summary>
    /// Every JSON document a Skill or the README shows, in a fenced <c>json</c> block or an
    /// indented code block, is one that the CLI reads or writes and validates against its schema:
    /// an envelope that names its schema against that schema; an operation document
    /// (<c>{"ops": [...]}</c>), or one operation (<c>{"op": ...}</c>) as a batch of one, against
    /// the ops schema of the Skill's product (any product's for the platform Skill and the
    /// README); and one member of a result (<c>"window": {...}</c>) against a member of that name
    /// in a result schema of the product or the shared ones. A block that is none of these fails,
    /// so no JSON goes unchecked. The operation documents the platform Skill passes to a command's
    /// <c>--ops</c>, inline or as a file beside an example (<see cref="SkillOperationDocuments"/>),
    /// are judged the same way against the ops schema of the command's product. A block of the
    /// platform Skill or the README that no command passes names no product, so it is valid when
    /// some product's ops schema accepts it; naming its product would take a change of the
    /// platform Skill's text. The product contract tests validate the product operation documents
    /// too, with the reasons a batch is refused.
    /// </summary>
    [Fact]
    public void JsonBlocks_ValidateAgainstTheSchemaTheyShow()
    {
        JsonNode capabilities = Capabilities();
        Dictionary<string, string> opsSchemas = capabilities["products"]!.AsArray()
            .SelectMany(static product => (product!["operations"]?.AsArray() ?? [])
                .Select(operation => (Product: product["id"]!.GetValue<string>(), Schema: operation!["inputSchema"]!.GetValue<string>())))
            .ToDictionary(static pair => pair.Product, static pair => pair.Schema, StringComparer.Ordinal);
        var members = new ResultMembers();
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<(string Where, string Json, string? Product)> documents = JsonBlocks()
            .Select(static block => ($"{Path.GetRelativePath(RepositoryPaths.Root, block.File)}:{block.Line}", block.Json, ProductOf(block.File)));
        (IReadOnlyList<SkillOperationDocument> passed, IReadOnlyList<string> unread) =
            SkillOperationDocuments.Collect(Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills"));
        var problems = new List<string>(unread);
        foreach ((string where, string json, string? product) in documents.Concat(passed
            .Where(static document => document.Command is not null)
            .Select(document => (document.Location, document.Json, opsSchemas.ContainsKey(document.Command!) ? document.Command : null))))
        {
            string text = json.Trim();
            bool member = text.StartsWith('"');
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(member ? "{" + text + "}" : text);
            }
            catch (System.Text.Json.JsonException exception)
            {
                problems.Add($"{where}: does not parse ({exception.Message})");
                continue;
            }

            if (member && node is JsonObject { Count: 1 } wrapper)
            {
                kinds.Add("member");
                (string name, JsonNode? value) = wrapper.Single();
                if (members.Validate(name, value, product) is { } problem)
                {
                    problems.Add($"{where}: {problem}");
                }
                continue;
            }
            string[] candidates;
            if (node is JsonObject root && root["schema"] is JsonValue uri && uri.TryGetValue(out string? schema)
                && schema.StartsWith(PublishedSchemas.UriPrefix, StringComparison.Ordinal))
            {
                kinds.Add("envelope");
                candidates = [PublishedSchemas.IdOf(schema)];
            }
            else if (node is JsonArray || (node is JsonObject batch && (batch.ContainsKey("ops") || batch.ContainsKey("op"))))
            {
                // A bare array is the batch's operations; one operation is a batch of one.
                kinds.Add(node is JsonObject single && !single.ContainsKey("ops") ? "operation" : "operations");
                node = node switch
                {
                    JsonArray array => new JsonObject { ["ops"] = array.DeepClone() },
                    JsonObject one when !one.ContainsKey("ops") => new JsonObject { ["ops"] = new JsonArray(one.DeepClone()) },
                    _ => node,
                };
                candidates = product is null ? [.. opsSchemas.Values] : [opsSchemas[product]];
            }
            else
            {
                problems.Add($"{where}: shows JSON that names no schema, holds no operation and is no member of a result, so nothing checks it");
                continue;
            }
            using var instance = System.Text.Json.JsonDocument.Parse(node!.ToJsonString());
            if (!candidates.Any(id => PublishedSchemas.Schema(id).Evaluate(instance.RootElement).IsValid))
            {
                problems.Add($"{where}: valid against none of [{string.Join(", ", candidates)}]");
            }
        }

        string[] unmatched = [.. new[] { "envelope", "operations", "operation", "member" }.Where(kind => !kinds.Contains(kind))];
        Assert.True(unmatched.Length == 0,
            $"No Skill shows a JSON {string.Join(" or ", unmatched)}; the block rules stopped matching, so they check nothing.");
        Assert.True(problems.Count == 0,
            "Every JSON document a Skill or the README shows is valid against the schema the CLI serves:"
            + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    /// <summary>The product whose Skill holds a file, or null for the platform Skill and the README.</summary>
    private static string? ProductOf(string file) =>
        SkillSetOf(file) is { } set && set.StartsWith("Aspose.Cli.Product.", StringComparison.Ordinal)
            ? set["Aspose.Cli.Product.".Length..].ToLowerInvariant()
            : null;

    /// <summary>
    /// The members of the published result schemas by name, each validated through a reference to
    /// its place in its schema, with every published schema registered for the references inside.
    /// </summary>
    private sealed class ResultMembers
    {
        private readonly Json.Schema.BuildOptions _options = new() { SchemaRegistry = new Json.Schema.SchemaRegistry() };
        private readonly List<(string Id, string Name, string Pointer)> _members = [];

        public ResultMembers()
        {
            var pending = new List<string>(PublishedSchemas.Ids);
            while (pending.Count > 0)
            {
                var failed = new List<string>();
                foreach (string id in pending)
                {
                    try
                    {
                        _ = Json.Schema.JsonSchema.FromText(PublishedSchemas.Document(id).ToJsonString(), _options);
                    }
                    catch (Json.Schema.RefResolutionException)
                    {
                        failed.Add(id);
                    }
                }
                Assert.True(failed.Count < pending.Count, "Published schemas reference schemas that are not published: " + string.Join(", ", failed));
                pending = failed;
            }
            foreach (string id in PublishedSchemas.ResultIds)
            {
                Collect(id, PublishedSchemas.Document(id), string.Empty);
            }
        }

        private static string Escape(string key) =>
            key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

        private void Collect(string id, JsonNode? node, string pointer)
        {
            if (node is JsonArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    Collect(id, array[index], $"{pointer}/{index}");
                }
            }
            else if (node is JsonObject schema)
            {
                foreach ((string key, JsonNode? child) in schema)
                {
                    string path = $"{pointer}/{Escape(key)}";
                    if (key == "properties" && child is JsonObject properties)
                    {
                        _members.AddRange(properties.Select(property => (id, property.Key, $"{path}/{Escape(property.Key)}")));
                    }
                    Collect(id, child, path);
                }
            }
        }

        /// <summary>Null when a member of that name in the product's or a shared result schema accepts the value; else why not.</summary>
        public string? Validate(string name, JsonNode? value, string? product)
        {
            (string Id, string Name, string Pointer)[] candidates =
            [
                .. _members.Where(member => member.Name == name
                    && (product is null
                        || member.Id.StartsWith($"v2/{product}/", StringComparison.Ordinal)
                        || member.Id.StartsWith("v2/common/", StringComparison.Ordinal))),
            ];
            if (candidates.Length == 0)
            {
                return $"no result schema has a member '{name}'";
            }
            using var instance = System.Text.Json.JsonDocument.Parse(value?.ToJsonString() ?? "null");
            foreach ((string id, _, string pointer) in candidates)
            {
                string reference = new JsonObject { ["$ref"] = PublishedSchemas.UriPrefix + id + PublishedSchemas.UriSuffix + "#" + pointer }.ToJsonString();
                if (Json.Schema.JsonSchema.FromText(reference, _options).Evaluate(instance.RootElement).IsValid)
                {
                    return null;
                }
            }
            return $"'{name}' is valid against no member of that name ({string.Join(", ", candidates.Select(static member => member.Id).Distinct(StringComparer.Ordinal))})";
        }
    }

    private JsonNode Capabilities()
    {
        CliResult result = _workspace.Run("capabilities", "--output-format", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    /// <summary>Every Markdown file of every Skill in the sources.</summary>
    private static IEnumerable<string> SkillFiles() =>
        Directory.GetDirectories(Path.Combine(RepositoryPaths.Root, "src"), "Aspose.Cli*")
            .Select(static project => Path.Combine(project, "Skills"))
            .Where(Directory.Exists)
            .SelectMany(static skills => Directory.EnumerateFiles(skills, "*.md", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal);

    /// <summary>The Skill files and the README.</summary>
    private static IEnumerable<string> DocumentFiles() =>
        SkillFiles().Append(Path.Combine(RepositoryPaths.Root, "README.md"));

    /// <summary>
    /// Every fenced <c>json</c> block, and every indented code block that starts like JSON
    /// (<c>{</c>, <c>[</c> or <c>"</c>), with the line its JSON starts on.
    /// </summary>
    private static IEnumerable<(string File, int Line, string Json)> JsonBlocks()
    {
        foreach (string file in DocumentFiles())
        {
            string[] lines = File.ReadAllText(file).ReplaceLineEndings("\n").Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                string trimmed = lines[index].Trim();
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    int start = index + 1;
                    int end = start;
                    while (end < lines.Length && !lines[end].TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        end++;
                    }
                    if (trimmed == "```json")
                    {
                        yield return (file, start + 1, string.Join('\n', lines[start..end]));
                    }
                    index = end;
                }
                else if (IndentedJson().IsMatch(lines[index]) && (index == 0 || lines[index - 1].Trim().Length == 0))
                {
                    int end = index;
                    while (end < lines.Length && lines[end].StartsWith("    ", StringComparison.Ordinal))
                    {
                        end++;
                    }
                    yield return (file, index + 1, string.Join('\n', lines[index..end]));
                    index = end - 1;
                }
            }
        }
    }

    /// <summary>The first line of an indented code block that starts like JSON.</summary>
    [GeneratedRegex(@"^ {4,}[{\[""]")]
    private static partial Regex IndentedJson();
}
