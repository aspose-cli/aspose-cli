using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// The <c>aspose-cli fonts</c> command group: <c>list</c> reports the font sources each engine
/// can see.
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
        return fonts;
    }

    private static Command CreateList(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        IReadOnlyList<ProductDefinition> providers = catalog.Products;
        var product = new Option<string?>("--product")
        {
            Description = "Product whose font engine to list; every product's when omitted.",
        }.WithInput(InputKind.None);
        product.AcceptOnlyFromAmong(
            providers
                .Select(static item => item.Manifest.Id)
                .ToArray());
        var list = new Command("list", "List each engine's font sources and its default fallback font.");
        list.Options.Add(product);

        list.SetAction(parseResult => executor.Run(parseResult, globals, context =>
        {
            string? selected = parseResult.GetValue(product);
            return new FontListResult
            {
                Products =
                [
                    .. providers
                        .Where(definition => selected is null || definition.Manifest.Id == selected)
                        .Select(definition => Fonts(definition.Manifest.Id, context.Activate(definition).FontEnvironment.ListFonts())),
                ],
            };
        }));

        return list;
    }

    private static ProductFonts Fonts(string product, EngineFonts engine) => new()
    {
        Product = product,
        DefaultFont = engine.DefaultFont,
        Sources = engine.Sources,
    };
}
