using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>A real <c>mcp serve</c> process that tests drive over its standard streams.</summary>
internal sealed class McpTestServer : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stderr;
    private int _id;

    internal Task<string> StandardError => _stderr;

    private McpTestServer(Process process)
    {
        _process = process;
        _stderr = process.StandardError.ReadToEndAsync();
    }

    internal static async Task<McpTestServer> Start(string directory, string work,
        IReadOnlyDictionary<string, string?>? variables = null, string[]? options = null)
    {
        var start = new ProcessStartInfo(CliRunner.ExecutablePath)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        CliEnvironment.Evaluation(Path.Combine(directory, "config")).Apply(start.Environment);
        foreach (string argument in new[] { "--workdir", work, "--max-input-bytes", "1048576", "mcp", "serve" })
        {
            start.ArgumentList.Add(argument);
        }
        foreach (var (name, value) in variables ?? new Dictionary<string, string?>())
        {
            start.Environment[name] = value;
        }
        foreach (string argument in options ?? []) { start.ArgumentList.Add(argument); }
        var server = new McpTestServer(Process.Start(start)!);
        try
        {
            JsonNode initialized = await server.Request("initialize", new
            {
                protocolVersion = "2025-03-26", capabilities = new { },
                clientInfo = new { name = "repository-regression", version = "1.0" },
            });
            Assert.Null(initialized["error"]);
            await server._process.StandardInput.WriteLineAsync(
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await server._process.StandardInput.FlushAsync();
            return server;
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    /// <summary>Asserts that the <c>execute</c> tool ran a command that exited with zero.</summary>
    internal static void AssertSuccess(JsonNode reply)
    {
        Assert.Null(reply["error"]);
        Assert.False(reply["result"]?["isError"]?.GetValue<bool>() ?? false, reply.ToJsonString());
        Assert.True(reply["result"]!["structuredContent"]!["exitCode"]!.GetValue<int>() == 0, reply.ToJsonString());
    }

    internal Task<JsonNode> Execute(string[] args, string? stdin = null) =>
        Request("tools/call", new { name = "execute", arguments = new { args, stdin, timeoutSeconds = 30 } });

    internal async Task<JsonNode> Request(string method, object parameters)
    {
        int id = ++_id;
        await _process.StandardInput.WriteLineAsync(
            JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
        await _process.StandardInput.FlushAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (true)
        {
            string? line = await _process.StandardOutput.ReadLineAsync(timeout.Token);
            Assert.NotNull(line);
            JsonNode reply = JsonNode.Parse(line)!;
            if (reply["id"]?.GetValue<int>() == id) { return reply; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _process.StandardInput.Close();
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        finally
        {
            await _stderr;
            _process.Dispose();
        }
    }
}
