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
            "Aspose CLI: read, edit, convert and render documents with the Aspose engines. " +
            "Every command prints JSON; 'schema' lists the commands and the documents that describe them.");
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
        root.Subcommands.Add(SkillCommandGroup.Create(executor, host.Skills, globals));
        root.Subcommands.Add(SchemaCommand.Create(
            executor,
            new Lazy<SchemaDirectory>(() => SchemaDirectory.Create(root, catalog, host.Schemas)),
            globals));
        root.Subcommands.Add(FontsCommandGroup.Create(executor, catalog, globals));

        root.ValidateParameters();
        HostHelpMetadata.Attach(root, catalog);
        CommandHelpRenderer.Attach(root);

        return root;
    }
}
