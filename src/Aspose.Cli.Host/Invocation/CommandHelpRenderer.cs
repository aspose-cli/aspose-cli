using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Completes the standard help layout of every command. Below the root, the usage names the
/// required options, the Options section lists only the command's own options and the global
/// options every command takes fold into one line; then come the command-owned examples and
/// learning links. Long value lists stay out of the option column. Product metadata stays in
/// its product assembly.
/// </summary>
internal static class CommandHelpRenderer
{
    /// <summary>The longest value list, joined by <c>|</c>, that stays in an option label.</summary>
    internal const int MaximumLabelValuesLength = 40;

    /// <summary>The longest value list, joined by <c>, </c>, that a description spells out.</summary>
    private const int MaximumListedValuesLength = 160;

    /// <summary>The usage token that stands for the options the usage line does not name.</summary>
    private const string OptionsToken = " [options]";

    /// <summary>
    /// Decorates the recursive root help action, which covers every command in
    /// the assembled tree.
    /// </summary>
    public static void Attach(RootCommand root)
    {
        ArgumentNullException.ThrowIfNull(root);

        HelpOption helpOption = root.Options.OfType<HelpOption>().Single();
        helpOption.Action = new AppendingHelpAction(
            (HelpAction)helpOption.Action!);
    }

    private static void WriteSections(
        CommandResult commandResult,
        TextWriter output)
    {
        if (!commandResult.Command.TryGetHelpMetadata(
                out CommandHelpMetadata? metadata)
            || metadata is null)
        {
            return;
        }

        if (metadata.Examples.Count > 0)
        {
            output.WriteLine("Examples:");
            foreach (string example in metadata.Examples)
            {
                output.WriteLine("  " + example);
            }

            output.WriteLine();
        }

        if (metadata.LearnMore.Count > 0)
        {
            output.WriteLine("Learn more:");
            int width = metadata.LearnMore.Max(
                static link => link.Command.Length);
            foreach (CommandHelpLink link in metadata.LearnMore)
            {
                output.WriteLine(
                    "  "
                    + link.Command.PadRight(width + 2)
                    + link.Description);
            }

            output.WriteLine();
        }
    }

    private sealed class AppendingHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _stockHelp;

        public AppendingHelpAction(HelpAction stockHelp) =>
            _stockHelp = stockHelp;

        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            ArgumentNullException.ThrowIfNull(parseResult);

            InvocationConfiguration configuration = parseResult.InvocationConfiguration;
            TextWriter output = configuration.Output;
            Command command = parseResult.CommandResult.Command;
            bool folds = command is not RootCommand;
            var restore = new List<Action>();
            using var stock = new StringWriter();
            int exitCode;
            Option[] global;
            try
            {
                MoveLongValueListsToDescriptions(parseResult.CommandResult, restore);
                global = folds ? HideGlobalOptions(parseResult.CommandResult, restore) : [];
                configuration.Output = stock;
                exitCode = _stockHelp.Invoke(parseResult);
            }
            finally
            {
                configuration.Output = output;
                foreach (Action undo in restore)
                {
                    undo();
                }
            }

            output.Write(folds ? WithRequiredOptions(stock.ToString(), command) : stock.ToString());
            if (global.Length > 0)
            {
                output.WriteLine("Global options:");
                output.WriteLine(
                    $"  {string.Join(", ", global.Select(static option => string.Join('/', option.Aliases.Order(StringComparer.Ordinal).Append(option.Name))))} "
                    + $"(see {DistributionInfo.CommandName} --help)");
                output.WriteLine();
            }

            WriteSections(parseResult.CommandResult, output);
            return exitCode;
        }
    }

    /// <summary>
    /// Hides, for the duration of one help rendering, the visible recursive options of the
    /// command's ancestors, which every command takes and the root's help describes, and returns
    /// them for the one line that names them. Each change adds its undo to
    /// <paramref name="restore"/> as it is made.
    /// </summary>
    private static Option[] HideGlobalOptions(CommandResult commandResult, List<Action> restore)
    {
        Option[] global = [.. VisibleOptions(commandResult).Skip(commandResult.Command.Options.Count).Where(static option => !option.Hidden)];
        foreach (Option option in global)
        {
            option.Hidden = true;
            restore.Add(() => option.Hidden = false);
        }

        return global;
    }

    /// <summary>
    /// The stock help with each required option and its value named in the usage line, before
    /// <c>[options]</c>, which the stock usage leaves them in.
    /// </summary>
    private static string WithRequiredOptions(string help, Command command)
    {
        string required = string.Join(
            ' ',
            command.Options
                .Where(static option => option.Required && !option.Hidden)
                .Select(static option => option is Option<bool> ? option.Name : $"{option.Name} <{option.HelpName ?? option.Name.TrimStart('-')}>"));
        if (required.Length == 0)
        {
            return help;
        }

        string[] lines = help.Split(Environment.NewLine);
        int usage = Array.IndexOf(lines, "Usage:") + 1;
        if (usage == 0 || usage >= lines.Length)
        {
            throw new InvalidOperationException($"The help of '{command.Name}' has no usage line.");
        }

        string line = lines[usage];
        int at = line.IndexOf(OptionsToken, StringComparison.Ordinal);
        lines[usage] = at < 0 ? $"{line} {required}" : $"{line[..at]} {required}{line[at..]}";
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// The stock layout spells every accepted value into the option label, and the widest label
    /// sets the column for every row. For the duration of one help rendering, an option whose
    /// values exceed <see cref="MaximumLabelValuesLength"/> is labeled by its name and lists the
    /// values in its description, or points to <c>capabilities</c> when they are too many.
    /// Capabilities read the unchanged option. Each change adds its undo to
    /// <paramref name="restore"/> as it is made, so a failure part way is undone too.
    /// </summary>
    private static void MoveLongValueListsToDescriptions(CommandResult commandResult, List<Action> restore)
    {
        foreach (Option option in VisibleOptions(commandResult))
        {
            IReadOnlyList<string> values = OptionCompletions.Read(option);
            if (option.HelpName is not null
                || values.Count == 0
                || values.Sum(static value => value.Length + 1) - 1 <= MaximumLabelValuesLength)
            {
                continue;
            }

            string? description = option.Description;
            string listed = string.Join(", ", values);
            string summary = listed.Length <= MaximumListedValuesLength
                ? $"Values: {listed}."
                : $"Accepts {values.Count} values, listed in aspose-cli capabilities.";
            option.HelpName = option.Name.TrimStart('-');
            option.Description = string.IsNullOrEmpty(description) ? summary : description + " " + summary;
            restore.Add(() =>
            {
                option.HelpName = null;
                option.Description = description;
            });
        }
    }

    /// <summary>The options help shows for a command: its own, then its ancestors' recursive ones.</summary>
    private static IEnumerable<Option> VisibleOptions(CommandResult commandResult)
    {
        foreach (Option option in commandResult.Command.Options)
        {
            yield return option;
        }

        for (SymbolResult? parent = commandResult.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is CommandResult ancestor)
            {
                foreach (Option option in ancestor.Command.Options.Where(static option => option.Recursive))
                {
                    yield return option;
                }
            }
        }
    }
}
