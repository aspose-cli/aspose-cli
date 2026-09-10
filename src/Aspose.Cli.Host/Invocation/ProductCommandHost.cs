using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Creates host execution adapters for statically registered products.</summary>
internal sealed class ProductCommandHostFactory(
    CommandExecutor executor,
    ProductCatalog catalog,
    GlobalOptions globals)
    : IProductCommandHostFactory
{
    public IProductCommandHost<TPort> Create<TPort>(string productId)
        where TPort : class =>
        new ProductCommandHost<TPort>(executor, catalog, productId, globals);
}

/// <summary>
/// Bridges one product-owned command tree to the shared host execution
/// pipeline without exposing the host composition root to the product.
/// </summary>
internal sealed class ProductCommandHost<TPort>(
    CommandExecutor executor,
    ProductCatalog catalog,
    string productId,
    GlobalOptions globals)
    : IProductCommandHost<TPort>
    where TPort : class
{
    public bool HasCapability<TCapability>(
        ProductCapability<TCapability> slot)
        where TCapability : class =>
        catalog.HasProvider(slot);

    public int Run(
        ParseResult parseResult,
        Func<ProductCommandContext<TPort>, ResultEnvelope> handler) =>
        executor.RunProduct(parseResult, globals, productId, handler);
}
