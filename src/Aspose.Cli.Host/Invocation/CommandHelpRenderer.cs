using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Appends command-owned examples and learning links after the standard help
/// layout. Product metadata stays in its product assembly.
/// </summary>
internal static class CommandHelpRenderer
{
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

            int exitCode = _stockHelp.Invoke(parseResult);
            WriteSections(
                parseResult.CommandResult,
                parseResult.InvocationConfiguration.Output);
            return exitCode;
        }
    }
}
