using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;
using static Aspose.Cli.Platform.Tests.Integration.McpTestServer;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>Operation secrets and their budgets behave alike directly, under a timeout and through MCP.</summary>
public sealed class McpSecretExecutionTests
{
    [Fact]
    public async Task Timeout_InterruptsUnfinishedOpsStdinWithoutPublishingOutput()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "input.xlsx", "--sheets", "Data").ExitCode);
        byte[] original = File.ReadAllBytes(workspace.File("input.xlsx"));
        var start = new System.Diagnostics.ProcessStartInfo(CliRunner.ExecutablePath)
        {
            WorkingDirectory = workspace.Path, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        CliEnvironment.Evaluation(workspace.ConfigDirectory).Apply(start.Environment);
        foreach (string argument in new[] { "--timeout=1", "cells", "edit", "input.xlsx",
            "--ops=-", "--out", "never.xlsx", "--output=json" })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = System.Diagnostics.Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteAsync("{");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(7));
            Assert.Equal(9, process.ExitCode);
            Assert.Equal("", await stdout);
            Assert.Equal("OPERATION_TIMEOUT", JsonNode.Parse(await stderr)!["error"]!["code"]!.GetValue<string>());
            Assert.False(File.Exists(workspace.File("never.xlsx")));
            Assert.Equal(original, File.ReadAllBytes(workspace.File("input.xlsx")));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedSecret_HasTheSameBudgetFailureAcrossProcesses(bool supervised)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "input.xlsx", "--sheets", "Data").ExitCode);
        string value = new('x', 5000);
        var variables = new Dictionary<string, string?> { [Variable] = value };
        string ops = $$"""{"ops":[{"op":"protect_sheet","sheet":"Data","passwordEnv":"{{Variable}}"}]}""";
        string[] args = ["cells", "edit", "input.xlsx", "--ops", ops, "--out", "never.xlsx", "--output=json"];
        if (supervised) { args = ["--timeout=10", .. args]; }
        CliResult direct = workspace.RunWithEnv(variables, args);
        await using var server = await McpTestServer.Start(workspace.Path, workspace.Path, variables);
        JsonNode remote = (await server.Execute(args))["result"]!["structuredContent"]!;
        Assert.Equal(direct.ExitCode, remote["exitCode"]!.GetValue<int>());
        JsonNode expected = JsonNode.Parse(direct.StdErr)!["error"]!;
        JsonNode actual = JsonNode.Parse(remote["stderr"]!.GetValue<string>())!["error"]!;
        Assert.Equal("INPUT_BUDGET_EXCEEDED", actual["code"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expected["details"], actual["details"]));
        Assert.DoesNotContain(value, direct.StdErr + remote.ToJsonString(), StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("never.xlsx")));
    }

    private const string OperationSecret = "synthetic-operation-password";
    private const string Variable = "ASPOSE_TEST.Password_\u53d8\u91cf";

    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("cells", "xlsx", "file")]
    [InlineData("cells", "xlsx", "inline")]
    [InlineData("cells", "xlsx", "stdin")]
    // Every product reads --ops through the same SDK source; each product resolves its own secrets.
    [InlineData("pdf", "pdf", "inline")]
    [InlineData("words", "docx", "inline")]
    public async Task OperationSecrets_AreIdenticalForDirectTimeoutAndMcpExecution(
        string product, string extension, string source)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Environment dependency fixture");
        string input = "input." + extension;
        string[] create = product switch
        {
            "cells" => [product, "create", input, "--sheets", "Data"],
            "pdf" => [product, "create", input, "--from-text", "content.txt"],
            _ => [product, "create", input, "--text", "content.txt"],
        };
        CliResult created = workspace.Run([.. create, "--output", "json"]);
        Assert.True(created.ExitCode == 0, created.StdErr);
        string ops = product switch
        {
            "cells" => $$"""{"ops":[{"op":"protect_sheet","sheet":"Data","passwordEnv":"{{Variable}}"}]}""",
            "pdf" => $$"""{"ops":[{"op":"encrypt","ownerPasswordEnv":"{{Variable}}","userPasswordEnv":"USER_PASSWORD"}]}""",
            _ => $$"""{"ops":[{"op":"protect","mode":"readOnly","passwordEnv":"{{Variable}}"}]}""",
        };
        File.WriteAllText(workspace.File("ops.json"), ops);
        string value = source switch { "file" => "ops.json", "stdin" => "-", _ => ops };
        string? stdin = source == "stdin" ? ops : null;
        var variables = new Dictionary<string, string?>
        {
            [Variable] = OperationSecret,
            ["USER_PASSWORD"] = "synthetic-user-password",
        };
        var cli = new CliProcess(CliRunner.ExecutablePath,
            CliEnvironment.Evaluation(workspace.ConfigDirectory, variables), TimeSpan.FromSeconds(30));
        await using var server = await McpTestServer.Start(workspace.Path, workspace.Path, variables);
        for (int mode = 0; mode < 4; mode++)
        {
            string output = $"result-{mode}.{extension}";
            string[] args = [product, "edit", input, "--ops=" + value, "--out", output, "--output=json"];
            if (mode % 2 == 1) { args = ["--timeout=15", .. args]; }
            string stdout;
            string stderr;
            int exitCode;
            if (mode < 2)
            {
                CliResult result = cli.Run(workspace.Path, stdin, args);
                (exitCode, stdout, stderr) = (result.ExitCode, result.StdOut, result.StdErr);
            }
            else
            {
                JsonNode reply = await server.Execute(args, stdin);
                AssertSuccess(reply);
                JsonNode result = reply["result"]!["structuredContent"]!;
                (exitCode, stdout, stderr) = (result["exitCode"]!.GetValue<int>(),
                    result["stdout"]!.GetValue<string>(), result["stderr"]!.GetValue<string>());
            }
            Assert.True(exitCode == 0, stderr);
            Assert.DoesNotContain(OperationSecret, stdout + stderr, StringComparison.Ordinal);
            Assert.True(File.Exists(workspace.File(output)));
            if (product == "pdf")
            {
                AssertSuccess(await server.Execute(
                    ["pdf", "inspect", output, "--password-env", "USER_PASSWORD", "--output=json"]));
                JsonNode locked = await server.Execute(["pdf", "inspect", output, "--output=json"]);
                Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked["result"]!["structuredContent"]!["stderr"]!.GetValue<string>())!["error"]!["code"]!.GetValue<string>());
            }
            else
            {
                string unprotect = product == "cells"
                    ? $$"""{"ops":[{"op":"unprotect_sheet","sheet":"Data","passwordEnv":"{{Variable}}"}]}"""
                    : $$"""{"ops":[{"op":"unprotect","passwordEnv":"{{Variable}}"}]}""";
                AssertSuccess(await server.Execute([product, "edit", output, "--ops", unprotect,
                    "--out", "checked." + extension, "--dry-run", "--output=json"]));
            }
        }
    }

    [Theory]
    [InlineData("cells")]
    [InlineData("pdf")]
    [InlineData("words")]
    public void MissingSecret_FailsOnlyTheOperationThatNamesIt(string product)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Secret fixture");
        string name = "MISSING_" + Guid.NewGuid().ToString("N");
        (string extension, string[] create, string ops) = product switch
        {
            "cells" => ("xlsx", new[] { "--sheets", "Data" },
                $$"""{"ops":[{"op":"protect_sheet","sheet":"Data","passwordEnv":"{{name}}"},{"op":"set_values","sheet":"Data","range":"A1","values":[[42]]}]}"""),
            "pdf" => ("pdf", ["--from-text", "content.txt"],
                $$"""{"ops":[{"op":"encrypt","ownerPasswordEnv":"{{name}}"},{"op":"set_metadata","title":"Kept"}]}"""),
            _ => ("docx", ["--text", "content.txt"],
                $$"""{"ops":[{"op":"protect","mode":"readOnly","passwordEnv":"{{name}}"},{"op":"set_text","at":{"block":1},"text":"Kept"}]}"""),
        };
        string input = "input." + extension;
        CliResult created = workspace.Run([product, "create", input, .. create]);
        Assert.True(created.ExitCode == 0, created.StdErr);
        string[] edit = [product, "edit", input, "--ops", ops, "--out", "result." + extension, "--output=json"];

        CliResult partial = workspace.Run([.. edit, "--best-effort", "--dry-run"]);
        CliResult failed = workspace.Run(edit);

        Assert.True(partial.ExitCode == 8, partial.StdErr);
        JsonNode outcomes = JsonNode.Parse(partial.StdOut)!["applied"]!;
        Assert.Equal("failed", outcomes[0]!["status"]!.GetValue<string>());
        Assert.Contains(name, outcomes[0]!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("ok", outcomes[1]!["status"]!.GetValue<string>());
        JsonNode error = JsonNode.Parse(failed.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(name, error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("result." + extension)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CellsMissingSecret_PreservesBestEffortAndDryRunOutcomes(bool supervised)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "input.xlsx", "--sheets", "Data").ExitCode);
        string name = "MISSING_" + Guid.NewGuid().ToString("N");
        string ops = $$"""{"ops":[{"op":"protect_sheet","sheet":"Data","passwordEnv":"{{name}}"},{"op":"set_values","sheet":"Data","range":"A1","values":[[42]]}]}""";
        await using var server = await McpTestServer.Start(workspace.Path, workspace.Path);
        string[] args = ["cells", "edit", "input.xlsx", "--ops", ops, "--out", "partial.xlsx",
            "--best-effort", "--dry-run", "--output=json"];
        if (supervised) { args = ["--timeout=10", .. args]; }
        JsonNode reply = await server.Execute(args);
        JsonNode result = reply["result"]!["structuredContent"]!;
        Assert.Equal(8, result["exitCode"]!.GetValue<int>());
        JsonNode output = JsonNode.Parse(result["stdout"]!.GetValue<string>())!;
        Assert.Equal("failed", output["applied"]![0]!["status"]!.GetValue<string>());
        Assert.Equal("ok", output["applied"]![1]!["status"]!.GetValue<string>());
        Assert.Contains(name, output["applied"]![0]!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("partial.xlsx")));
    }
}
