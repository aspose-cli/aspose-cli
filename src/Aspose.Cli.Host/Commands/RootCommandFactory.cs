using System.CommandLine;
using Aspose.Cli.Host.Invocation;

namespace Aspose.Cli.Host.Commands;

/// <summary>Assembles the full command tree.</summary>
internal static class RootCommandFactory
{
    /// <summary>
    /// Builds the command tree and hands back the global options, so the entry
    /// point can read <c>--output-format</c> when rendering a parse error.
    /// </summary>
    public static RootCommand Create(
        HostContext host,
        out GlobalOptions globals)
    {
        ArgumentNullException.ThrowIfNull(host);
        var executor = new CommandExecutor(host);
        ProductCatalog catalog = host.Catalog;
        globals = new GlobalOptions();

        var root = new RootCommand(
            "Aspose CLI — high-fidelity file automation for people, scripts, and AI agents. " +
            "Structured commands print JSON contract envelopes; explicit schema and docs documents remain raw.");
        globals.AddTo(root);
        VersionOutput.Attach(root, executor, globals, catalog);

        GlobalOptions options = globals;
        foreach (ProductDefinition product in catalog.Products)
        {
            // Each product command runs in the host pipeline with that product activated.
            Command command = product.CreateCommand((parse, run) => executor.Run(parse, options, context => run(
                new ProductCommandScope(
                    context.Activate(product),
                    context.Paths,
                    context.ResourceBudgets.Inputs,
                    context.ReadEnvironment))));
            root.Subcommands.Add(command.WithInvocationPolicy(
                new CommandInvocationPolicy(ProductId: product.Manifest.Id)));
        }
        root.Subcommands.Add(LicenseCommandGroup.Create(executor, catalog, globals));
        var capabilitySnapshot = new Lazy<CliCapabilitySnapshot>(
            () => CliCapabilitySnapshot.Create(
                root,
                catalog,
                host.Schemas));
        root.Subcommands.Add(CapabilitiesCommand.Create(
            executor,
            globals,
            capabilitySnapshot));
        root.Subcommands.Add(SkillCommandGroup.Create(executor, host.Skills, globals));
        root.Subcommands.Add(SchemaCommand.Create(executor, host.Schemas, globals));
        root.Subcommands.Add(DocsCommand.Create(executor, host.Docs, globals));
        root.Subcommands.Add(FontsCommandGroup.Create(executor, catalog, globals));

        root.ValidateParameters();
        HostHelpMetadata.Attach(root, catalog);
        CommandHelpRenderer.Attach(root);

        return root;
    }
}
