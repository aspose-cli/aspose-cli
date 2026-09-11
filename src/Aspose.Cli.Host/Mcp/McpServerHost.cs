using System.ComponentModel;
using Aspose.Cli.Host.Invocation;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol;

namespace Aspose.Cli.Host.Mcp;

internal static class McpServerHost
{
    internal const string CapabilitiesToolName = "capabilities";
    internal const string ExecuteToolName = "execute";
    internal static IReadOnlyList<string> ToolNames { get; } =
        [CapabilitiesToolName, ExecuteToolName];

    public static int Run(HostContext host, GlobalValues globals)
        => RunAsync(host, globals).GetAwaiter().GetResult();

    private static async Task<int> RunAsync(
        HostContext host, GlobalValues globals)
    {
        ArgumentNullException.ThrowIfNull(host);
        var tools = new McpTools(new McpCommandRunner(host, globals));
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = Aspose.Cli.Sdk.DistributionInfo.CommandName,
                Version = Invocation.VersionInfo.CliVersion,
            },
            ToolCollection = CreateTools(tools),
        };
        await using var transport = new StdioServerTransport(options, loggerFactory: null);
        await using McpServer server = McpServer.Create(
            transport,
            options,
            loggerFactory: null,
            serviceProvider: null);
        await server.RunAsync().ConfigureAwait(false);
        return 0;
    }

    internal static McpServerPrimitiveCollection<McpServerTool> CreateTools(
        McpTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal)
        {
            McpServerTool.Create(
                (Func<CancellationToken, Task<McpExecutionResult>>)tools.CapabilitiesAsync,
                new McpServerToolCreateOptions
                {
                    Name = CapabilitiesToolName,
                    Description = "Return the compiled CLI capability snapshot as JSON.",
                    ReadOnly = true,
                    Destructive = false,
                    Idempotent = true,
                    OpenWorld = false,
                    UseStructuredContent = true,
                }),
            McpServerTool.Create(
                (Func<string[], string?, int, CancellationToken, Task<McpExecutionResult>>)
                    tools.ExecuteAsync,
                new McpServerToolCreateOptions
                {
                    Name = ExecuteToolName,
                    Description = "Run one bounded, allowlisted CLI command without a shell.",
                    ReadOnly = false,
                    Destructive = true,
                    Idempotent = false,
                    OpenWorld = false,
                    UseStructuredContent = true,
                }),
        };
    }
}

internal sealed class McpTools
{
    private readonly McpCommandRunner _runner;

    public McpTools(McpCommandRunner runner)
    {
        _runner = runner;
    }

    [Description("Return the compiled CLI capability snapshot as JSON.")]
    public Task<McpExecutionResult> CapabilitiesAsync(
        CancellationToken cancellationToken = default) =>
        AsMcpResultAsync(() => _runner.RunCapabilitiesAsync(cancellationToken));

    [Description("Run one bounded, allowlisted CLI command without a shell.")]
    public Task<McpExecutionResult> ExecuteAsync(
        [Description("CLI arguments as separate values; never a shell command string.")]
        string[] args,
        [Description("Optional bounded text written to the command's standard input.")]
        string? stdin = null,
        [Description("Command timeout from 1 to 600 seconds; defaults to 120.")]
        int timeoutSeconds = McpCommandRunner.DefaultTimeoutSeconds,
        CancellationToken cancellationToken = default) =>
        AsMcpResultAsync(
            () => _runner.RunAsync(args, stdin, timeoutSeconds, cancellationToken));

    private static async Task<McpExecutionResult> AsMcpResultAsync(
        Func<Task<McpExecutionResult>> command)
    {
        try
        {
            return await command().ConfigureAwait(false);
        }
        catch (McpCommandException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}

internal sealed record McpExecutionResult
{
    public required int ExitCode { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }
}
