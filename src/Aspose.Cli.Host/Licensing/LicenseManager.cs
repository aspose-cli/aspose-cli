using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Configuration;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Licensing;

/// <summary>
/// Owns license status, installation and removal for the <c>license</c> and
/// <c>doctor</c> commands and the startup notice. The App reaches the same
/// operations through a CLI child process (<c>AppCliGateway</c>), so the
/// long-lived service never applies a license itself.
/// </summary>
internal static class LicenseManager
{
    /// <summary>Reports validation failures per product without hiding healthy products.</summary>
    internal static LicenseStatusResult Inspect(CommandContext context, string? productId = null)
    {
        ProductDefinition[] products = Select(context.Catalog, productId);
        Dictionary<string, ILicenseGate> gates = products.ToDictionary(
            product => product.Manifest.Id, product => context.Activate(product).LicenseGate, StringComparer.Ordinal);
        ProductLicenseStatus[] statuses = products.Select(product => Inspect(context, product, gates[product.Manifest.Id])).ToArray();
        string? identity = null;
        if (statuses.All(static status => status.Mode != LicenseModes.Invalid))
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (ProductDefinition product in products)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(product.Manifest.Id + "\0" + gates[product.Manifest.Id].Identity + "\0"));
            }
            identity = Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        return new LicenseStatusResult
        {
            Products = Array.AsReadOnly(statuses),
            Identity = identity,
            SharedUserLicenseInstalled = context.ProductActivation.UserLicenseChanges?.IsSharedInstalled(context.ProductActivation.ConfigDirectory)
                ?? File.Exists(LicenseResolver.SharedUserLicensePath(context.ProductActivation.ConfigDirectory)),
        };
    }

    internal static LicenseStatusResult Install(CommandContext context, string sourcePath, string? productId)
    {
        LicenseStatusResult? result = null;
        LicenseInstaller.InstallMany(context.ResourceBudgets, sourcePath, snapshot =>
        {
            CommandContext validation = CompositionRoot.Create(
                context.Catalog, context.Globals with { LicensePath = snapshot }, resourceBudgets: context.ResourceBudgets);
            (string[] compatible, string[] destinations) = SelectInstallation(context, Inspect(validation, productId));
            result = Inspect(WithChanges(context, new UserLicenseChanges(
                compatible.Select(id => KeyValuePair.Create<string, string?>(id, snapshot)))));
            return destinations;
        });
        return result!;
    }

    private static (string[] Products, string[] Destinations) SelectInstallation(CommandContext context, LicenseStatusResult validation)
    {
        string[] compatible = validation.Products.Where(static product => product.Mode == LicenseModes.Licensed)
            .Select(static product => product.Product).ToArray();
        if (compatible.Length == 0)
        {
            throw CliErrors.LicenseInvalid("file", validation.Products.FirstOrDefault()?.Problem
                ?? "the file is not valid for the selected products");
        }
        ConfigurationPaths.EnsureUserDirectory();
        return (compatible, compatible.Select(id =>
            LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, id)).ToArray());
    }

    internal static LicenseStatusResult RemoveAndReport(CommandContext context, string? productId)
    {
        bool sharedOnly = productId == "shared";
        ProductDefinition[] products = Select(context.Catalog, sharedOnly ? null : productId);
        var changes = new UserLicenseChanges(sharedOnly ? [] : products.Select(product =>
            KeyValuePair.Create<string, string?>(product.Manifest.Id, null)), productId is null || sharedOnly);
        LicenseStatusResult result = Inspect(WithChanges(context, changes));
        Remove(context, productId);
        return result;
    }

    private static CommandContext WithChanges(CommandContext context, UserLicenseChanges changes) =>
        CompositionRoot.Create(context.Catalog, context.Globals, resourceBudgets: context.ResourceBudgets,
            licenseChanges: changes);

    internal static bool Remove(CommandContext context, string? productId) =>
        LicenseInstaller.RemoveMany(context.ResourceBudgets, RemovalPaths(context, productId)).Count > 0;

    /// <summary>The saved user license files a removal for <paramref name="productId"/> deletes when they exist.</summary>
    internal static string[] RemovalPaths(CommandContext context, string? productId)
    {
        bool sharedOnly = string.Equals(productId, "shared", StringComparison.Ordinal);
        ProductDefinition[] products = Select(context.Catalog, sharedOnly ? null : productId);
        IEnumerable<string> paths = sharedOnly
            ? []
            : products.Select(product => LicenseResolver.UserLicensePath(
                context.ProductActivation.ConfigDirectory, product.Manifest.Id));
        if (productId is null || sharedOnly)
        {
            paths = paths.Prepend(LicenseResolver.SharedUserLicensePath(context.ProductActivation.ConfigDirectory));
        }
        return [.. paths];
    }

    /// <summary>
    /// The user license files an installation for <paramref name="productId"/> may write: one per
    /// selected product; the installation writes those of the products the license is valid for.
    /// </summary>
    internal static string[] InstallationPaths(CommandContext context, string? productId) =>
    [
        .. Select(context.Catalog, productId).Select(product =>
            LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, product.Manifest.Id)),
    ];

    private static ProductLicenseStatus Inspect(CommandContext context, ProductDefinition product, ILicenseGate gate)
    {
        CliException? error = null;
        string mode;
        try { mode = gate.EnsureApplied().ToContractName(); }
        catch (CliException exception) { error = exception; mode = LicenseModes.Invalid; }
        return new ProductLicenseStatus
        {
            Product = product.Manifest.Id,
            Name = product.Manifest.DisplayName,
            Mode = mode,
            Source = gate.Resolution.SourceLabel,
            Path = gate.Resolution.Path,
            Problem = error?.Message,
            Hint = error?.Hint,
            UserLicenseInstalled = context.ProductActivation.UserLicenseChanges?.IsProductInstalled(context.ProductActivation.ConfigDirectory, product.Manifest.Id)
                ?? File.Exists(LicenseResolver.UserLicensePath(context.ProductActivation.ConfigDirectory, product.Manifest.Id)),
        };
    }

    private static ProductDefinition[] Select(ProductCatalog catalog, string? productId) =>
        productId is null ? catalog.Products.ToArray() : [catalog.ResolveById(productId)];
}
