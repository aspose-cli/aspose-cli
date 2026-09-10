using System.CommandLine;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Ports;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// The <c>aspose-cli fonts</c> command group: diagnose the font environment that
/// drives rendering fidelity. <c>list</c> reports what the engine can see;
/// <c>check &lt;file&gt;</c> routes by product and reports whether its fonts are available here and
/// what they will be substituted with — the P-5 "renders wrong on the server"
/// diagnostic that <c>info --detail fonts</c> cannot give.
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
                : $"Product font engine to inspect. Default: {defaultProduct}.",
        };
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

        return list;
    }

    private static Command CreateCheck(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "Supported document whose fonts to check.",
        };

        var password = new PasswordOptions("--password", "the document");
        var fontDirectories = new FontDirectoryOptions();

        var check = new Command(
            "check", "Check whether a document's fonts are available here, and what they substitute to.");
        check.Arguments.Add(fileArgument);
        password.AddTo(check);
        fontDirectories.AddTo(check);

        check.SetAction(parseResult => executor.Run(parseResult, globals, context =>
        {
            string path = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
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
            IFontEnvironment environment = context.Activate(product).FontEnvironment!;
            Aspose.Cli.Sdk.Rendering.FontSearchProfile profile =
                fontDirectories.Read(parseResult);
            if (!profile.IsAmbient
                && !product.Manifest.Engine.SupportsExplicitFontProfiles)
            {
                throw CliErrors.FeatureUnsupported(
                    "explicit font profiles",
                    product.Manifest.Id,
                    Providers(catalog)
                        .Where(static provider => provider.Manifest.Engine
                            .SupportsExplicitFontProfiles)
                        .Select(static provider => provider.Manifest.Id)
                        .ToArray());
            }
            return environment.CheckFonts(
                path,
                new FontCheckRequest
                {
                    Password = password.Resolve(
                        parseResult,
                        context.ResourceBudgets.Inputs),
                    FontProfile = profile,
                });
        }));

        return check;
    }

    private static ProductDefinition[] Providers(ProductCatalog catalog) =>
        catalog.Products
            .Where(static product => product.Manifest.Engine.SupportsFontDiagnostics)
            .ToArray();
}
