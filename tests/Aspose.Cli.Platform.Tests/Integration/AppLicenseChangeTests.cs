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
            using HttpClient app = await LocalServiceEntry.Client(started["url"]!.GetValue<string>(), TestDeadline.Hang);
            await AssertMode(app, "pdf", "evaluation");

            string configured = workspace.File(".aspose/licenses/pdf.lic");
            Directory.CreateDirectory(Path.GetDirectoryName(configured)!);
            File.WriteAllText(configured, "<License>invalid native license fixture</License>");

            await AssertMode(app, "pdf", "invalid");
            await AssertMode(app, "words", "evaluation");
            JsonNode again = Start(workspace);
            Assert.True(again["reused"]!.GetValue<bool>());
            Assert.Equal(pid, again["pid"]!.GetValue<int>());

            File.Delete(configured);

            await AssertMode(app, "pdf", "evaluation");
            Assert.Equal(pid, Start(workspace)["pid"]!.GetValue<int>());
        }
        finally
        {
            Assert.Equal(0, workspace.Run("app", "stop", "--output-format", "json").ExitCode);
        }
    }

    // The service outlives the shell that started it: the App answers for the shell whose
    // 'app open' reached it last, its license environment and its working directory.
    [Fact]
    public async Task AppOpen_FromAnotherShell_ReportsThatShellsLicense()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("invalid.lic"), "<License>invalid native license fixture</License>");
        string project = workspace.File("project");
        Directory.CreateDirectory(Path.Combine(project, ".aspose", "licenses"));
        File.WriteAllText(
            Path.Combine(project, ".aspose", "licenses", "pdf.lic"),
            "<License>invalid native license fixture</License>");
        try
        {
            JsonNode started = Start(workspace);
            using HttpClient app = await LocalServiceEntry.Client(started["url"]!.GetValue<string>(), TestDeadline.Hang);
            await AssertMode(app, "pdf", "evaluation");

            JsonNode licensed = Start(workspace, InvalidLicenseShell(workspace));
            Assert.Equal(started["pid"]!.GetValue<int>(), licensed["pid"]!.GetValue<int>());
            await AssertMode(app, "pdf", "invalid", RequesterChange);

            Start(workspace);
            await AssertMode(app, "pdf", "evaluation", RequesterChange);

            Start(workspace, variables: null, "--workdir", project);
            await AssertMode(app, "pdf", "invalid", RequesterChange);
            await AssertMode(app, "words", "evaluation", RequesterChange);
        }
        finally
        {
            Assert.Equal(0, workspace.Run("app", "stop", "--output-format", "json").ExitCode);
        }
    }

    [Fact]
    public async Task AppOpen_FromAShellWithoutALicense_ReportsEvaluationInAServiceALicensedShellStarted()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("invalid.lic"), "<License>invalid native license fixture</License>");
        try
        {
            JsonNode started = Start(workspace, InvalidLicenseShell(workspace));
            using HttpClient app = await LocalServiceEntry.Client(started["url"]!.GetValue<string>(), TestDeadline.Hang);
            await AssertMode(app, "pdf", "invalid");

            Start(workspace);
            await AssertMode(app, "pdf", "evaluation", RequesterChange);
        }
        finally
        {
            Assert.Equal(0, workspace.Run("app", "stop", "--output-format", "json").ExitCode);
        }
    }

    // A document the App opens renders under the license of the shell that opened it.
    [Fact]
    public void AppOpenFile_FromAShellWithARefusedLicense_FailsThatDocument()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("invalid.lic"), "<License>invalid native license fixture</License>");
        File.WriteAllText(workspace.File("first.csv"), "Label,Value\nFIRST_DOCUMENT,42\n");
        try
        {
            Start(workspace);

            (int exit, string output, string error) = RunShell(
                workspace, InvalidLicenseShell(workspace),
                "app", "open", workspace.File("first.csv"), "--no-open", "--output-format", "json");

            Assert.NotEqual(0, exit);
            Assert.True(string.IsNullOrWhiteSpace(output), output);
            Assert.Equal("LICENSE_INVALID", JsonNode.Parse(error)!["error"]!["code"]!.GetValue<string>());

            JsonNode opened = Start(workspace, variables: null, workspace.File("first.csv"));
            Assert.Equal("first.csv", opened["file"]!.GetValue<string>());
        }
        finally
        {
            Assert.Equal(0, workspace.Run("app", "stop", "--output-format", "json").ExitCode);
        }
    }

    /// <summary>A requester change is answered at once; the wait only absorbs status polling.</summary>
    private static readonly TimeSpan RequesterChange = TimeSpan.FromSeconds(30);

    private static Dictionary<string, string?> InvalidLicenseShell(TempWorkspace workspace) =>
        new() { ["ASPOSE_LICENSE_PATH"] = workspace.File("invalid.lic") };

    private static JsonNode Start(TempWorkspace workspace) => Start(workspace, variables: null);

    private static JsonNode Start(
        TempWorkspace workspace, IReadOnlyDictionary<string, string?>? variables, params string[] extra)
    {
        string[] args = ["app", "open", "--no-open", "--output-format", "json", .. extra];
        if (variables is null)
        {
            CliResult result = workspace.Run(args);
            Assert.True(result.ExitCode == 0, result.StdErr);
            Assert.Empty(result.StdErr);
            return JsonNode.Parse(result.StdOut)!;
        }

        (int exit, string output, string error) = RunShell(workspace, variables, args);
        Assert.True(exit == 0, error);
        Assert.Empty(error);
        return JsonNode.Parse(output)!;
    }

    // The workspace runs strip license sources; a shell that has one sets it after them.
    private static (int Exit, string Output, string Error) RunShell(
        TempWorkspace workspace, IReadOnlyDictionary<string, string?> variables, params string[] args)
    {
        System.Diagnostics.ProcessStartInfo start = workspace.StartInfo();
        foreach ((string name, string? value) in variables) { start.Environment[name] = value; }
        start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardOutputEncoding = start.StandardErrorEncoding = System.Text.Encoding.UTF8;
        foreach (string arg in args) { start.ArgumentList.Add(arg); }
        using var process = System.Diagnostics.Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"aspose-cli {string.Join(' ', args)} did not finish.");
        }
        return (process.ExitCode, output.Result, error.Result);
    }

    /// <summary>
    /// The App, through a client that entered it once by the address <c>app open</c> returned,
    /// reports the CLI's own view of the licence, read in a child
    /// process and kept for a moment, so a change outside the App shows up
    /// within seconds rather than instantly.
    /// </summary>
    private static async Task AssertMode(HttpClient app, string product, string expected, TimeSpan? within = null)
    {
        string mode = await ProductMode(app, product);
        using var deadline = new CancellationTokenSource(within ?? TestDeadline.Hang);
        while (mode != expected && !deadline.IsCancellationRequested)
        {
            await Task.Delay(250);
            mode = await ProductMode(app, product);
        }
        Assert.Equal(expected, mode);
    }

    private static async Task<string> ProductMode(HttpClient app, string product)
    {
        JsonNode status = JsonNode.Parse(await app.GetStringAsync("/api/status"))!;
        return status["license"]!["products"]!.AsArray()
            .Single(item => item!["product"]!.GetValue<string>() == product)!["mode"]!.GetValue<string>();
    }
}
