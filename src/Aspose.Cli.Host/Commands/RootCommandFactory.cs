using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Mcp;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Commands;

/// <summary>Assembles the full command tree.</summary>
internal static class RootCommandFactory
{
    /// <summary>
    /// Builds the command tree and hands back the global options, so the entry
    /// point can read <c>--output</c>/<c>--quiet</c> when rendering a parse error.
    /// </summary>
    public static RootCommand Create(
        HostContext host,
        out GlobalOptions globals)
    {
        ArgumentNullException.ThrowIfNull(host);
        var executor = new CommandExecutor(host);
        ProductCatalog catalog = host.Catalog;
        bool licensingApplicable = catalog.Products.Any(
            static product => product.Manifest.Engine.LicenseApplicable);
        globals = new GlobalOptions(licensingApplicable);

        var root = new RootCommand(
            "Aspose CLI — high-fidelity file automation for people, scripts, and AI agents. " +
            "Structured commands support --output json for stable, machine-readable results; explicit schema and docs documents remain raw.");
        globals.AddTo(root);

        Lazy<CliCapabilitySnapshot>? capabilitySnapshot = null;
        Func<CapabilitiesResult> capabilities = () =>
            (capabilitySnapshot
                ?? throw new InvalidOperationException(
                    "The CLI capability snapshot is not initialized."))
            .Value.Result;
        root.Subcommands.Add(AppCommand.Create(
            executor,
            catalog,
            host.Edition,
            capabilities,
            globals));
        root.Subcommands.Add(PreviewCommand.Create(executor, catalog, globals));
        root.Subcommands.Add(ReviewCommand.Create(
            executor,
            catalog,
            host.ContractJson.Serializer,
            globals));
        var productHostFactory = new ProductCommandHostFactory(
            executor,
            catalog,
            globals);
        foreach (ProductDefinition product in catalog.Products)
        {
            root.Subcommands.Add(product.CreateCommand(productHostFactory));
        }
        if (licensingApplicable)
        {
            root.Subcommands.Add(LicenseCommandGroup.Create(executor, catalog, globals));
        }
        capabilitySnapshot = new Lazy<CliCapabilitySnapshot>(
            () => CliCapabilitySnapshot.Create(
                root,
                catalog,
                host.Edition,
                host.Schemas));
        root.Subcommands.Add(CapabilitiesCommand.Create(
            executor,
            globals,
            capabilitySnapshot));
        root.Subcommands.Add(DoctorCommand.Create(executor, catalog, globals));
        root.Subcommands.Add(SkillCommandGroup.Create(executor, host.Skills, globals));
        root.Subcommands.Add(SchemaCommand.Create(executor, host.Schemas, globals));
        root.Subcommands.Add(DocsCommand.Create(executor, host.Docs, globals));
        if (catalog.Products.Any(
                static product => product.Manifest.Engine.SupportsFontDiagnostics))
        {
            root.Subcommands.Add(FontsCommandGroup.Create(executor, catalog, globals));
        }
        root.Subcommands.Add(McpCommand.Create(catalog));
        root.Subcommands.Add(UpdateCommand.Create(executor, globals, host.Edition));

        HostHelpMetadata.Attach(root);
        CommandHelpRenderer.Attach(root);

        return root;
    }
}
