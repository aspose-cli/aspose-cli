using System.CommandLine;
using Aspose.Cli.Host.Invocation;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// The <c>aspose-cli capabilities</c> command: runtime self-description so agents
/// can discover verbs, formats and ops instead of hard-coding them. Never
/// touches the engine, so it stays fast and license-independent.
/// </summary>
internal static class CapabilitiesCommand
{
    public static Command Create(
        CommandExecutor executor,
        GlobalOptions globals,
        Lazy<CliCapabilitySnapshot> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var capabilities = new Command(
            "capabilities",
            "Describe everything this build supports (products, verbs, formats), as data.");
        var product = new Argument<string?>("product")
        {
            Description = "Optional product id to select.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var command = new Argument<string?>("command")
        {
            Description = "Optional product-relative command path to select.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var summary = new Option<bool>("--summary")
        {
            Description = "List only each product's commands, formats and edit operation names.",
        };
        capabilities.Arguments.Add(product);
        capabilities.Arguments.Add(command);
        capabilities.Options.Add(summary);
        capabilities.Validators.Add(result =>
        {
            if (result.GetValue(summary) && result.GetResult(command) is { Tokens.Count: > 0 })
            {
                result.AddError(
                    "--summary cannot be combined with a command path; "
                    + "run 'capabilities <product> <command>' without --summary for one command.");
            }
        });

        capabilities.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) => parseResult.GetValue(summary)
                ? snapshot.Value.Summarize(parseResult.GetValue(product))
                : snapshot.Value.Select(
                    parseResult.GetValue(product),
                    parseResult.GetValue(command))));

        return capabilities;
    }
}
