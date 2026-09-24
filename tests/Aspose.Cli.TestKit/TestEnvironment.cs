using System.ComponentModel;
using System.Diagnostics;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Configuration;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The assembly fixture of every test project. Tests never see the developer's
/// configuration, installed licenses or <c>ASPOSE_*</c> settings: the process gets a
/// private configuration directory, and only the <c>ASPOSE_CLI_TEST_*</c> inputs of the
/// run survive. On teardown it stops the CLI processes the run left behind, such as
/// warm viewer services and render workers, so they cannot lock the build output.
/// </summary>
public sealed class TestEnvironment : IDisposable
{
    private const string TestVariablePrefix = DistributionInfo.EnvironmentVariablePrefix + "TEST_";
    private readonly TempDirectory _configuration = new();
    private readonly DateTime _started = DateTime.Now;

    public TestEnvironment()
    {
        foreach (string name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(IsIsolated).ToArray())
        {
            Environment.SetEnvironmentVariable(name, null);
        }
        Environment.SetEnvironmentVariable(
            ConfigurationPaths.EnvironmentVariableName,
            _configuration.File(DistributionInfo.ConfigurationDirectoryName));
    }

    /// <summary>An inherited setting the CLI would read: every <c>ASPOSE_*</c> variable except a test input.</summary>
    internal static bool IsIsolated(string name) =>
        name.StartsWith("ASPOSE_", StringComparison.OrdinalIgnoreCase)
        && !name.StartsWith(TestVariablePrefix, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        try
        {
            StopLeftoverProcesses(_started);
        }
        finally
        {
            _configuration.Dispose();
        }
    }

    /// <summary>Stops processes of the tested executable that started at or after <paramref name="since"/>.</summary>
    public static void StopLeftoverProcesses(DateTime since)
    {
        if (!CliRunner.TryGetExecutablePath(out string? executable))
        {
            return;
        }
        foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using (process)
            {
                try
                {
                    if (process.StartTime >= since
                        && string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(10_000);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
                {
                    // The process exited while it was inspected.
                }
            }
        }
    }
}
