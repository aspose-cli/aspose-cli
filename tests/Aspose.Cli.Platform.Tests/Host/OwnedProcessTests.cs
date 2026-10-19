using System.Diagnostics;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>How the host ends the child processes it owns.</summary>
public sealed class OwnedProcessTests
{
    [Fact]
    public void Stop_WhoseExitCannotBeConfirmed_ReportsWorkerTerminationFailed()
    {
        Requires.Windows();
        var start = new ProcessStartInfo(ToolPath.Require("pwsh")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 60" })
        {
            start.ArgumentList.Add(argument);
        }
        using OwnedProcess owned = OwnedProcess.Start(start, killOnClose: true, waitForExit: static (_, _) => Task.FromResult(false));
        try
        {
            CliException error = Assert.Throws<CliException>(() => owned.Stop());

            Assert.Equal(ErrorCodes.WorkerTerminationFailed, error.Code);
            Assert.Equal(owned.Process.Id, error.Details?["pid"]?.GetValue<int>());
        }
        finally
        {
            Assert.True(owned.Process.WaitForExit(TimeSpan.FromSeconds(30)), "The killed child kept running.");
        }
    }

    [Theory]
    [InlineData(nameof(SecondPress.Ignore), 1, true)]
    [InlineData(nameof(SecondPress.Ignore), 2, true)]
    [InlineData(nameof(SecondPress.Ignore), 3, true)]
    [InlineData(nameof(SecondPress.EndProcess), 1, true)]
    [InlineData(nameof(SecondPress.EndProcess), 2, false)]
    [InlineData(nameof(SecondPress.EndProcess), 3, false)]
    public void ConsoleInterrupt_KeepsTheProcessOnlyForTheFirstPressUnlessTheSecondIsIgnored(
        string secondPress, int press, bool keeps) =>
        Assert.Equal(keeps, ConsoleInterrupt.KeepsProcess(Enum.Parse<SecondPress>(secondPress), press));

    [Fact]
    public void ConsoleInterrupt_Press_CancelsTheWorkAndCountsThePresses()
    {
        var scope = new ConsoleInterrupt.Scope(SecondPress.EndProcess);
        Assert.Equal(1, scope.Press());
        Assert.True(scope.Cancellation.IsCancellationRequested);
        Assert.Equal(2, scope.Press());
        scope.Cancellation.Dispose();
    }

    [Fact]
    public async Task RenderWorker_ThatIgnoresItsClosedInput_IsKilledAndConfirmed()
    {
        Requires.Windows();
        string root = Path.Combine(Path.GetTempPath(), $"aspose-render-worker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string pidFile = Path.Combine(root, "worker.pid");
        Process? worker = null;
        try
        {
            using var supervisor = new RenderWorkerSupervisor(() => SleeperStartInfo(pidFile), TimeSpan.FromMinutes(1));
            using var cancellation = new CancellationTokenSource();
            Task<RenderWorkerResponse> render = Task.Run(() => supervisor.Render(new RenderWorkerRequest
            {
                Id = 1,
                Source = Path.Combine(root, "missing.docx"),
                Output = root,
                MaxPartCount = 1,
                TimeoutMs = 60_000,
            }, cancellation.Token));
            Assert.True(await WaitForFileAsync(pidFile, TimeSpan.FromSeconds(30)), "The worker did not start.");
            worker = Process.GetProcessById(int.Parse((await File.ReadAllTextAsync(pidFile)).Trim()));

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render);

            Assert.True(worker.HasExited, $"The render worker {worker.Id} survived its retirement.");
            Assert.Null(supervisor.ProcessId);
        }
        finally
        {
            if (worker is { HasExited: false }) { worker.Kill(entireProcessTree: true); }
            worker?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TimeoutWorker_TerminationFailure_KeepsTheWorkerRoot()
    {
        string? root = null;
        using OperationDeadline deadline = OperationDeadline.Start(TimeSpan.FromMinutes(1));
        string[] args = ["cells", "inspect", "report.xlsx"];
        try
        {
            CliException error = await Assert.ThrowsAsync<CliException>(() => TimeoutWorkerSupervisor.ExecuteAsync(
                ActualCommandTree.Host, args, ActualCommandTree.Parser.Parse(args), deadline,
                () => new ProcessStartInfo("unused-worker"),
                runChild: (start, _, _, _) =>
                {
                    root = start.Environment[WorkerOutputSession.RootEnvironmentVariable];
                    throw CliErrors.WorkerTerminationFailed(4242);
                }));

            Assert.Equal(ErrorCodes.WorkerTerminationFailed, error.Code);
            Assert.NotNull(root);
            Assert.True(Directory.Exists(root), "A worker that may still write lost its staging root.");
        }
        finally
        {
            if (root is not null && Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }

    /// <summary>
    /// A worker that writes its process id and then sleeps without reading its input; the
    /// render-worker argument the supervisor appends lands in a trailing comment.
    /// </summary>
    private static ProcessStartInfo SleeperStartInfo(string pidFile)
    {
        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
            $"Set-Content -LiteralPath '{pidFile}' -Value $PID; Start-Sleep -Seconds 60 #" })
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0) { return true; }
            await Task.Delay(50);
        }
        return false;
    }
}
