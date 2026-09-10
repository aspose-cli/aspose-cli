using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Licensing;

/// <summary>
/// Coordinates validation and user-level storage while product engines remain
/// responsible for interpreting license content.
/// </summary>
internal static class ProductLicenseProvisioning
{
    public static IReadOnlyList<ProductLicenseInstallation> Install(
        ProductCatalog catalog,
        string sourcePath,
        GlobalValues globals,
        string? requestedProductId = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(globals);
        EnsureApplicable(catalog, requestedProductId, "installation");

        CommandContext validation = CompositionRoot.Create(
            catalog,
            globals with { LicensePath = sourcePath });
        IReadOnlyList<ProductLicenseInspection> inspections =
            ProductLicenseInspector.Inspect(validation);
        ProductLicenseInspection[] compatible = requestedProductId is null
            ? inspections.Where(IsLicensed).ToArray()
            : [RequireLicensed(inspections, requestedProductId)];
        if (compatible.Length == 0)
        {
            ProductLicenseInspection? rejected = inspections.FirstOrDefault(
                static inspection => inspection.Error is not null);
            throw rejected?.Error
                ?? CliErrors.LicenseInvalid(
                    "file",
                    "the file is not valid for any installed product");
        }

        Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnsureUserDirectory();
        ProductLicenseInstallation[] installs = compatible
            .Select(inspection => new ProductLicenseInstallation(
                inspection.Product,
                LicenseResolver.UserLicensePath(inspection.Product.Manifest.Id)))
            .ToArray();
        LicenseInstaller.InstallMany(
            validation.ResourceBudgets,
            sourcePath,
            installs.Select(static install => install.Path));
        return installs;
    }

    public static bool RemoveProduct(ProductCatalog catalog, string productId)
    {
        EnsureApplicable(catalog, productId, "removal");
        return LicenseInstaller.Remove(LicenseResolver.UserLicensePath(productId));
    }

    public static bool RemoveShared(ProductCatalog catalog)
    {
        EnsureApplicable(catalog, requestedProductId: null, "removal");
        return LicenseInstaller.Remove(LicenseResolver.UserLicensePath());
    }

    public static bool RemoveAll(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ProductDefinition[] applicable = ApplicableProducts(catalog);
        EnsureApplicable(catalog, requestedProductId: null, "removal");
        bool removed = LicenseInstaller.Remove(LicenseResolver.UserLicensePath());
        foreach (ProductDefinition product in applicable)
        {
            removed = LicenseInstaller.Remove(
                LicenseResolver.UserLicensePath(product.Manifest.Id)) || removed;
        }

        return removed;
    }

    public static void EnsureApplicable(
        ProductCatalog catalog,
        string? requestedProductId,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ProductDefinition[] applicable = ApplicableProducts(catalog);
        if (requestedProductId is null)
        {
            if (applicable.Length == 0)
            {
                throw CliErrors.LicenseNotApplicable(
                    operation,
                    product: null,
                    available: []);
            }
            return;
        }

        ProductDefinition selected = catalog.ResolveById(requestedProductId);
        if (!selected.Manifest.Engine.LicenseApplicable)
        {
            throw CliErrors.LicenseNotApplicable(
                operation,
                selected.Manifest.Id,
                applicable.Select(static product => product.Manifest.Id).ToArray());
        }
    }

    private static ProductDefinition[] ApplicableProducts(ProductCatalog catalog) =>
        catalog.Products
            .Where(static product => product.Manifest.Engine.LicenseApplicable)
            .ToArray();

    private static bool IsLicensed(ProductLicenseInspection inspection) =>
        inspection.Error is null && inspection.State == LicenseState.Licensed;

    private static ProductLicenseInspection RequireLicensed(
        IReadOnlyList<ProductLicenseInspection> inspections,
        string productId)
    {
        ProductLicenseInspection? inspection = inspections.FirstOrDefault(item =>
            string.Equals(
                item.Product.Manifest.Id,
                productId,
                StringComparison.OrdinalIgnoreCase));
        if (inspection is null)
        {
            throw CliErrors.OptionInvalid(
                "--product",
                $"unknown product '{productId}'",
                $"Use one of: {string.Join(
                    ", ",
                    inspections.Select(
                        static item => item.Product.Manifest.Id))}.");
        }

        if (inspection.Error is not null)
        {
            throw inspection.Error;
        }

        if (inspection.State != LicenseState.Licensed)
        {
            throw CliErrors.LicenseInvalid(
                "file",
                $"the file does not license {inspection.Product.Manifest.DisplayName}");
        }

        return inspection;
    }
}

internal sealed record ProductLicenseInstallation(
    ProductDefinition Product,
    string Path);
