using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// C-R34: the <c>--help</c> of every visible leaf command has a
/// Description, a Usage that lists the required options, its Arguments, its own Options with the
/// global ones folded into one line, at least two Examples and Learn more, and lists the values
/// of an option once. What one command's help metadata decides is a case of that command; what
/// the help layout decides for every command (required options in Usage, the global options, a
/// value list beside a description that repeats it) is one case each. The help is read from the
/// committed help snapshots, which <c>CliContractTests</c> keeps equal to what the CLI prints.
/// </summary>
internal static partial class ContractRules
{
    private const int MinimumExamples = 2;

    private static IEnumerable<InvariantCase> R34(CliCatalog catalog)
    {
        var help = new Lazy<HelpSnapshots>(static () => new HelpSnapshots());
        CliCommand[] leaves =
        [
            .. catalog.Commands.Where(command => !command.Hidden && command.Words.Length > 0
                && !command.Subcommands.Any(name => !catalog.Command($"{command.Words} {name}").Hidden)),
        ];
        HashSet<string> global = [.. catalog.Command(string.Empty).Options.Select(static option => option.Name)];

        foreach (CliCommand command in leaves)
        {
            yield return Check("C-R34", command.Words, () => HelpSections(command, help.Value.Of(command)));
        }
        yield return Check("C-R34", "usage lists required options", () => help.Value.All(leaves)
            .SelectMany(static pair => pair.Command.Options.Where(static option => option.Required && !option.Hidden)
                .Where(option => !pair.Help.Usage.Contains(option.Name, StringComparison.Ordinal))
                .Select(option => $"{pair.Command.Words}: Usage leaves out the required {option.Name}")));
        yield return Check("C-R34", "global options folded", () => help.Value.All(leaves)
            .Select(pair => (pair.Command, Count: pair.Help.Options.Count(row => row.Names.Any(global.Contains))))
            .Where(static item => item.Count > 1)
            .Select(static item => $"{item.Command.Words}: lists {item.Count} global options one by one"));
        yield return Check("C-R34", "value lists", () => help.Value.All(leaves)
            .SelectMany(pair => pair.Help.Options.Where(static row => row.RepeatsValues)
                .Select(row => row.Names.Any(global.Contains)
                    ? $"global {row.Names[^1]}: lists its values in <{row.Placeholder}> and again in its description"
                    : $"{pair.Command.Words} {row.Names[^1]}: lists its values in <{row.Placeholder}> and again in its description"))
            .Distinct(StringComparer.Ordinal));
    }

    private static IEnumerable<string> HelpSections(CliCommand command, Help help)
    {
        if (!help.Sections.TryGetValue("Description", out List<string>? description) || description.All(string.IsNullOrWhiteSpace))
        {
            yield return "has no Description";
        }
        if (help.Usage.Length == 0)
        {
            yield return "has no Usage";
        }
        if (command.Arguments.Count > 0 && !help.Sections.ContainsKey("Arguments"))
        {
            yield return "has no Arguments";
        }
        foreach (CliOption option in command.Options.Where(static option => !option.Hidden))
        {
            if (!help.Options.Any(row => row.Names.Contains(option.Name)))
            {
                yield return $"Options leaves out {option.Name}";
            }
        }
        int examples = help.Sections.TryGetValue("Examples", out List<string>? lines)
            ? lines.Count(static line => line.TrimStart().StartsWith("aspose-cli ", StringComparison.Ordinal))
            : 0;
        if (examples < MinimumExamples)
        {
            yield return $"has {examples} example{(examples == 1 ? string.Empty : "s")}; at least {MinimumExamples}";
        }
        if (!help.Sections.TryGetValue("Learn more", out List<string>? links) || links.All(string.IsNullOrWhiteSpace))
        {
            yield return "has no Learn more";
        }
    }

    /// <summary>One row of the Options section: its names, its value placeholder and its description.</summary>
    private sealed record HelpOption(string[] Names, string? Placeholder, string Description)
    {
        /// <summary>The placeholder lists the allowed values and the description names each of them again.</summary>
        public bool RepeatsValues =>
            Placeholder?.Split('|') is { Length: > 1 } values
            && values.All(value => Regex.IsMatch(Description, $@"(?<![\w-]){Regex.Escape(value)}(?![\w-])"));
    }

    /// <summary>A help text split into its sections, each a list of its lines.</summary>
    private sealed record Help(Dictionary<string, List<string>> Sections, string Usage, IReadOnlyList<HelpOption> Options)
    {
        public static Help Parse(string text)
        {
            var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            List<string>? current = null;
            foreach (string line in text.ReplaceLineEndings("\n").Split('\n'))
            {
                if (HelpHeading().Match(line) is { Success: true } heading)
                {
                    current = sections[heading.Groups["name"].Value] = [];
                }
                else if (line.Length > 0)
                {
                    current?.Add(line);
                }
            }
            string usage = sections.TryGetValue("Usage", out List<string>? lines) ? string.Join(' ', lines.Select(static line => line.Trim())) : string.Empty;
            HelpOption[] options =
            [
                .. (sections.TryGetValue("Options", out List<string>? rows) ? rows : [])
                    .Select(static row => HelpRow().Match(row))
                    .Where(static match => match.Success)
                    .Select(static match => new HelpOption(
                        [.. match.Groups["names"].Value.Split(", ")],
                        match.Groups["placeholder"].Success ? match.Groups["placeholder"].Value : null,
                        match.Groups["description"].Value)),
            ];
            return new Help(sections, usage, options);
        }
    }

    /// <summary>
    /// The help of each command path from the snapshots in
    /// <c>tests/Aspose.Cli.Platform.Tests/Integration/Snapshots/*.help.txt</c>, where each help
    /// follows a <c>## aspose-cli &lt;path&gt; --help</c> heading.
    /// </summary>
    private sealed class HelpSnapshots
    {
        private readonly Dictionary<string, Help> _help = new(StringComparer.Ordinal);

        public HelpSnapshots()
        {
            string directory = Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Platform.Tests", "Integration", "Snapshots");
            foreach (string file in Directory.EnumerateFiles(directory, "*.help.txt"))
            {
                string? words = null;
                var text = new System.Text.StringBuilder();
                foreach (string line in File.ReadAllText(file).ReplaceLineEndings("\n").Split('\n').Append("## end"))
                {
                    if (line.StartsWith("## ", StringComparison.Ordinal))
                    {
                        if (words is not null)
                        {
                            _help[words] = Help.Parse(text.ToString());
                        }
                        words = SnapshotHeading().Match(line) is { Success: true } heading ? heading.Groups["words"].Value.Trim() : null;
                        text.Clear();
                    }
                    else
                    {
                        text.Append(line).Append('\n');
                    }
                }
            }
        }

        public Help Of(CliCommand command) =>
            _help.TryGetValue(command.Words, out Help? help)
                ? help
                : throw new InvalidOperationException($"No help snapshot holds 'aspose-cli {command.Words} --help'; run CliContractTests with ASPOSE_CLI_TEST_UPDATE_SNAPSHOTS=1.");

        public IEnumerable<(CliCommand Command, Help Help)> All(IReadOnlyList<CliCommand> commands) =>
            commands.Select(command => (command, Of(command)));
    }

    [GeneratedRegex(@"^## aspose-cli(?<words>(?: [^ ]+)*?) --help$")]
    private static partial Regex SnapshotHeading();

    [GeneratedRegex(@"^(?<name>[A-Z][A-Za-z ]*):$")]
    private static partial Regex HelpHeading();

    /// <summary>An option row: two spaces, the names, an optional placeholder and marker, then the description after two spaces.</summary>
    [GeneratedRegex(@"^  (?<names>[-/][^\s,]*(?:, [-/][^\s,]*)*)(?: <(?<placeholder>[^>]*)>)?(?: \(REQUIRED\))?(?:\s{2,}(?<description>.*))?$")]
    private static partial Regex HelpRow();
}
