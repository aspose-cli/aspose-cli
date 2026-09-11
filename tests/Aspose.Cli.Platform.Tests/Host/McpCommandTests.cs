using System.Diagnostics;
using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Mcp;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class McpCommandTests
{


    [Fact]
    public void Server_ExposesExactlyTwoTools()
    {
        Assert.Equal(["capabilities", "execute"], McpServerHost.ToolNames);
    }

    [Fact]
    public void Server_ReportsReadOnlyDiscoveryAndMutatingExecution()
    {
        var tools = McpServerHost.CreateTools(new McpTools(new McpCommandRunner(ActualCommandTree.Host)));
        var discovery = tools.Single(tool => tool.ProtocolTool.Name == "capabilities").ProtocolTool;
        var execution = tools.Single(tool => tool.ProtocolTool.Name == "execute").ProtocolTool;

        Assert.True(discovery.Annotations!.ReadOnlyHint);
        Assert.False(discovery.Annotations.DestructiveHint);
        Assert.False(execution.Annotations!.ReadOnlyHint);
        Assert.True(execution.Annotations.DestructiveHint);
    }

    [Theory]
    [InlineData("cells", "query", "range", "input.xlsx")]
    [InlineData("pdf", "edit", "input.pdf", "--ops", "{\"ops\":[]}", "--out", "out.pdf")]
    [InlineData("schema", "v2/pdf/pdf-info")]
    [InlineData("doctor")]
    [InlineData("docs", "pdf/forms-security")]
    [InlineData("fonts", "list")]
    [InlineData("fonts", "check", "input.xlsx")]
    [InlineData("preview", "status")]
    [InlineData("app", "status")]
    public void Execute_AllowsOnlyDocumentAndReadOnlyHostCommands(
        params string[] args)
    {
        var runner = new McpCommandRunner(ActualCommandTree.Host);
        runner.EnsureAllowed(args);
    }

    [Theory]
    [InlineData("mcp", "serve")]
    [InlineData("capabilities")]
    [InlineData("license", "install")]
    [InlineData("license", "remove")]
    [InlineData("skill", "install")]
    [InlineData("preview", "start")]
    [InlineData("preview", "stop")]
    [InlineData("app", "stop")]
    [InlineData("update", "install")]
    public void Execute_RejectsPrivilegedAndLifecycleCommands(
        params string[] args)
    {
        var runner = new McpCommandRunner(ActualCommandTree.Host);
        Assert.Throws<McpCommandException>(() => runner.EnsureAllowed(args));
    }

    [Fact]
    public void Execute_EnforcesArgumentInputAndTimeoutBudgets()
    {
        var runner = new McpCommandRunner(ActualCommandTree.Host);

        Assert.Throws<McpCommandException>(() => runner.Validate([], null, 120));
        Assert.Throws<McpCommandException>(() => runner.Validate(
            Enumerable.Repeat("x", McpCommandRunner.MaximumArgumentCount + 1).ToArray(),
            null,
            120));
        Assert.Throws<McpCommandException>(() => runner.Validate(
            [new string('x', McpCommandRunner.MaximumArgumentBytes + 1)],
            null,
            120));
        Assert.Throws<McpCommandException>(() => runner.Validate(
            ["cells", "query", "range"],
            new string('x', McpCommandRunner.MaximumInputBytes + 1),
            120));
        Assert.Throws<McpCommandException>(() => runner.Validate(["cells"], null, 0));
        Assert.Throws<McpCommandException>(() => runner.Validate(["cells"], null, 601));
    }

    [Fact]
    public void Execute_ForwardsOnlyExplicitSafeEnvironmentReferences()
    {
        Assert.Equal(
            ["DOC_PASSWORD", "OUTPUT_PASSWORD"],
            InvocationEnvironment.ReferencedVariables(ActualCommandTree.Parser.Parse(
            [
                "words", "convert", "file.docx", "--to", "docx", "--out", "out.docx",
                "--password-env", "DOC_PASSWORD",
                "--encrypt-env=OUTPUT_PASSWORD",
            ])));
    }

    [Fact]
    public void McpServer_IsNeverWrappedByTheTimeoutWorker()
    {
        Assert.True(ActualCommandTree.Parser.Parse(
            ["--timeout", "30", "mcp", "serve"]).ServiceLifetime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_TimeoutOrCancellationClosesInheritedPipesAndKillsTheProcessTree(
        bool cancel)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            $"aspose-mcp-timeout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string childScript = Path.Combine(root, "child.ps1");
        string parentScript = Path.Combine(root, "parent.ps1");
        string pidFile = Path.Combine(root, "child.pid");
        Process? child = null;
        Task<McpExecutionResult>? execution = null;
        using var cancellation = new CancellationTokenSource();
        const int timeoutSeconds = 10;
        try
        {
            await File.WriteAllTextAsync(
                childScript,
                "$identity = Get-Process -Id $PID\n"
                + "$receipt = [string]$PID + '|' + [string]$identity.StartTime.ToUniversalTime().Ticks + '|' + $identity.Path\n"
                + $"[IO.File]::WriteAllText('{PowerShellLiteral(pidFile)}.tmp', $receipt)\n"
                + $"[IO.File]::Move('{PowerShellLiteral(pidFile)}.tmp', '{PowerShellLiteral(pidFile)}')\n"
                + "while ($true) { Start-Sleep -Milliseconds 100 }");
            await File.WriteAllTextAsync(
                parentScript,
                "$powershell = Join-Path $env:SystemRoot 'System32\\WindowsPowerShell\\v1.0\\powershell.exe'\n"
                + $"$child = Start-Process -FilePath $powershell -ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-File','{PowerShellLiteral(childScript)}' -NoNewWindow -PassThru\n"
                + $"while (-not (Test-Path -LiteralPath '{PowerShellLiteral(pidFile)}')) {{ Start-Sleep -Milliseconds 10 }}\n"
                + "while ($true) { Start-Sleep -Milliseconds 100 }");

            var runner = new McpCommandRunner(
                ProbeParser(),
                () => CreatePowerShellStartInfo(parentScript));
            var stopwatch = Stopwatch.StartNew();
            execution = runner.RunAsync(
                ["timeout-probe"],
                stdin: null,
                timeoutSeconds,
                cancellation.Token);

            // Two cold interpreters must be ready before this test can exercise descendant cleanup.
            Assert.True(
                await WaitForFileAsync(pidFile, TimeSpan.FromSeconds(5)),
                "The adversarial descendant did not become ready within its startup budget.");
            string[] identity = (await File.ReadAllTextAsync(pidFile)).Split('|');
            Process candidate = Process.GetProcessById(int.Parse(identity[0]));
            try
            {
                _ = candidate.Handle;
                Assert.Equal(long.Parse(identity[1]), candidate.StartTime.ToUniversalTime().Ticks);
                Assert.Equal(identity[2], candidate.MainModule!.FileName, ignoreCase: true);
                child = candidate;
            }
            finally
            {
                if (child is null) { candidate.Dispose(); }
            }

            if (cancel)
            {
                stopwatch.Restart();
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            }
            else
            {
                McpCommandException error = await Assert.ThrowsAsync<McpCommandException>(
                    () => execution);
                Assert.Contains($"exceeded the {timeoutSeconds}-second timeout", error.Message);
            }

            stopwatch.Stop();
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(cancel ? 0 : timeoutSeconds)
                    + McpCommandRunner.ShutdownGracePeriod + TimeSpan.FromSeconds(1),
                $"MCP process cleanup took {stopwatch.Elapsed}.");
            Assert.True(
                await WaitForExitAsync(child, TimeSpan.FromSeconds(3)),
                $"MCP descendant process {child.Id} survived timeout cleanup.");
        }
        finally
        {
            cancellation.Cancel();
            try { await ObserveCanceledExecutionAsync(execution); }
            finally
            {
                try { KillProcess(child); }
                finally { Directory.Delete(root, recursive: true); }
            }
        }
    }

    [Fact]
    public async Task Execute_OneSecondTimeoutDoesNotRequireAReadyDescendant()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        string root = Path.Combine(Path.GetTempPath(), $"aspose-mcp-timeout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string script = Path.Combine(root, "single-worker.ps1");
        Task<McpExecutionResult>? execution = null;
        using var cancellation = new CancellationTokenSource();
        try
        {
            await File.WriteAllTextAsync(script, "while ($true) { Start-Sleep -Milliseconds 100 }");
            var runner = new McpCommandRunner(ProbeParser(), () => CreatePowerShellStartInfo(script));
            var stopwatch = Stopwatch.StartNew();
            execution = runner.RunAsync(["timeout-probe"], null, 1, cancellation.Token);
            McpCommandException error = await Assert.ThrowsAsync<McpCommandException>(() => execution);
            Assert.Contains("exceeded the 1-second timeout", error.Message);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1)
                + McpCommandRunner.ShutdownGracePeriod + TimeSpan.FromSeconds(1), stopwatch.Elapsed.ToString());
        }
        finally
        {
            cancellation.Cancel();
            try { await ObserveCanceledExecutionAsync(execution); }
            finally { Directory.Delete(root, recursive: true); }
        }
    }

    private static InvocationParser ProbeParser()
    {
        var root = new RootCommand();
        var globals = new GlobalOptions(licensingApplicable: false);
        globals.AddTo(root);
        root.Subcommands.Add(new Command("timeout-probe").WithInvocationPolicy(
            new CommandInvocationPolicy(ProductId: "timeout-probe")));
        return new InvocationParser(root, globals);
    }

    private static ProcessStartInfo CreatePowerShellStartInfo(string script)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        return start;
    }

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (File.Exists(path))
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException) { return false; }
    }

    private static async Task ObserveCanceledExecutionAsync(Task? execution)
    {
        if (execution is null) { return; }
        try { await execution.WaitAsync(McpCommandRunner.ShutdownGracePeriod + TimeSpan.FromSeconds(3)); }
        catch (OperationCanceledException) { }
        catch (McpCommandException) { }
    }

    private static void KillProcess(Process? process)
    {
        if (process is null) { return; }
        try
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            Assert.True(process.WaitForExit(3_000), "The owned MCP descendant did not stop during test cleanup.");
        }
        finally { process.Dispose(); }
    }

    private static string PowerShellLiteral(string value) => value.Replace("'", "''");
}
