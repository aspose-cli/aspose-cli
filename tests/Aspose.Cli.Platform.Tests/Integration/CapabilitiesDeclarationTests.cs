using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// capabilities projects the command tree and guesses nothing: the commands it lists are the
/// commands help reaches, a product selection lists that product's commands under the same paths,
/// and every option's allowed values are the values its declaration accepts, which help shows in
/// the option label or description. A value list inferred from an option's name (such as
/// <c>--to</c> or <c>--view</c>) instead of read from the option would differ from help.
/// </summary>
public sealed partial class CapabilitiesDeclarationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void CommandPaths_AreTheCommandsHelpReaches()
    {
        JsonNode capabilities = Capabilities();
        string[] listed =
        [
            .. capabilities["commands"]!.AsArray()
                .Where(static command => !command!["hidden"]!.GetValue<bool>())
                .Select(static command => command!["path"]!.GetValue<string>())
                .Order(StringComparer.Ordinal),
        ];

        // Help runs once per path for the process; running the listed paths' help together first
        // only warms that cache. The walk below still reaches commands from help alone.
        HelpOutputs.Prefetch(listed.Select(static path => path.Split(' ')[1..]));
        var reached = new List<string>();
        var pending = new List<string> { "aspose-cli" };
        while (pending.Count > 0)
        {
            reached.AddRange(pending);
            pending = [.. pending.SelectMany(path => Subcommands(Help(path)).Select(child => path + " " + child))];
        }

        string[] helped = [.. reached.Order(StringComparer.Ordinal)];
        Assert.True(listed.SequenceEqual(helped, StringComparer.Ordinal),
            "capabilities lists exactly the visible commands help reaches. Only in capabilities: ["
            + string.Join(", ", listed.Except(helped)) + "]; only in help: [" + string.Join(", ", helped.Except(listed)) + "].");
    }

    [Fact]
    public void ProductSelection_ListsTheProductsCommandsUnderTheSamePaths()
    {
        JsonNode capabilities = Capabilities();
        string[] all = [.. capabilities["commands"]!.AsArray().Select(static command => command!["path"]!.GetValue<string>())];
        foreach (string product in capabilities["products"]!.AsArray().Select(static product => product!["id"]!.GetValue<string>()))
        {
            CliResult result = _workspace.Run("capabilities", product, "--output", "json");
            Assert.True(result.ExitCode == 0, result.StdErr);
            JsonNode selected = JsonNode.Parse(result.StdOut)!;
            string[] expected = [.. all.Where(path => path == "aspose-cli " + product || path.StartsWith("aspose-cli " + product + " ", StringComparison.Ordinal))];
            string[] commands = [.. selected["commands"]!.AsArray().Select(static command => command!["path"]!.GetValue<string>())];
            string[] productCommands =
            [
                .. Assert.Single(selected["products"]!.AsArray())!["commands"]!.AsArray()
                    .Select(static command => "aspose-cli " + command!["path"]!.GetValue<string>()),
            ];
            Assert.True(expected.SequenceEqual(commands, StringComparer.Ordinal),
                $"capabilities {product} lists the commands of the full tree under {product}; it lists: [{string.Join(", ", commands)}]");
            Assert.True(expected.Order(StringComparer.Ordinal).SequenceEqual(productCommands.Order(StringComparer.Ordinal), StringComparer.Ordinal),
                $"The {product} product entry names the same commands, relative to the executable: [{string.Join(", ", productCommands)}]");
        }
    }

    [Fact]
    public void AllowedValues_AreTheValuesHelpShows()
    {
        JsonNode capabilities = Capabilities();
        JsonNode[] commands = [.. capabilities["commands"]!.AsArray().Where(static command => !command!["hidden"]!.GetValue<bool>()).Select(static command => command!)];
        HelpOutputs.Prefetch(commands.Select(static command => command["path"]!.GetValue<string>().Split(' ')[1..]));
        var problems = new List<string>();
        foreach (JsonNode command in commands)
        {
            string path = command["path"]!.GetValue<string>();
            Dictionary<string, HelpValues> help = HelpOptions(Help(path));
            foreach (JsonNode? option in command["options"]!.AsArray())
            {
                string name = option!["name"]!.GetValue<string>();
                if (option["hidden"]!.GetValue<bool>() || option["type"]!.GetValue<string>() == "boolean")
                {
                    continue;
                }
                string[] declared = [.. option["allowedValues"]!.AsArray().Select(static value => value!.GetValue<string>()).Order(StringComparer.Ordinal)];
                if (!help.TryGetValue(name, out HelpValues? shown))
                {
                    problems.Add($"{path} {name}: help does not show the option");
                    continue;
                }
                bool same = shown.Count is { } count
                    ? count == declared.Length
                    : declared.SequenceEqual(shown.Values.Order(StringComparer.Ordinal), StringComparer.Ordinal);
                if (!same)
                {
                    problems.Add($"{path} {name}: capabilities [{string.Join(", ", declared)}], help "
                        + (shown.Count is { } total ? $"{total} values" : $"[{string.Join(", ", shown.Values)}]"));
                }
            }
        }

        Assert.True(problems.Count == 0,
            "Every option's allowed values in capabilities are the values its declaration accepts, as help shows them:"
            + Environment.NewLine + string.Join(Environment.NewLine, problems.Order(StringComparer.Ordinal)));
    }

    private JsonNode Capabilities()
    {
        CliResult result = _workspace.Run("capabilities", "--output", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    private static string Help(string path)
    {
        CliResult result = HelpOutputs.Of(path.Split(' ')[1..]);
        Assert.True(result.ExitCode == 0, $"{path} --help: {result.StdErr}");
        return result.StdOut.ReplaceLineEndings("\n");
    }

    private static IEnumerable<string> Section(string help, string title)
    {
        bool inside = false;
        foreach (string line in help.Split('\n'))
        {
            if (line == title + ":")
            {
                inside = true;
                continue;
            }
            if (inside && line.Length == 0)
            {
                yield break;
            }
            if (inside)
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<string> Subcommands(string help) =>
        Section(help, "Commands").Select(static line => line.TrimStart().Split([' ', ','], 2)[0]);

    /// <summary>The values help shows for an option: listed, or only counted when there are too many.</summary>
    private sealed record HelpValues(IReadOnlyList<string> Values, int? Count);

    private static Dictionary<string, HelpValues> HelpOptions(string help)
    {
        var options = new Dictionary<string, HelpValues>(StringComparer.Ordinal);
        foreach (string line in Section(help, "Options"))
        {
            Match row = OptionRow().Match(line);
            if (!row.Success)
            {
                continue;
            }
            string label = row.Groups["label"].Value;
            string description = row.Groups["description"].Value;
            Match placeholder = Placeholder().Match(label);
            string[] names = [.. OptionName().Matches(placeholder.Success ? label[..placeholder.Index] : label).Select(static name => name.Value)];
            HelpValues values = new([], null);
            if (ListedValues().Match(description) is { Success: true } listed)
            {
                values = new HelpValues([.. listed.Groups["values"].Value.Split(", ")], null);
            }
            else if (CountedValues().Match(description) is { Success: true } counted)
            {
                values = new HelpValues([], int.Parse(counted.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (placeholder.Success && names.Length > 0 && placeholder.Groups["text"].Value != names[^1].TrimStart('-'))
            {
                values = new HelpValues(placeholder.Groups["text"].Value.Split('|'), null);
            }
            foreach (string name in names)
            {
                options[name] = values;
            }
        }
        return options;
    }

    [GeneratedRegex(@"^  (?<label>\S.*?)\s{2,}(?<description>.*)$")]
    private static partial Regex OptionRow();

    [GeneratedRegex("<(?<text>[^>]*)>")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"--[A-Za-z0-9][A-Za-z0-9-]*")]
    private static partial Regex OptionName();

    [GeneratedRegex(@"Values: (?<values>[^\[]+?)\.(?=\s*(\[|$))")]
    private static partial Regex ListedValues();

    [GeneratedRegex(@"Accepts (?<count>[0-9]+) values, listed in aspose-cli capabilities\.")]
    private static partial Regex CountedValues();
}
