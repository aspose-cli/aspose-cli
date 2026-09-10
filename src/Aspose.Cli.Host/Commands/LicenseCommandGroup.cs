using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

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
        status.SetAction(parse => executor.Run(
            parse,
            globals,
            Result));
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
        };
        var install = new Command(
            "install", "Validate a license file and install it for every compatible product.");
        Option<string?> product = ProductOption(catalog,
            "Install only for this product instead of detecting compatible products.");
        install.Arguments.Add(file);
        install.Options.Add(product);
        install.SetAction(parse => executor.Run(parse, globals, context =>
        {
            string? requestedProduct = parse.GetValue(product);
            ProductLicenseProvisioning.EnsureApplicable(
                catalog,
                requestedProduct,
                "installation");
            string source = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            ProductLicenseProvisioning.Install(
                catalog,
                source,
                context.Globals,
                requestedProduct);
            return Result(CompositionRoot.Create(catalog, context.Globals));
        }));
        return install;
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
        {
            string? selected = parse.GetValue(product);
            if (selected is null)
            {
                ProductLicenseProvisioning.RemoveAll(catalog);
            }
            else
            {
                ProductLicenseProvisioning.RemoveProduct(catalog, selected);
            }

            return Result(CompositionRoot.Create(catalog, context.Globals));
        }));
        return remove;
    }

    private static LicenseStatusResult Result(CommandContext context)
    {
        IReadOnlyList<ProductLicenseInspection> inspections =
            ProductLicenseInspector.Inspect(context);
        bool applicable = inspections.Any(static inspection => inspection.Applicable);
        ProductLicenseInspection primary = inspections.FirstOrDefault(
            static inspection => inspection.Applicable) ?? inspections[0];
        return new LicenseStatusResult
        {
            Applicable = applicable,
            Mode = primary.State.ToContractName(),
            Source = primary.Resolution.SourceLabel,
            Path = primary.Resolution.Path,
            License = EnvelopeParts.License(primary.State),
            Products = inspections.Select(ToContract).ToArray(),
        };
    }

    private static ProductLicenseStatus ToContract(ProductLicenseInspection inspection) =>
        new()
        {
            Product = inspection.Product.Manifest.Id,
            Applicable = inspection.Applicable,
            Mode = inspection.State.ToContractName(),
            Source = inspection.Resolution.SourceLabel,
            Path = inspection.Resolution.Path,
            Problem = inspection.Error?.Message,
            Hint = inspection.Error?.Hint,
        };

    private static Option<string?> ProductOption(
        ProductCatalog catalog,
        string description)
    {
        var option = new Option<string?>("--product") { Description = description };
        option.AcceptOnlyFromAmong(
            catalog.Products
                .Select(static item => item.Manifest.Id)
                .ToArray());
        return option;
    }
}
