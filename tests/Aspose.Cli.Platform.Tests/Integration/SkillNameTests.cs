using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The names a Skill teaches exist in this build, so deleting or renaming a code, option,
/// operation or command leaves no stale reference behind. Four kinds of name are checked, each in
/// inline code outside fenced blocks: an <c>UPPER_SNAKE</c> code is a diagnostic or review check
/// that capabilities lists; a <c>--option</c> is an option of some command; a <c>snake_case</c>
/// name is an operation of some product; and a snippet that starts with a command path, such as
/// <c>cells convert data.csv --to xlsx</c>, parses against the tree as
/// <c>aspose-cli &lt;snippet&gt;</c>, or with a placeholder names only existing options. A
/// placeholder (<c>&lt;name&gt;</c>) is removed and the names around it are still checked, such as
/// the option of <c>--pages &lt;range&gt;</c>; environment variables (<c>ASPOSE_*</c>) are skipped.
/// Each kind of name is checked at least once, and every Skill set has names checked. Commands written with <c>aspose-cli</c> are already
/// checked by <see cref="SkillInstallTests"/>, and product operation documents by each product's
/// contract tests; here a fenced JSON block that names its schema, or a platform Skill operation
/// document, validates against the schema the CLI serves.
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
        string[] silent = [.. SkillFiles().Select(SkillSetOf).Distinct(StringComparer.Ordinal)
            .Where(skills => !checkedNames.Keys.Any(key => key.Skills == skills))];
        Assert.True(silent.Length == 0,
            $"The rule checks no name of the Skills of {string.Join(", ", silent)} ({counts}).");

        Assert.True(problems.Count == 0,
            "Every code, option, operation and command a Skill names exists in this build:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    private enum NameKind
    {
        Code,
        Option,
        Operation,
        Command,
    }

    /// <summary>The project whose Skills hold a file, such as <c>Aspose.Cli.Product.Pdf</c>.</summary>
    private static string SkillSetOf(string file) =>
        Path.GetRelativePath(Path.Combine(RepositoryPaths.Root, "src"), file).Split(Path.DirectorySeparatorChar)[0];

    [Fact]
    public void JsonBlocks_ThatNameASchemaOrHoldPlatformOperations_Validate()
    {
        string[] opsSchemas =
        [
            .. Capabilities()["products"]!.AsArray()
                .SelectMany(static product => product!["operations"]?.AsArray() ?? [])
                .Select(static operation => operation!["inputSchema"]!.GetValue<string>()),
        ];
        string platform = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Skills");
        int checkedBlocks = 0;
        var problems = new List<string>();
        foreach ((string file, int line, string json) in JsonBlocks())
        {
            string where = $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line}";
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json);
            }
            catch (System.Text.Json.JsonException exception)
            {
                if (json.Contains("\"schema\"", StringComparison.Ordinal) || json.Contains("\"ops\"", StringComparison.Ordinal))
                {
                    problems.Add($"{where}: does not parse ({exception.Message})");
                }
                continue;
            }
            string[] candidates = node is JsonObject root && root["schema"] is JsonValue uri && uri.TryGetValue(out string? text)
                && text.StartsWith(PublishedSchemas.UriPrefix, StringComparison.Ordinal)
                    ? [PublishedSchemas.IdOf(text)]
                    : node is JsonObject { } document && document.ContainsKey("ops") && file.StartsWith(platform, StringComparison.OrdinalIgnoreCase)
                        ? opsSchemas
                        : [];
            if (candidates.Length == 0)
            {
                continue;
            }
            checkedBlocks++;
            using var instance = System.Text.Json.JsonDocument.Parse(json);
            if (!candidates.Any(id => PublishedSchemas.Schema(id).Evaluate(instance.RootElement).IsValid))
            {
                problems.Add($"{where}: valid against none of [{string.Join(", ", candidates)}]");
            }
        }

        Assert.True(checkedBlocks > 0, "No Skill JSON block names a schema or holds platform operations; the rule checks nothing.");
        Assert.True(problems.Count == 0,
            "Every Skill JSON block that names its schema, and every platform Skill operation document, is valid against the schema the CLI serves:"
            + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private JsonNode Capabilities()
    {
        CliResult result = _workspace.Run("capabilities", "--output", "json");
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

    /// <summary>Inline code spans outside fenced blocks, with their line, other than <c>aspose-cli</c> commands.</summary>
    private static IEnumerable<(string File, int Line, string Code)> InlineCode()
    {
        foreach (string file in SkillFiles())
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

    /// <summary>Every fenced <c>json</c> block, with the line it starts on.</summary>
    private static IEnumerable<(string File, int Line, string Json)> JsonBlocks()
    {
        foreach (string file in SkillFiles())
        {
            string[] lines = File.ReadAllText(file).ReplaceLineEndings("\n").Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].Trim() != "```json")
                {
                    continue;
                }
                int start = index + 1;
                int end = start;
                while (end < lines.Length && !lines[end].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    end++;
                }
                yield return (file, start, string.Join('\n', lines[start..end]));
                index = end;
            }
        }
    }

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
