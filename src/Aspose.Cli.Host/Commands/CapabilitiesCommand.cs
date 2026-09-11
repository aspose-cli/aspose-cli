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
        capabilities.Arguments.Add(product);
        capabilities.Arguments.Add(command);

        capabilities.SetAction(parseResult => executor.RunLightweight(
            parseResult,
            globals,
            (_, _) => snapshot.Value.Select(
                parseResult.GetValue(product),
                parseResult.GetValue(command))));

        return capabilities;
    }
}
