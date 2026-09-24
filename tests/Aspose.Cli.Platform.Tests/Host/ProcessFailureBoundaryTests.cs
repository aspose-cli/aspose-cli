using System.Text.Json.Nodes;
using System.Diagnostics;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// Verifies the process-level boundary used by interactive and hidden CLI
/// processes after command-specific exception handling has been exhausted.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class ProcessFailureBoundaryTests
{
    [Fact]
    public void WindowsErrorMode_AddsRequiredFlagsWithoutClearingExistingBits()
    {
        const uint existing = 0x4000;

        uint mode = WindowsProcessErrorMode.AddNonInteractiveFlags(existing);

        Assert.Equal(
            existing
                | WindowsProcessErrorMode.SemFailCriticalErrors
                | WindowsProcessErrorMode.SemNoGpFaultErrorBox,
            mode);
    }

    [Fact]
    public void WindowsErrorMode_SuppressionIsSafeOnTheCurrentHost()
    {
        WindowsProcessErrorMode.SuppressNativeErrorUi();
        if (OperatingSystem.IsWindows())
        {
            uint actual = WindowsProcessErrorMode.GetCurrentModeForTesting();
            Assert.Equal(
                WindowsProcessErrorMode.SemFailCriticalErrors,
                actual & WindowsProcessErrorMode.SemFailCriticalErrors);
            Assert.Equal(
                WindowsProcessErrorMode.SemNoGpFaultErrorBox,
                actual & WindowsProcessErrorMode.SemNoGpFaultErrorBox);
        }
    }

    [Fact]
    public async Task WindowsErrorMode_InheritedFailFastChildTerminatesWithoutBlockingOnCrashUi()
    {
        Requires.Windows();

        WindowsProcessErrorMode.SuppressNativeErrorUi();
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        foreach (string argument in new[]
        {
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            "[Environment]::FailFast('aspose-cli crash-ui regression probe')",
        })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["DOTNET_EnableCrashReport"] = "0";

        using Process child = Process.Start(start)!;
        Task<string> output = child.StandardOutput.ReadToEndAsync();
        Task<string> error = child.StandardError.ReadToEndAsync();
        bool exited;
        try
        {
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            exited = true;
        }
        catch (TimeoutException)
        {
            exited = false;
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        if (!exited)
        {
            Assert.Fail("A fatal child process was blocked by interactive crash UI.");
            return;
        }
        Assert.NotEqual(0, child.ExitCode);
        await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Execute_ReturnsTheReporterExitCode_WithoutLeakingTheException()
    {
        var expected = new InvalidOperationException("boundary failure");
        Exception? reported = null;

        int exitCode = ProcessFailureBoundary.Execute(
            () => throw expected,
            exception =>
            {
                reported = exception;
                return 37;
            });

        Assert.Equal(37, exitCode);
        Assert.Same(expected, reported);
    }

    [Fact]
    public void Execute_ReturnsNormally_WithoutCallingTheReporter()
    {
        int exitCode = ProcessFailureBoundary.Execute(
            () => 12,
            _ => throw new InvalidOperationException(
                "The reporter must not run for a successful process."));

        Assert.Equal(12, exitCode);
    }

    [Fact]
    public void BootstrapFailure_IsStructuredAndDoesNotLeakExceptionDetails()
    {
        using var error = new StringWriter();

        int exitCode = ProcessFailureBoundary.RenderBootstrapFailure(
            new InvalidOperationException(
                "bootstrap-secret=C:\\customers\\private\\license.lic"),
            error);

        Assert.Equal(1, exitCode);
        JsonNode envelope = JsonNode.Parse(error.ToString())!;
        Assert.Equal(
            "INTERNAL_ERROR",
            envelope["error"]!["code"]!.GetValue<string>());
        Assert.Equal(
            "HOST-PROCESS-0003",
            envelope["error"]!["details"]!["diagnosticId"]!
                .GetValue<string>());
        Assert.DoesNotContain(
            "customers",
            error.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "license.lic",
            error.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("token=0123456789abcdef")]
    [InlineData("csrf=0123456789abcdef")]
    [InlineData("authorization=Bearer-secret")]
    [InlineData("license-content=private-bytes")]
    public void DiagnosticRedactor_RemovesSecretAssignments(
        string secret)
    {
        string redacted = DiagnosticRedactor.Redact(
            "failure " + secret + " inner=problem");

        Assert.DoesNotContain(
            secret[(secret.IndexOf('=') + 1)..],
            redacted,
            StringComparison.Ordinal);
        Assert.Contains("<redacted>", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticRedactor_RemovesWindowsAndUnixSourcePaths()
    {
        string redacted = DiagnosticRedactor.Redact(
            "at Work in /home/alice/repo/Secret.cs:line 42 "
            + "inner=C:\\Users\\Alice\\repo\\Secret.cs at Work");

        Assert.DoesNotContain("/home/alice", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "C:\\Users\\Alice",
            redacted,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("--timeout 30 doctor", "doctor")]
    [InlineData("cells --timeout=30 inspect input.xlsx", "cells inspect")]
    [InlineData("--output json app status", "app status")]
    public void TimeoutSupervisor_ResolvesCommandPathAroundGlobalOptions(
        string commandLine,
        string expected)
    {
        IEnumerable<string> path = ActualCommandTree.Parser.Parse(
            commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)).CommandPath.Select(command => command.Name);

        Assert.Equal(expected, string.Join(' ', path));
    }
}
