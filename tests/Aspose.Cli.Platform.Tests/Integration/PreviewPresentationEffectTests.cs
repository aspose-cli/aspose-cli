using System.Diagnostics;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>The public demo effect is part of a session's identity.</summary>
[Collection("Local service lifecycle")]
public sealed class PreviewPresentationEffectTests
{
    [Fact]
    public async Task DemoEffect_ReachesTheBrowserAndAChangedEffectReplacesTheSession()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("book.csv"), "Label,Value\nREADY,42\n");
        var processes = new List<Process>();
        try
        {
            JsonNode plain = Start(workspace, processes, "book.csv");
            JsonNode demo = Start(workspace, processes, "book.csv", "--fx", "demo");
            Assert.False(demo["reused"]!.GetValue<bool>());
            Assert.NotEqual(plain["id"]!.GetValue<string>(), demo["id"]!.GetValue<string>());
            Assert.Equal(plain["url"]!.GetValue<string>(), demo["url"]!.GetValue<string>());

            JsonNode again = Start(workspace, processes, "book.csv", "--fx", "demo");
            Assert.True(again["reused"]!.GetValue<bool>());
            Assert.Equal(demo["id"]!.GetValue<string>(), again["id"]!.GetValue<string>());

            using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
            string script = await http.GetStringAsync(
                new Uri(new Uri(demo["url"]!.GetValue<string>()), "/live/client.js"));
            Assert.Contains("window.__asposePreviewFx=\"demo\"", script, StringComparison.Ordinal);
        }
        finally
        {
            workspace.Run("preview", "stop", "--all", "--output", "json");
            foreach (Process process in processes)
            {
                if (!process.WaitForExit(10_000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
                process.Dispose();
            }
        }
    }

    [Fact]
    public void DemoEffect_IsRejectedByAProductThatDoesNotDeclareIt()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("doc.md"), "Paragraph.");
        CliResult created = workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult rejected = workspace.Run("preview", "doc.docx", "--fx", "demo", "--output", "json");

        Assert.NotEqual(0, rejected.ExitCode);
        JsonNode error = JsonNode.Parse(rejected.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("--fx", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        CliResult status = workspace.Run("preview", "status", "--output", "json");
        Assert.Empty(JsonNode.Parse(status.StdOut)!["sessions"]!.AsArray());
    }

    private static JsonNode Start(
        TempWorkspace workspace,
        List<Process> processes,
        params string[] arguments)
    {
        CliResult result = workspace.Run(["preview", .. arguments, "--output", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode session = JsonNode.Parse(result.StdOut)!;
        int pid = session["pid"]!.GetValue<int>();
        if (!processes.Any(process => process.Id == pid))
        {
            Process process = Process.GetProcessById(pid);
            _ = process.Handle;
            processes.Add(process);
        }
        return session;
    }
}
