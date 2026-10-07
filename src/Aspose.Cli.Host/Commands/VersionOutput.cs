using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Extends the parser's built-in <c>--version</c> so that <c>--version --output &lt;mode&gt;</c>
/// answers with a <see cref="VersionResult"/> through the ordinary result writer. Plain
/// <c>--version</c> keeps the parser's own action and output, and every other companion token
/// keeps the parser's own usage error: the built-in validator is skipped only when the one
/// other option is <c>--output</c>.
/// </summary>
internal static class VersionOutput
{
    public static void Attach(
        RootCommand root,
        CommandExecutor executor,
        GlobalOptions globals,
        ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(catalog);

        VersionOption version = root.Options.OfType<VersionOption>().Single();
        Action<OptionResult>[] builtIn = [.. version.Validators];
        version.Validators.Clear();
        version.Validators.Add(result =>
        {
            if (!OnlyWithOutput(result, globals.Output))
            {
                foreach (Action<OptionResult> validate in builtIn)
                {
                    validate(result);
                }
            }
        });
        version.Action = new VersionAction(
            (SynchronousCommandLineAction)version.Action!,
            executor,
            globals,
            catalog);
    }

    // The parser skips the other options' validation while a terminating option such as
    // --version is present, so the --output value is checked here: an unknown mode keeps
    // the built-in usage error.
    private static bool OnlyWithOutput(OptionResult result, Option output) =>
        result.Parent is CommandResult parent
        && parent.Children.All(child => child is OptionResult { Option: var option }
            && (option == output || option is VersionOption))
        && parent.Children.OfType<OptionResult>()
            .SingleOrDefault(child => child.Option == output) is { Tokens: [{ } mode] }
        && GlobalOptions.IsOutputMode(mode.Value);

    private static VersionResult Describe(ProductCatalog catalog) => new()
    {
        CliVersion = VersionInfo.CliVersion,
        ArtifactVersion = string.Equals(
                VersionInfo.ArtifactVersion,
                VersionInfo.CliVersion,
                StringComparison.Ordinal)
            ? null
            : VersionInfo.ArtifactVersion,
        SourceRevision = VersionInfo.SourceRevision,
        BuildDirty = VersionInfo.BuildDirty,
        EnginePins = CliCapabilitySnapshot.EnginePins(
            catalog.Products.Select(catalog.GetCapabilities)),
    };

    private sealed class VersionAction(
        SynchronousCommandLineAction plain,
        CommandExecutor executor,
        GlobalOptions globals,
        ProductCatalog catalog) : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => plain.ClearsParseErrors;

        public override int Invoke(ParseResult parseResult) =>
            parseResult.GetResult(globals.Output) is null
                ? plain.Invoke(parseResult)
                : executor.RunLightweight(
                    parseResult,
                    globals,
                    (_, _) => Describe(catalog));
    }
}
