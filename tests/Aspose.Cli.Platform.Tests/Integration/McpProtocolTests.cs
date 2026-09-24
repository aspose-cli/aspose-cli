using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;
using static Aspose.Cli.Platform.Tests.Integration.McpTestServer;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed class McpProtocolTests
{
    [Fact]
    public async Task Execute_UsesActualSyntaxPreservesHostDefaultsAndRejectsUnauthorizedCommands()
    {
        using var temp = new TempDirectory();
        string work = temp.File("work");
        Directory.CreateDirectory(work);
        File.WriteAllBytes(Path.Combine(work, "Data"), new byte[2 * 1024 * 1024]);
        File.WriteAllText(Path.Combine(work, "input.csv"), "Name,Value\nA,42\n");
        await using var server = await McpTestServer.Start(temp.Path, work);

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

    [Fact]
    public async Task Execute_InheritedExplicitLicenseKeepsItsSourceAndAnchoredPath()
    {
        using var temp = new TempDirectory();
        string work = temp.File("work");
        string other = temp.File("other");
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "input.csv"), "Name,Value\nA,42\n");
        File.WriteAllText(Path.Combine(work, "selected.lic"), "<License>synthetic invalid fixture</License>");
        // The explicit license outranks every environment source; the resolver tests cover that order.
        var variables = new Dictionary<string, string?>
        {
            ["ASPOSE_CELLS_LICENSE_B64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("synthetic environment fixture")),
        };
        await using var server = await McpTestServer.Start(temp.Path, work, variables, ["--license", "selected.lic"]);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_PreservesCorruptDocumentDiagnostics(bool supervised)
    {
        using var temp = new TempDirectory();
        string input = temp.File("malformed.docx");
        byte[] bytes = "PK\x03\x04invalid synthetic docx"u8.ToArray();
        File.WriteAllBytes(input, bytes);
        await using var server = await McpTestServer.Start(temp.Path, temp.Path);
        string[] args = ["words", "inspect", "malformed.docx", "--output", "json"];
        if (supervised) { args = ["--timeout", "15", .. args]; }

        JsonNode reply = await server.Execute(args);

        Assert.Null(reply["error"]);
        JsonObject result = Assert.IsType<JsonObject>(reply["result"]);
        // The MCP tool completed; the child's input error is carried by the execution result.
        Assert.False(result["isError"]?.GetValue<bool>() ?? false, reply.ToJsonString());
        JsonObject execution = Assert.IsType<JsonObject>(result["structuredContent"]);
        Assert.Equal(3, execution["exitCode"]!.GetValue<int>());
        Assert.Equal(string.Empty, execution["stdout"]!.GetValue<string>());
        JsonNode error = JsonNode.Parse(execution["stderr"]!.GetValue<string>())!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(error["message"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(error["hint"]!.GetValue<string>()));
        Assert.Equal(bytes, File.ReadAllBytes(input));
    }

    private static JsonNode Error(JsonNode reply)
    {
        JsonNode execution = reply["result"]!["structuredContent"]!;
        Assert.Equal(7, execution["exitCode"]!.GetValue<int>());
        return JsonNode.Parse(execution["stderr"]!.GetValue<string>())!["error"]!;
    }
}
