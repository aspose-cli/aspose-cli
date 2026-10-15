using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

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

    /// <summary>
    /// The product that opens an existing file. A file the system will not let it read is the
    /// input error a product command reports for it, such as <c>FILE_LOCKED</c>.
    /// </summary>
    public static ProductDefinition ResolveExistingFile(
        this ProductCatalog catalog,
        string path,
        string? productId = null,
        string operation = "open",
        CancellationToken cancellationToken = default)
    {
        try
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (InputFailures.FromAccess(exception, Path.GetFullPath(path)) is { } access)
            {
                throw access;
            }
            throw;
        }
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
