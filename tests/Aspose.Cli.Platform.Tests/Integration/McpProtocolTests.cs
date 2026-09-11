using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed partial class McpProtocolTests
{
    [Fact]
    public async Task Execute_UsesActualSyntaxPreservesHostDefaultsAndRejectsUnauthorizedCommands()
    {
        using var temp = new TempDirectory();
        string work = temp.File("work");
        Directory.CreateDirectory(work);
        File.WriteAllBytes(Path.Combine(work, "Data"), new byte[2 * 1024 * 1024]);
        File.WriteAllText(Path.Combine(work, "input.csv"), "Name,Value\nA,42\n");
        await using var server = await Server.Start(temp.Path, work);

        JsonNode tools = await server.Request("tools/list", new { });
        JsonArray listed = tools["result"]!["tools"]!.AsArray();
        Assert.Equal(2, listed.Count);
        Assert.True(listed.Single(tool => tool!["name"]!.GetValue<string>() == "capabilities")!["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.False(listed.Single(tool => tool!["name"]!.GetValue<string>() == "execute")!["annotations"]!["readOnlyHint"]!.GetValue<bool>());

        string[][] variants =
        [
            ["--max-input-bytes", "1048576", "--quiet=false", "-f", "json", "cells", "inspect", "input.csv"],
            ["--max-input-bytes=1048576", "cells", "--verbose=false", "inspect", "input.csv", "--output=json"],
            ["cells", "inspect", "input.csv", "-q=false", "-f=json", "--max-input-bytes=1048576"],
        ];
        foreach (string[] args in variants)
        {
            JsonNode result = await server.Execute(args);
            AssertSuccess(result);
            Assert.Equal(Path.Combine(work, "input.csv"),
                JsonNode.Parse(result["result"]!["structuredContent"]!["stdout"]!.GetValue<string>())!["source"]!["path"]!.GetValue<string>());
        }

        AssertSuccess(await server.Execute(["cells", "create", "book.xlsx", "--sheets", "Data", "--output=json"]));
        const string ops = """{"ops":[{"op":"set_values","sheet":"Data","range":"A1","values":[[42]]}]}""";
        AssertSuccess(await server.Execute(
            ["cells", "edit", "book.xlsx", "--ops=-", "--in-place", "--output=json"], ops));
        JsonNode queried = await server.Execute(
            ["cells", "query", "range", "book.xlsx", "--sheet", "Data", "--range", "A1", "--output=json"]);
        AssertSuccess(queried);
        Assert.Equal(42, JsonNode.Parse(queried["result"]!["structuredContent"]!["stdout"]!.GetValue<string>())!["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<int>());

        JsonNode oversized = await server.Execute(["cells", "inspect", "Data", "--output=json"]);
        Assert.Equal("FILE_TOO_LARGE", JsonNode.Parse(oversized["result"]!["structuredContent"]!["stderr"]!.GetValue<string>())!["error"]!["code"]!.GetValue<string>());

        foreach (string[] args in new string[][]
        {
            ["--quiet=false", "license", "install", "anything.lic"],
            ["--max-input-bytes=1048576", "app", "stop"],
            ["cells", "does-not-exist", "book.xlsx"],
            ["--max-input-bytes=2097152", "cells", "inspect", "input.csv"],
        })
        {
            JsonNode refused = await server.Execute(args);
            Assert.True(refused["result"]!["isError"]!.GetValue<bool>(), refused.ToJsonString());
        }
        Assert.Equal(2 * 1024 * 1024, new FileInfo(Path.Combine(work, "Data")).Length);
    }

    [Theory]
    [InlineData("ASPOSE_CELLS_LICENSE_PATH")]
    [InlineData("ASPOSE_CELLS_LICENSE_B64")]
    [InlineData("ASPOSE_LICENSE_PATH")]
    [InlineData("ASPOSE_LICENSE_B64")]
    public async Task Execute_InheritedExplicitLicenseKeepsItsSourceAndAnchoredPath(string variable)
    {
        using var temp = new TempDirectory();
        string work = temp.File("work");
        string other = temp.File("other");
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "input.csv"), "Name,Value\nA,42\n");
        File.WriteAllText(Path.Combine(work, "selected.lic"), "<License>synthetic invalid fixture</License>");
        var variables = new Dictionary<string, string?>
        {
            [variable] = variable.EndsWith("_B64", StringComparison.Ordinal)
                ? Convert.ToBase64String(Encoding.UTF8.GetBytes("synthetic environment fixture"))
                : temp.File("missing-environment.lic"),
        };
        await using var server = await Server.Start(temp.Path, work, variables, ["--license", "selected.lic"]);
        foreach (bool supervised in new[] { false, true })
        {
            string[] args = ["cells", "inspect", "input.csv", "--workdir", other, "--output=json"];
            if (supervised) { args = ["--timeout=10", .. args]; }
            JsonNode inherited = Error(await server.Execute(args));
            Assert.Equal("LICENSE_INVALID", inherited["code"]!.GetValue<string>());
            Assert.Equal("flag", inherited["details"]!["source"]!.GetValue<string>());

            JsonNode replaced = Error(await server.Execute([.. args, "--license=missing-execution.lic"]));
            Assert.Equal("LICENSE_FILE_NOT_FOUND", replaced["code"]!.GetValue<string>());
            Assert.Equal("--license", replaced["details"]!["source"]!.GetValue<string>());
            Assert.Equal(Path.Combine(other, "missing-execution.lic"), replaced["details"]!["path"]!.GetValue<string>());
        }
    }

    private static JsonNode Error(JsonNode reply)
    {
        JsonNode execution = reply["result"]!["structuredContent"]!;
        Assert.Equal(7, execution["exitCode"]!.GetValue<int>());
        return JsonNode.Parse(execution["stderr"]!.GetValue<string>())!["error"]!;
    }

    private static void AssertSuccess(JsonNode reply)
    {
        Assert.Null(reply["error"]);
        Assert.False(reply["result"]?["isError"]?.GetValue<bool>() ?? false, reply.ToJsonString());
        Assert.True(reply["result"]!["structuredContent"]!["exitCode"]!.GetValue<int>() == 0, reply.ToJsonString());
    }

    private sealed class Server : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _stderr;
        private int _id;

        private Server(Process process)
        {
            _process = process;
            _stderr = process.StandardError.ReadToEndAsync();
        }

        internal static async Task<Server> Start(string directory, string work,
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
            var server = new Server(Process.Start(start)!);
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
}
