using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Checks documented command paths and literal references against the real executable.</summary>
internal sealed class DocumentationCommandValidator
{
    private readonly Dictionary<string, JsonNode> _commands;
    private readonly ILookup<string, string> _children;
    private readonly HashSet<string> _schemas;

    private DocumentationCommandValidator(CliCatalog catalog)
    {
        _commands = catalog.CommandNodes.ToDictionary(
            static item => Path(item["words"]!.GetValue<string>()),
            static item => item,
            StringComparer.Ordinal);
        _children = _commands.Keys.Where(static path => path.Contains(' '))
            .ToLookup(static path => Parent(path), StringComparer.Ordinal);
        _schemas = catalog.Ids.Concat(catalog.PartIds).ToHashSet(StringComparer.Ordinal);
    }

    public static DocumentationCommandValidator Read() => new(CliCatalog.Current);

    /// <summary>A command's path from the executable, such as <c>aspose-cli cells read</c>.</summary>
    private static string Path(string words) => words.Length == 0 ? "aspose-cli" : "aspose-cli " + words;

    public void AssertMarkdown(string source, string content)
    {
        foreach (string command in CommandsInMarkdown(content))
        {
            string? problem = ValidateCommand(command);
            Assert.True(problem is null, $"{source}: {command}\n{problem}");
        }
    }

    public static IEnumerable<string> CommandsInMarkdown(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string fencePattern = @"(?m)^[ \t]*(?<fence>\x60{3}|~{3})[^\n]*\n(?<body>[\s\S]*?)^[ \t]*\k<fence>[ \t]*$";
        foreach (Match block in Regex.Matches(normalized, fencePattern, RegexOptions.CultureInvariant))
        {
            // Shell continuation markers only have meaning inside command blocks.
            string body = Regex.Replace(block.Groups["body"].Value, @"(?:\x60|\\|\^)\n[ \t]*", " ", RegexOptions.CultureInvariant);
            foreach (string line in body.Split('\n'))
            {
                string command = line.Trim();
                if (command == "aspose-cli" || command.StartsWith("aspose-cli ", StringComparison.Ordinal))
                {
                    yield return command;
                }
            }
        }

        string prose = Regex.Replace(normalized, fencePattern, "\n", RegexOptions.CultureInvariant);
        foreach (Match match in Regex.Matches(
            prose,
            @"(?<!\x60)\x60(?!\x60)(?<command>aspose-cli(?:\s+[^\x60]*)?)\x60(?!\x60)",
            RegexOptions.CultureInvariant))
        {
            yield return Regex.Replace(match.Groups["command"].Value, @"\s+", " ", RegexOptions.CultureInvariant);
        }
    }

    public string? ValidateCommand(string command)
    {
        // Keep PowerShell/POSIX single-quoted JSON and quoted option values opaque.
        string[] tokens = Regex.Matches(
                command, @"(?:[^\s""']+|""(?:\\.|[^""\\])*""|'(?:''|[^'])*')+", RegexOptions.CultureInvariant)
            .Select(static match => match.Value.Trim('"', '\''))
            .ToArray();
        if (tokens.Length == 0 || tokens[0] != "aspose-cli")
        {
            return "Expected an aspose-cli command.";
        }

        string path = "aspose-cli";
        bool positional = false;
        bool optionsEnded = false;
        for (int index = 1; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (token.StartsWith('#') || token is "|" or "||" or "&&" or ";" || IsPlaceholder(token))
            {
                return null;
            }
            if (token == "--")
            {
                optionsEnded = true;
                continue;
            }
            if (!optionsEnded && token.StartsWith('-') && token != "-")
            {
                string name = token.Split('=', 2)[0];
                JsonNode? option = FindOption(path, name);
                if (option is null)
                {
                    return $"Unknown option '{name}' for '{path}'.";
                }
                if (!token.Contains('='))
                {
                    int minimum = option["minimumArity"]!.GetValue<int>();
                    if (minimum > 0)
                    {
                        index += minimum;
                    }
                    else if (option["type"]!.GetValue<string>() == "boolean"
                        && index + 1 < tokens.Length && bool.TryParse(tokens[index + 1], out _))
                    {
                        index++;
                    }
                }
                continue;
            }

            string? child = positional ? null : _children[path].FirstOrDefault(candidate => candidate == path + " " + token);
            if (child is not null)
            {
                path = child;
                continue;
            }

            if (_commands[path]["arguments"]!.AsArray().Count == 0)
            {
                return $"Unknown subcommand '{token}' for '{path}'.";
            }

            // Teaching snippets may omit arguments or contain placeholder data.
            if (!positional && Regex.IsMatch(token, @"^[A-Za-z0-9][A-Za-z0-9_./-]*$", RegexOptions.CultureInvariant))
            {
                if (path == "aspose-cli schema" && !_schemas.Contains(token))
                {
                    return $"Unknown schema id '{token}'.";
                }
            }
            positional = true;
        }

        return null;
    }

    private JsonNode? FindOption(string path, string name)
    {
        bool inherited = false;
        while (path.Length > 0)
        {
            JsonNode? option = _commands[path]["options"]!.AsArray().FirstOrDefault(candidate =>
                candidate is not null && Named(candidate, name)
                && (!inherited || candidate["recursive"]!.GetValue<bool>()));
            if (option is not null)
            {
                return option;
            }
            inherited = true;
            path = Parent(path);
        }
        return null;
    }

    private static bool Named(JsonNode symbol, string name) =>
        symbol["name"]!.GetValue<string>() == name
        || symbol["aliases"]!.AsArray().Any(alias => alias!.GetValue<string>() == name);

    private static bool IsPlaceholder(string value) => value.StartsWith('<') || value.StartsWith('[');

    private static string Parent(string path)
    {
        int separator = path.LastIndexOf(' ');
        return separator < 0 ? string.Empty : path[..separator];
    }
}
