using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Appends command-owned examples and learning links after the standard help
/// layout, and keeps long value lists out of the option column. Product
/// metadata stays in its product assembly.
/// </summary>
internal static class CommandHelpRenderer
{
    /// <summary>The longest value list, joined by <c>|</c>, that stays in an option label.</summary>
    internal const int MaximumLabelValuesLength = 40;

    /// <summary>The longest value list, joined by <c>, </c>, that a description spells out.</summary>
    private const int MaximumListedValuesLength = 160;

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

            var restore = new List<Action>();
            int exitCode;
            try
            {
                MoveLongValueListsToDescriptions(parseResult.CommandResult, restore);
                exitCode = _stockHelp.Invoke(parseResult);
            }
            finally
            {
                foreach (Action undo in restore)
                {
                    undo();
                }
            }

            WriteSections(
                parseResult.CommandResult,
                parseResult.InvocationConfiguration.Output);
            return exitCode;
        }
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

    /// <summary>The options help shows for a command: its own and its ancestors' recursive ones.</summary>
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
