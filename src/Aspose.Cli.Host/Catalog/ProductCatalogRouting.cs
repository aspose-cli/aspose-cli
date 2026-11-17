using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Catalog;

/// <summary>Host routing and user-facing lookup over the compiled catalog.</summary>
internal static class ProductCatalogRouting
{
    public static ProductDefinition ResolveById(
        this ProductCatalog catalog,
        string productId) =>
        catalog.TryGet(productId, out ProductDefinition? product)
            ? product!
            : throw CliErrors.OptionInvalid(
                "--product",
                $"unknown product '{productId}'");
}
