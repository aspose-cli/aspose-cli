using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        // PowerShell 7 is a .NET process like the CLI's workers, and starts warm on every test machine.
        var start = new ProcessStartInfo(ToolPath.Require("pwsh"))
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
            // Crash UI waits for a user indefinitely; the bound only has to outlast a loaded machine.
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
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

    /// <summary>
    /// A bootstrap failure can be the SDK's own static initialization, which then fails again
    /// wherever the report touches SDK state: the boundary still ends the process with exit code
    /// 1 and a plain INTERNAL_ERROR line, whatever the envelope's serialization throws.
    /// </summary>
    [Fact]
    public void BootstrapFailure_FallsBackToAPlainLineWhenTheEnvelopeFails()
    {
        var error = new FailingWriter(failures: 1);

        int exitCode = ProcessFailureBoundary.RenderBootstrapFailure(
            new TypeInitializationException("Aspose.Cli.Sdk.Errors.ErrorCodes", new InvalidOperationException("static failure")),
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("INTERNAL_ERROR", error.Written, StringComparison.Ordinal);
        Assert.Contains("HOST-PROCESS-0003", error.Written, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whatever the console throws, the bootstrap boundary itself never throws: a process whose
    /// startup failed exits with code 1 instead of crashing.
    /// </summary>
    [Fact]
    public void BootstrapFailure_NeverThrowsWhenEveryWriteFails()
    {
        var error = new FailingWriter(failures: int.MaxValue);

        int exitCode = ProcessFailureBoundary.RenderBootstrapFailure(
            new TypeInitializationException("Aspose.Cli.Sdk.Errors.ErrorCodes", new InvalidOperationException("static failure")),
            error);

        Assert.Equal(1, exitCode);
    }

    /// <summary>
    /// The plain fallback line runs when the envelope could not be written, possibly because
    /// the SDK's static state is what failed, so it reads no static field or property of the SDK
    /// or Host: only constants and literals, which the compiler inlines.
    /// </summary>
    [Fact]
    public void BootstrapFallback_ReadsNoStaticStateOfTheSdkOrHost()
    {
        string path = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host", "Invocation", "ProcessFailureBoundary.cs");
        SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(static candidate => candidate.Identifier.ValueText == nameof(ProcessFailureBoundary.RenderBootstrapFailure));
        CatchClauseSyntax[] fallbacks = [.. method.DescendantNodes().OfType<CatchClauseSyntax>()];
        Assert.NotEmpty(fallbacks);

        Type[] types =
        [
            .. typeof(Aspose.Cli.Sdk.Errors.ErrorCodes).Assembly.GetTypes(),
            .. typeof(ProcessFailureBoundary).Assembly.GetTypes(),
        ];
        string[] reads =
        [
            .. fallbacks.SelectMany(static fallback => fallback.Block.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                .Where(access => access.Expression is IdentifierNameSyntax owner && types.Any(type =>
                    type.Name == owner.Identifier.ValueText && IsStaticState(type, access.Name.Identifier.ValueText)))
                .Select(static access => $"line {access.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {access}"),
        ];

        Assert.True(reads.Length == 0,
            "The bootstrap fallback reads static state that may be what failed to initialize; write the code as a literal or constant:"
            + Environment.NewLine + string.Join(Environment.NewLine, reads));

        static bool IsStaticState(Type type, string member) =>
            type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) is { IsLiteral: false }
            || type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) is not null;
    }

    /// <summary>A console whose first writes throw as a type whose static initialization failed would.</summary>
    private sealed class FailingWriter(int failures) : TextWriter
    {
        private readonly StringBuilder _written = new();
        private int _failures = failures;

        public override Encoding Encoding => Encoding.UTF8;

        public string Written => _written.ToString();

        public override void Write(char value) => Fail().Append(value);

        public override void Write(string? value) => Fail().Append(value);

        public override void WriteLine(string? value) => Fail().AppendLine(value);

        public override void Flush() => Fail();

        private StringBuilder Fail() => _failures-- > 0
            ? throw new TypeInitializationException("Aspose.Cli.Sdk.Errors.ErrorCodes", null)
            : _written;
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
