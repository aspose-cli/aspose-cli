using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Architecture.Tests;

internal static class ProductFileRouterTestExtensions
{
    public static async ValueTask<ProductDefinition> RouteAsync(
        this ProductFileRouter router,
        string path,
        CancellationToken cancellationToken = default) =>
        (await router.ResolveAsync(
            new FileRouteRequest(path),
            cancellationToken)).Product;
}
