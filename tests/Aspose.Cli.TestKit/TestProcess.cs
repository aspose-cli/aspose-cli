using System.Diagnostics;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Runs one test in a new process of its test assembly, the executable xunit builds it as: for a
/// test that changes what a process cannot undo, or whose subject ends the process. The test
/// tells the run apart by the environment variable it is given.
/// </summary>
public static class TestProcess
{
    /// <summary>
    /// Runs <paramref name="method"/> of <paramref name="testClass"/> alone with
    /// <paramref name="variable"/> set to <paramref name="value"/>, and returns the process's exit
    /// code with its stdout followed by its stderr.
    /// </summary>
    public static (int ExitCode, string Output) RunAlone(Type testClass, string method, string variable, string value)
    {
        ArgumentNullException.ThrowIfNull(testClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);
        var start = new ProcessStartInfo(Path.ChangeExtension(testClass.Assembly.Location, ".exe"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add($"{testClass.FullName}.{method}");
        start.Environment[variable] = value;
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(2)), $"The test process of {method} did not exit.");
        return (process.ExitCode, output + error.GetAwaiter().GetResult());
    }
}
