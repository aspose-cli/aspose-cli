using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Mcp;

/// <summary>Hosts the bounded local MCP stdio bridge.</summary>
internal static class McpCommand
{
    public static Command Create(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var mcp = new Command("mcp", "Expose this CLI to a local MCP client over stdio.");
        var serve = new Command("serve", "Run the single-session MCP stdio server.");
        string[] productRoots = catalog.Products
            .Select(static product => product.Manifest.Id)
            .ToArray();
        serve.SetAction(_ => McpServerHost.Run(productRoots));
        mcp.Subcommands.Add(serve);
        return mcp;
    }
}
