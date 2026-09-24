using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Updating;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>How a detached installer run is reported to later update commands.</summary>
public sealed class UpdateClientStatusTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void MissingOrSucceededRun_ProducesNoWarning()
    {
        string path = _directory.File("status.json");
        Assert.Null(UpdateStatus.ReadWarning(path));
        Write(path, new JsonObject { ["state"] = "succeeded", ["installerProcessId"] = 1 });
        Assert.Null(UpdateStatus.ReadWarning(path));
    }

    [Fact]
    public void FailedRun_ReportsTheInstallerReasonAndLog()
    {
        string path = _directory.File("status.json");
        Write(path, new JsonObject
        {
            ["state"] = "failed",
            ["targetVersion"] = "1.2.0",
            ["message"] = "Directory publication remained blocked",
            ["log"] = @"C:\temp\status.log",
        });

        Warning warning = UpdateStatus.ReadWarning(path)!;

        Assert.Equal(UpdateStatus.FailedWarningCode, warning.Code);
        Assert.Contains("1.2.0", warning.Message, StringComparison.Ordinal);
        Assert.Contains("Directory publication remained blocked", warning.Message, StringComparison.Ordinal);
        Assert.Contains(@"C:\temp\status.log", warning.Hint, StringComparison.Ordinal);
        Assert.Contains("mcp serve", warning.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingRunWhoseInstallerIsGone_IsReportedAsInterrupted()
    {
        string path = _directory.File("status.json");
        UpdateStatus.WritePending(path, ExitedProcessId(), "1.0.0", "1.1.0");

        JsonObject written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("pending", written["state"]!.GetValue<string>());
        Assert.Equal("1.0.0", written["currentVersion"]!.GetValue<string>());
        Assert.Equal(Path.ChangeExtension(path, ".log"), written["log"]!.GetValue<string>());

        Warning warning = UpdateStatus.ReadWarning(path)!;
        Assert.Equal(UpdateStatus.FailedWarningCode, warning.Code);
        Assert.Contains("stopped before", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunningInstaller_IsReportedAsInProgress()
    {
        Requires.Windows();
        using Process installer = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            "-NoLogo -NoProfile -NonInteractive -Command Start-Sleep -Seconds 60")
        { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            string path = _directory.File("status.json");
            Write(path, new JsonObject { ["state"] = "running", ["installerProcessId"] = installer.Id });
            Assert.Equal(UpdateStatus.InProgressWarningCode, UpdateStatus.ReadWarning(path)!.Code);
        }
        finally { installer.Kill(entireProcessTree: true); }
    }

    [Fact]
    public void StatusFilesAreKeyedByOneSpellingOfTheInstallDirectory()
    {
        string root = _directory.File("install");
        Assert.Equal(UpdateStatus.PathFor(root), UpdateStatus.PathFor(root + Path.DirectorySeparatorChar));
        Assert.NotEqual(UpdateStatus.PathFor(root), UpdateStatus.PathFor(root + "-other"));
    }

    [Fact]
    public void UnreachableHttpsFeed_IsAnActionableNetworkError()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var workspace = new TempWorkspace();

        CliResult result = workspace.Run(
            "update", "check", $"https://127.0.0.1:{port}/RELEASE-MANIFEST.json", "--output", "json");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEqual(130, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Contains("could not be reached", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("RELEASE_FEED_UNAVAILABLE", error["code"]!.GetValue<string>());
        Assert.Contains("proxy", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    private static void Write(string path, JsonObject status) => File.WriteAllText(path, status.ToJsonString());

    private static int ExitedProcessId()
    {
        using Process process = Process.Start(new ProcessStartInfo(
            OperatingSystem.IsWindows() ? Path.Combine(Environment.SystemDirectory, "whoami.exe") : "true")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        process.WaitForExit();
        return process.Id;
    }
}
