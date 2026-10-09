using System.CommandLine;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Ports;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// The <c>aspose-cli fonts</c> command group: diagnose the font environment that
/// drives rendering fidelity. <c>list</c> reports what the engine can see;
/// <c>check &lt;file&gt;</c> routes by product and reports whether its fonts are available here and
/// what they will be substituted with — the "renders wrong on the server"
/// diagnostic that a product listing of the used fonts cannot give.
/// </summary>
internal static class FontsCommandGroup
{
    public static Command Create(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var fonts = new Command("fonts", "Diagnose the font environment used for rendering.");
        fonts.Subcommands.Add(CreateList(executor, catalog, globals));
        fonts.Subcommands.Add(CreateCheck(executor, catalog, globals));
        return fonts;
    }

    private static Command CreateList(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        ProductDefinition[] providers = Providers(catalog);
        string? defaultProduct = providers.FirstOrDefault(product =>
                product.Manifest.IsDefaultCandidate)?.Manifest.Id
            ?? providers.FirstOrDefault()?.Manifest.Id;
        var product = new Option<string>("--product")
        {
            Required = defaultProduct is null,
            Description = defaultProduct is null
                ? "Product font engine to inspect. Required because this distribution has no declared default."
                : "Product font engine to inspect.",
        }.WithInput(InputKind.None);
        if (defaultProduct is not null)
        {
            product.DefaultValueFactory = _ => defaultProduct;
        }
        product.AcceptOnlyFromAmong(
            providers
                .Select(static item => item.Manifest.Id)
                .ToArray());
        var list = new Command("list", "List the engine's font sources and its default fallback font.");
        list.Options.Add(product);

        list.SetAction(parseResult => executor.Run(parseResult, globals, context =>
        {
            string selected = parseResult.GetValue(product)!;
            ProductDefinition definition =
                catalog.ResolveById(selected);
            return context.Activate(definition).FontEnvironment!.ListFonts();
        }));

        return list.WithInvocationPolicy(new CommandInvocationPolicy(McpAllowed: true));
    }

    private static Command CreateCheck(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var standard = new StandardOptions(new CommandTraits
        {
            Input = new InputDocument("Supported document whose fonts to check.", "the document"),
            UsesFonts = true,
        });
        Command check = standard.CreateCommand(
            "check", "Check whether a document's fonts are available here, and what they substitute to.", []);

        check.SetAction(parseResult => executor.Run(parseResult, globals, context =>
        {
            StandardInvocation invocation = standard.Bind(
                parseResult, context.Paths, context.ResourceBudgets.Inputs, context.ReadEnvironment);
            string path = invocation.Input;
            ProductDefinition product = catalog.ResolveExistingFile(
                path,
                operation: "fonts",
                cancellationToken: context.Deadline.Token);
            if (!product.Manifest.Engine.SupportsFontDiagnostics)
            {
                throw CliErrors.FeatureUnsupported(
                    "font diagnostics",
                    product.Manifest.Id,
                    Providers(catalog)
                        .Select(static provider => provider.Manifest.Id)
                        .ToArray());
            }
            ProductBinding binding = context.Activate(product);
            using IDisposable fontScope = FontProfiles.Use(
                catalog, product, binding, invocation.FontDirectories);
            return binding.FontEnvironment!.CheckFonts(
                path,
                new FontCheckRequest
                {
                    Password = invocation.InputPassword,
                });
        }));

        return check.WithInvocationPolicy(new CommandInvocationPolicy(McpAllowed: true));
    }

    private static ProductDefinition[] Providers(ProductCatalog catalog) =>
        catalog.Products
            .Where(static product => product.Manifest.Engine.SupportsFontDiagnostics)
            .ToArray();
}
