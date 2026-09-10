using System.Diagnostics;
using System.Text;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Runs the `aspose-cli` executable as a child process, the way a user or an agent
/// would. The binary path and isolated evaluation environment are configurable
/// for current development-build tests.
/// </summary>
public sealed class CliProcess
{
    private readonly string _exePath;
    private readonly CliEnvironment _environment;
    private readonly TimeSpan _timeout;

    public CliProcess(string exePath, CliEnvironment environment, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(exePath);
        ArgumentNullException.ThrowIfNull(environment);
        _exePath = exePath;
        _environment = environment;
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
    }

    public CliResult Run(string workingDirectory, params string[] args) =>
        RunCore(workingDirectory, standardInput: null, args);

    public CliResult Run(string workingDirectory, string? standardInput, params string[] args)
        => RunCore(workingDirectory, standardInput, args);

    private CliResult RunCore(
        string workingDirectory,
        string? standardInput,
        params string[] args)
    {
        if (!File.Exists(_exePath))
        {
            throw new FileNotFoundException($"CLI executable not found: {_exePath}", _exePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            // Pinned so tests control stdin bytes exactly: the default falls
            // back to the host's OEM codepage, which silently replaces
            // non-ANSI characters (a U+FEFF becomes '?'). No BOM is emitted
            // by the writer itself; tests that want one send it explicitly.
            StandardInputEncoding = standardInput is not null ? new UTF8Encoding(false) : null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            ErrorDialog = false,
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        _environment.Apply(startInfo.Environment);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {_exePath}");

        if (standardInput is not null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        Task<string> stdOut = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"aspose-cli {string.Join(' ', args)} did not finish within {_timeout.TotalSeconds:N0}s.");
        }

        return new CliResult(process.ExitCode, stdOut.Result, stdErr.Result);
    }
}
