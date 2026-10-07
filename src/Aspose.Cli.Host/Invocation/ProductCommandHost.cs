using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Creates host execution adapters for statically registered products.</summary>
internal sealed class ProductCommandHostFactory(
    CommandExecutor executor,
    GlobalOptions globals)
    : IProductCommandHostFactory
{
    public IProductCommandHost<TPort> Create<TPort>(string productId)
        where TPort : class =>
        new ProductCommandHost<TPort>(executor, productId, globals);
}

/// <summary>
/// Bridges one product-owned command tree to the shared host execution
/// pipeline without exposing the host composition root to the product.
/// </summary>
internal sealed class ProductCommandHost<TPort>(
    CommandExecutor executor,
    string productId,
    GlobalOptions globals)
    : IProductCommandHost<TPort>
    where TPort : class
{
    public int Run(
        ParseResult parseResult,
        Func<ProductCommandContext<TPort>, ResultEnvelope> handler) =>
        executor.RunProduct(parseResult, globals, productId, handler);
}
