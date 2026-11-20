using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The command documents of <c>schema</c> project the command tree and guess nothing: the commands
/// the catalog lists are the commands help reaches, and every option's allowed values are the values
/// its declaration accepts, which help shows in the option label or description. A value list inferred from an option's name (such as
/// <c>--to</c> or <c>--view</c>) instead of read from the option would differ from help.
/// </summary>
public sealed partial class SchemaDeclarationTests
{
    [Fact]
    public void CommandPaths_AreTheCommandsHelpReaches()
    {
        string[] listed =
        [
            .. CliCatalog.Current.Commands
                .Select(static command => Path(command.Words))
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
            "The schema catalog lists exactly the visible commands help reaches. Only in the catalog: ["
            + string.Join(", ", listed.Except(helped)) + "]; only in help: [" + string.Join(", ", helped.Except(listed)) + "].");
    }

    [Fact]
    public void AllowedValues_AreTheValuesHelpShows()
    {
        IReadOnlyList<JsonNode> commands = CliCatalog.Current.CommandNodes;
        HelpOutputs.Prefetch(commands.Select(static command => Path(command["words"]!.GetValue<string>()).Split(' ')[1..]));
        var problems = new List<string>();
        foreach (JsonNode command in commands)
        {
            string path = Path(command["words"]!.GetValue<string>());
            Dictionary<string, HelpValues> help = HelpOptions(Help(path));
            foreach (JsonNode? option in command["options"]!.AsArray())
            {
                string name = option!["name"]!.GetValue<string>();
                if (option["type"]!.GetValue<string>() == "boolean")
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
                    : shown.Description is { } description && declared.Length > 0
                        ? declared.All(value => NamesValue(description, value))
                        : declared.SequenceEqual(shown.Values.Order(StringComparer.Ordinal), StringComparer.Ordinal);
                if (!same)
                {
                    problems.Add($"{path} {name}: schema [{string.Join(", ", declared)}], help "
                        + (shown.Count is { } total ? $"{total} values" : $"[{string.Join(", ", shown.Values)}]"));
                }
            }
        }

        Assert.True(problems.Count == 0,
            "Every option's allowed values in its command's schema document are the values its declaration accepts, as help shows them:"
            + Environment.NewLine + string.Join(Environment.NewLine, problems.Order(StringComparer.Ordinal)));
    }

    /// <summary>A command's path from the executable, such as <c>aspose-cli cells read</c>.</summary>
    private static string Path(string words) => words.Length == 0 ? "aspose-cli" : "aspose-cli " + words;

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
    /// <summary>
    /// The values help shows for an option: listed after "Values:" or in its label, counted, or,
    /// when its description already explains each value, that description (<see cref="Description"/>).
    /// </summary>
    private sealed record HelpValues(IReadOnlyList<string> Values, int? Count, string? Description = null);

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
            else
            {
                // A long list moved out of the label is not repeated after a description that names each value.
                values = new HelpValues([], null, description);
            }
            foreach (string name in names)
            {
                options[name] = values;
            }
        }
        return options;
    }

    /// <summary>Whether <paramref name="description"/> names <paramref name="value"/> as a word of its own.</summary>
    private static bool NamesValue(string description, string value) =>
        Regex.IsMatch(description, $@"(?<![A-Za-z0-9-]){Regex.Escape(value)}(?![A-Za-z0-9-])", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^  (?<label>\S.*?)\s{2,}(?<description>.*)$")]
    private static partial Regex OptionRow();

    [GeneratedRegex("<(?<text>[^>]*)>")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"--[A-Za-z0-9][A-Za-z0-9-]*")]
    private static partial Regex OptionName();

    [GeneratedRegex(@"Values: (?<values>[^\[]+?)\.(?=\s*(\[|$))")]
    private static partial Regex ListedValues();

    [GeneratedRegex(@"Accepts (?<count>[0-9]+) values, listed in the command's document of aspose-cli schema\.")]
    private static partial Regex CountedValues();
}
