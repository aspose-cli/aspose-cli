using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The App holds no engine of its own, so a license change is not an event in
/// its life: the same process keeps answering, keeps its documents on screen,
/// and reports the new state as soon as the configuration changes.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class AppLicenseChangeTests
{
    [Fact]
    public async Task BrokenAndRepairedLicenseConfiguration_KeepsTheSameAppRunning()
    {
        using var workspace = new TempWorkspace();
        try
        {
            JsonNode started = Start(workspace);
            int pid = started["pid"]!.GetValue<int>();
            var url = new Uri(started["url"]!.GetValue<string>());
            await AssertMode(url, "pdf", "evaluation");

            string configured = workspace.File(".aspose/licenses/pdf.lic");
            Directory.CreateDirectory(Path.GetDirectoryName(configured)!);
            File.WriteAllText(configured, "<License>invalid native license fixture</License>");

            await AssertMode(url, "pdf", "invalid");
            await AssertMode(url, "words", "evaluation");
            JsonNode again = Start(workspace);
            Assert.True(again["reused"]!.GetValue<bool>());
            Assert.Equal(pid, again["pid"]!.GetValue<int>());

            File.Delete(configured);

            await AssertMode(url, "pdf", "evaluation");
            Assert.Equal(pid, Start(workspace)["pid"]!.GetValue<int>());
        }
        finally
        {
            Assert.Equal(0, workspace.Run("app", "stop", "--output", "json").ExitCode);
        }
    }

    private static JsonNode Start(TempWorkspace workspace)
    {
        CliResult result = workspace.Run("app", "--no-open", "--output", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Empty(result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    /// <summary>
    /// The App reports the CLI's own view of the licence, read in a child
    /// process and kept for a moment, so a change outside the App shows up
    /// within seconds rather than instantly.
    /// </summary>
    private static async Task AssertMode(Uri app, string product, string expected)
    {
        string mode = await ProductMode(app, product);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (mode != expected && !deadline.IsCancellationRequested)
        {
            await Task.Delay(250);
            mode = await ProductMode(app, product);
        }
        Assert.Equal(expected, mode);
    }

    private static async Task<string> ProductMode(Uri app, string product)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        JsonNode status = JsonNode.Parse(await client.GetStringAsync(new Uri(app, "/api/status")))!;
        return status["license"]!["products"]!.AsArray()
            .Single(item => item!["product"]!.GetValue<string>() == product)!["mode"]!.GetValue<string>();
    }
}
