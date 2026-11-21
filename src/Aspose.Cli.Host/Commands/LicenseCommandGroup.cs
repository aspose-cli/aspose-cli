using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;

namespace Aspose.Cli.Host.Commands;

/// <summary>The <c>aspose-cli license</c> command group.</summary>
internal static class LicenseCommandGroup
{
    public static Command Create(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var license = new Command("license", "Inspect and manage the Aspose license used by the CLI.");
        license.Subcommands.Add(CreateStatus(executor, catalog, globals));
        license.Subcommands.Add(CreateInstall(executor, catalog, globals));
        license.Subcommands.Add(CreateRemove(executor, catalog, globals));
        return license;
    }

    private static Command CreateStatus(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var status = new Command(
            "status",
            "Show whether licensing applies and which license source and mode are in effect.");
        Option<string?> product = ProductOption(catalog, "Inspect only this product.");
        status.Options.Add(product);
        status.SetAction(parse => executor.Run(
            parse,
            globals,
            context => LicenseManager.Inspect(context, parse.GetValue(product))));
        return status;
    }

    private static Command CreateInstall(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var file = new Argument<string>("file")
        {
            Description = "Path to an Aspose license file (.lic) to install as this user's default.",
        }.WithInput(InputKind.None);
        var install = new Command(
            "install", "Validate a license file and install it for every compatible product.");
        Option<string?> product = ProductOption(catalog,
            "Install only for this product instead of detecting compatible products.");
        install.Arguments.Add(file);
        install.Options.Add(product);
        install.SetAction(parse => executor.Run(parse, globals, context =>
            LicenseManager.Install(context, context.Paths.ResolveInput(parse.GetRequiredValue(file), file.Name), parse.GetValue(product))));
        return install.WithInvocationPolicy(new CommandInvocationPolicy(RefusesEvaluationRequest: true));
    }

    private static Command CreateRemove(
        CommandExecutor executor,
        ProductCatalog catalog,
        GlobalOptions globals)
    {
        var remove = new Command(
            "remove", "Remove saved user licenses; env and project sources are left untouched.");
        Option<string?> product = ProductOption(catalog,
            "Remove only this product's saved user license.");
        remove.Options.Add(product);
        remove.SetAction(parse => executor.Run(parse, globals, context =>
            LicenseManager.RemoveAndReport(context, parse.GetValue(product))));
        return remove.WithInvocationPolicy(new CommandInvocationPolicy(RefusesEvaluationRequest: true));
    }

    private static Option<string?> ProductOption(
        ProductCatalog catalog,
        string description)
    {
        var option = new Option<string?>("--product") { Description = description }.WithInput(InputKind.None);
        option.AcceptOnlyFromAmong(
            catalog.Products
                .Select(static item => item.Manifest.Id)
                .ToArray());
        return option;
    }
}
