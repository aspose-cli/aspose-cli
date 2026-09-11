using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Host.Skills;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// <c>aspose-cli docs [topic]</c> — prints an offline reference document bundled with
/// the binary; with no topic it lists the available ones. Like <c>aspose-cli schema</c>,
/// the output is the raw document (the payload is the doc, not a result envelope).
/// The content is the same embedded skill <c>skill install</c> extracts, so it
/// can never drift from the shipped skill.
/// </summary>
internal static class DocsCommand
{
    public static Command Create(
        CommandExecutor executor,
        DocsCatalog catalog,
        GlobalOptions globals)
    {
        var topicArgument = new Argument<string?>("topic")
        {
            Description = "Doc topic to print; omit to list the available topics.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);

        var docs = new Command("docs", "Print an offline reference document bundled with this binary.");
        docs.Arguments.Add(topicArgument);

        docs.SetAction(parseResult => executor.RunRaw(parseResult, globals, () =>
        {
            string? topic = parseResult.GetValue(topicArgument);
            if (string.IsNullOrEmpty(topic))
            {
                return string.Join(Environment.NewLine, catalog.Topics);
            }

            return catalog.TryRead(topic, out string content)
                ? content
                : throw CliErrors.OptionInvalid(
                    "topic",
                    $"unknown docs topic '{topic}'",
                    $"Known topics: {string.Join(", ", catalog.Topics)}.");
        }));

        return docs;
    }
}
