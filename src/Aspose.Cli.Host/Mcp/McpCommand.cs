using System.CommandLine;
using Aspose.Cli.Host.Invocation;

namespace Aspose.Cli.Host.Mcp;

/// <summary>Hosts the bounded local MCP stdio bridge.</summary>
internal static class McpCommand
{
    public static Command Create(HostContext host, GlobalOptions globals)
    {
        ArgumentNullException.ThrowIfNull(host);
        var mcp = new Command("mcp", "Expose this CLI to a local MCP client over stdio.");
        var serve = new Command("serve", "Run the single-session MCP stdio server.");
        serve.SetAction(parse => McpServerHost.Run(host, globals.Resolve(parse)));
        mcp.Subcommands.Add(serve);
        return mcp.WithInvocationPolicy(new CommandInvocationPolicy(Execution: CommandExecutionOwnership.Service));
    }
}
