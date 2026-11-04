using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>What a command run under <c>--timeout</c> reports from the worker that ran it.</summary>
public sealed class TimeoutWorkerSupervisorTests
{
    [Theory]
    [InlineData(-1073741571, "0xC00000FD", "")]
    [InlineData(-532462766, "0xE0434352", "{\"schema\":")]
    public async Task AWorkerThatCrashes_ReportsAnInternalErrorAndPublishesNothing(int exitCode, string hex, string stdout)
    {
        using var temp = new TempDirectory();
        string target = temp.File("out.png");
        string? root = null;
        using OperationDeadline deadline = OperationDeadline.Start(TimeSpan.FromMinutes(1));
        string[] args = ["pdf", "render", temp.File("out.pdf"), "--timeout", "60", "--output-format", "json"];
        try
        {
            InvocationProcessResult result = await TimeoutWorkerSupervisor.ExecuteAsync(
                ActualCommandTree.Host, args, ActualCommandTree.Parser.Parse(args), deadline,
                () => new ProcessStartInfo("unused-worker"),
                runChild: (start, _, _, _) =>
                {
                    root = start.Environment[WorkerOutputSession.RootEnvironmentVariable]!;
                    string manifest = start.Environment[WorkerOutputSession.ManifestEnvironmentVariable]!;
                    HandOff(root, manifest, temp.Path, target);
                    Assert.True(File.Exists(manifest), "The worker handed off no output to withhold.");
                    return Task.FromResult(new ChildProcessResult(
                        exitCode, Encoding.UTF8.GetBytes(stdout), Encoding.UTF8.GetBytes("Stack overflow.\r\n   at Engine.Draw()\r\n")));
                });

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Stdout);
            JsonNode error = JsonNode.Parse(result.Stderr)!["error"]!;
            Assert.Equal("INTERNAL_ERROR", error["code"]!.GetValue<string>());
            string message = error["message"]!.GetValue<string>();
            Assert.Contains(hex, message, StringComparison.Ordinal);
            Assert.Contains("no output was published", message, StringComparison.Ordinal);
            Assert.Contains("re-exported copy", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.DoesNotContain("Stack overflow", result.Stderr, StringComparison.Ordinal);
            Assert.False(File.Exists(target), "A crashed worker's output was published.");
            Assert.NotNull(root);
            Assert.False(Directory.Exists(root), "A crashed worker's staging was kept.");
        }
        finally
        {
            if (root is not null && Directory.Exists(root)) { UserStorage.TryDeleteTree(root); }
        }
    }

    [Theory]
    [InlineData(0, "{\"schema\":\"result\"}\n", "")]
    [InlineData(1, "", "{\"error\":{\"code\":\"INTERNAL_ERROR\"}}\n")]
    [InlineData(3, "", "{\"error\":{\"code\":\"FILE_NOT_FOUND\"}}\n")]
    [InlineData(8, "{\"schema\":\"result\"}\n", "")]
    [InlineData(130, "", "")]
    public async Task AWorkerThatExitsWithACliExitCode_KeepsItsOutputAndExitCode(int exitCode, string stdout, string stderr)
    {
        using OperationDeadline deadline = OperationDeadline.Start(TimeSpan.FromMinutes(1));
        string[] args = ["pdf", "render", "out.pdf", "--timeout", "60", "--output-format", "json"];

        InvocationProcessResult result = await TimeoutWorkerSupervisor.ExecuteAsync(
            ActualCommandTree.Host, args, ActualCommandTree.Parser.Parse(args), deadline,
            () => new ProcessStartInfo("unused-worker"),
            runChild: (_, _, _, _) => Task.FromResult(new ChildProcessResult(
                exitCode, Encoding.UTF8.GetBytes(stdout), Encoding.UTF8.GetBytes(stderr))));

        Assert.Equal(new InvocationProcessResult(exitCode, stdout, stderr), result);
    }

    /// <summary>Stages <paramref name="target"/> in the worker's root and seals its manifest, as a worker does before it exits.</summary>
    private static void HandOff(string root, string manifest, string directory, string target)
    {
        var worker = new WorkerOutputSession(root, manifest);
        using (OperationDeadline deadline = OperationDeadline.Start(null))
        using (var outputs = new AtomicOutputSetWriter(
            new SafeFileWriter(new ResourceBudgetLedger(deadline, outputSession: worker)), directory, "render"))
        {
            outputs.Stage(target, false, file => File.WriteAllText(file, "png"));
            outputs.Commit();
        }
        worker.SealForPublication();
    }
}
