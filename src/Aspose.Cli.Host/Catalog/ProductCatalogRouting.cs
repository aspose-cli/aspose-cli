using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Catalog;

/// <summary>Host routing and user-facing lookup over the compiled catalog.</summary>
internal static class ProductCatalogRouting
{
    public static string DefaultProductId(this ProductCatalog catalog)
    {
        return catalog.DefaultProduct?.Manifest.Id
            ?? throw CliErrors.OptionInvalid(
                "--product",
                "this distribution has no declared default product",
                "Specify --product explicitly; catalog ordering is never used as a default.");
    }

    public static IReadOnlyList<string> DefaultOwnerFormats(
        this ProductCatalog catalog) =>
        catalog.DefaultOwnerExtensions
            .Select(static extension => extension.TrimStart('.'))
            .ToArray();

    public static ProductDefinition ResolveExistingFile(
        this ProductCatalog catalog,
        string path,
        string? productId = null,
        string operation = "open",
        CancellationToken cancellationToken = default)
    {
        return new ProductFileRouter(catalog)
            .ResolveAsync(
                new FileRouteRequest(path)
                {
                    ExplicitProductId = productId,
                    Operation = operation,
                },
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult()
            .Product;
    }

    public static ProductDefinition ResolveById(
        this ProductCatalog catalog,
        string productId) =>
        catalog.TryGet(productId, out ProductDefinition? product)
            ? product!
            : throw CliErrors.OptionInvalid(
                "--product",
                $"unknown product '{productId}'",
                $"Use one of: {string.Join(
                    ", ",
                catalog.Products.Select(
                        static item => item.Manifest.Id))}.");
}
