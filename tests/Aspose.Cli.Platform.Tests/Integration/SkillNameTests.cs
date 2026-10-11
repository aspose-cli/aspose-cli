using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The names a Skill or the README teaches exist in this build (C-R38), so deleting or renaming a
/// code, option, operation or command leaves no stale reference behind; the C# sources are read by
/// the C-R38 invariant cases. Four kinds of name are checked, each in
/// inline code outside fenced blocks: an <c>UPPER_SNAKE</c> code is a diagnostic or review check
/// that capabilities lists; a <c>--option</c> is an option of some command; a <c>snake_case</c>
/// name is an operation of some product; and a snippet that starts with a command path, such as
/// <c>cells convert data.csv --to xlsx</c>, parses against the tree as
/// <c>aspose-cli &lt;snippet&gt;</c>, or with a placeholder names only existing options. A
/// placeholder (<c>&lt;name&gt;</c>) is removed and the names around it are still checked, such as
/// the option of <c>--pages &lt;range&gt;</c>; environment variables (<c>ASPOSE_*</c>) are skipped.
/// Each kind of name is checked at least once, and every Skill set and the README have names
/// checked. Commands written with <c>aspose-cli</c> are already checked by
/// <see cref="SkillInstallTests"/> and <see cref="DocumentationContractTests"/>. Every JSON
/// document they show validates against the schema the CLI serves (see
/// <see cref="JsonBlocks_ValidateAgainstTheSchemaTheyShow"/>).
/// </summary>
public sealed partial class SkillNameTests(DocumentationContractFixture fixture)
    : IClassFixture<DocumentationContractFixture>, IDisposable
{
    /// <summary>Snake-case words a Skill writes that are values, not operation names, with why.</summary>
    private static readonly Dictionary<string, string> NotOperations = new(StringComparer.Ordinal)
    {
        ["shift_jis"] = "a text encoding name that cells convert --encoding accepts",
    };

    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void InlineNames_ExistInThisBuild()
    {
        JsonNode capabilities = Capabilities();
        HashSet<string> codes =
        [
            .. capabilities["diagnostics"]!.AsArray().Select(static diagnostic => diagnostic!["code"]!.GetValue<string>()),
            .. capabilities["products"]!.AsArray()
                .SelectMany(static product => product!["review"]?["checks"]?.AsArray() ?? [])
                .Select(static check => check!["code"]!.GetValue<string>()),
        ];
        HashSet<string> options =
        [
            .. capabilities["commands"]!.AsArray()
                .SelectMany(static command => command!["options"]!.AsArray())
                .SelectMany(static option => option!["aliases"]!.AsArray().Select(static alias => alias!.GetValue<string>())
                    .Append(option!["name"]!.GetValue<string>())),
        ];
        HashSet<string> operations =
        [
            .. capabilities["products"]!.AsArray()
                .SelectMany(static product => product!["operations"]?.AsArray() ?? [])
                .SelectMany(static operation => operation!["ops"]!.AsArray().Select(static op => op!.GetValue<string>())),
        ];
        HashSet<string> roots =
        [
            .. capabilities["commands"]!.AsArray()
                .Select(static command => command!["path"]!.GetValue<string>().Split(' '))
                .Where(static words => words.Length == 2)
                .Select(static words => words[1]),
        ];

        var problems = new List<string>();
        // How many names of each kind each Skill set checked, so a rule that stops matching fails.
        var checkedNames = new Dictionary<(string Skills, NameKind Kind), int>();
        foreach ((string file, int line, string written) in InlineCode())
        {
            string where = $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line}: `{written}`";
            // A placeholder stands for a value the reader supplies; the names around it are still checked.
            string code = Placeholder().Replace(written, string.Empty).Trim();
            bool placeholders = code.Length != written.Trim().Length;
            NameKind? kind = null;
            if (UpperSnake().IsMatch(code))
            {
                if (code.StartsWith("ASPOSE_", StringComparison.Ordinal))
                {
                    continue;
                }
                kind = NameKind.Code;
                if (!codes.Contains(code))
                {
                    problems.Add($"{where} is no diagnostic or review check of this build");
                }
            }
            else if (OptionSnippet().Match(code) is { Success: true } option)
            {
                kind = NameKind.Option;
                if (!options.Contains(option.Groups["name"].Value))
                {
                    problems.Add($"{where} is no option of any command");
                }
            }
            else if (LowerSnake().IsMatch(code))
            {
                kind = NameKind.Operation;
                if (!operations.Contains(code) && !NotOperations.ContainsKey(code))
                {
                    problems.Add($"{where} is no operation of any product");
                }
            }
            else if (code.Split(' ', 2) is [{ } first, _] && roots.Contains(first))
            {
                kind = NameKind.Command;
                if (placeholders)
                {
                    // A command with a placeholder cannot parse; each option it names must still exist.
                    problems.AddRange(OptionToken().Matches(code).Select(static match => match.Groups["name"].Value)
                        .Where(name => !options.Contains(name))
                        .Select(name => $"{where}: {name} is no option of any command"));
                }
                else if (fixture.Validator.ValidateCommand("aspose-cli " + code) is { } problem)
                {
                    problems.Add($"{where}: {problem}");
                }
            }

            if (kind is { } counted)
            {
                (string, NameKind) key = (SkillSetOf(file), counted);
                checkedNames[key] = checkedNames.GetValueOrDefault(key) + 1;
            }
        }

        string counts = string.Join(", ", checkedNames.OrderBy(static pair => pair.Key.Skills, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.Kind)
            .Select(static pair => $"{pair.Key.Skills} {pair.Key.Kind}: {pair.Value}"));
        NameKind[] unmatched = [.. Enum.GetValues<NameKind>().Where(kind => !checkedNames.Keys.Any(key => key.Kind == kind))];
        Assert.True(unmatched.Length == 0,
            $"No Skill names a {string.Join(" or ", unmatched)} the rule recognizes, so it checks nothing; the inline-code rules stopped matching ({counts}).");
        string[] silent = [.. DocumentFiles().Select(SkillSetOf).Distinct(StringComparer.Ordinal)
            .Where(skills => !checkedNames.Keys.Any(key => key.Skills == skills))];
        Assert.True(silent.Length == 0,
            $"The rule checks no name of {string.Join(", ", silent)} ({counts}).");

        Assert.True(problems.Count == 0,
            "Every code, option, operation and command a Skill or the README names exists in this build:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    private enum NameKind
    {
        Code,
        Option,
        Operation,
        Command,
    }

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
    /// so no JSON goes unchecked. The product contract tests validate the product operation
    /// documents too, with the reasons a batch is refused.
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
        var problems = new List<string>();
        foreach ((string file, int line, string json) in JsonBlocks())
        {
            string where = $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line}";
            string? product = ProductOf(file);
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
            else if (node is JsonObject batch && (batch.ContainsKey("ops") || batch.ContainsKey("op")))
            {
                kinds.Add(batch.ContainsKey("ops") ? "operations" : "operation");
                node = batch.ContainsKey("ops") ? batch : new JsonObject { ["ops"] = new JsonArray(batch.DeepClone()) };
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

    /// <summary>Inline code spans outside fenced blocks, with their line, other than <c>aspose-cli</c> commands.</summary>
    private static IEnumerable<(string File, int Line, string Code)> InlineCode()
    {
        foreach (string file in DocumentFiles())
        {
            string[] lines = File.ReadAllText(file).ReplaceLineEndings("\n").Split('\n');
            bool fenced = false;
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                    continue;
                }
                if (fenced)
                {
                    continue;
                }
                foreach (Match span in InlineSpan().Matches(lines[index]))
                {
                    string code = span.Groups["code"].Value.Trim();
                    if (code.Length > 0 && !code.StartsWith("aspose-cli", StringComparison.Ordinal))
                    {
                        yield return (file, index + 1, code);
                    }
                }
            }
        }
    }

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

    [GeneratedRegex(@"(?<!`)`(?<code>[^`]+)`(?!`)")]
    private static partial Regex InlineSpan();

    [GeneratedRegex(@"^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$")]
    private static partial Regex UpperSnake();

    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$")]
    private static partial Regex LowerSnake();

    [GeneratedRegex(@"^(?<name>--[a-z][a-z0-9-]*)(?:[ =].*)?$")]
    private static partial Regex OptionSnippet();

    /// <summary>An option named anywhere in a snippet.</summary>
    [GeneratedRegex(@"(?<![\w-])(?<name>--[a-z][a-z0-9-]*)")]
    private static partial Regex OptionToken();

    /// <summary>A placeholder such as <c>&lt;range&gt;</c> or <c>&lt;out.pdf&gt;</c>.</summary>
    [GeneratedRegex(@"<[^<>\s][^<>]*>")]
    private static partial Regex Placeholder();
}
