using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Configuration;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The assembly fixture of every test project. Tests never see the developer's
/// configuration, installed licenses or <c>ASPOSE_*</c> settings: the process gets a
/// private configuration directory, and only the <c>ASPOSE_CLI_TEST_*</c> inputs of the
/// run survive. It holds the account's <see cref="TestRunLock"/> unless the runner that started
/// it does. On teardown it stops the CLI processes this test process left behind, such
/// as warm viewer services and render workers, so they cannot lock the build output.
/// </summary>
/// <remarks>
/// The test process joins a job object of its own, which every process it starts inherits,
/// including services that outlive the command that started them. Teardown stops only members
/// of that job, so test projects that run at the same time never stop each other's processes.
/// </remarks>
public sealed class TestEnvironment : IDisposable
{
    private const string TestVariablePrefix = DistributionInfo.EnvironmentVariablePrefix + "TEST_";
    // First, so a run waits for the account's other test run before it changes anything.
    private readonly TestRunLock? _runLock = TestRunLock.AcquireUnlessHeld();
    private readonly TempDirectory _configuration = new();
    private readonly SafeFileHandle? _job = JoinOwnJob();

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
            if (_job is not null)
            {
                StopLeftoverProcesses(_job);
            }
        }
        finally
        {
            _job?.Dispose();
            _configuration.Dispose();
            _runLock?.Dispose();
        }
    }

    private static SafeFileHandle? JoinOwnJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        SafeFileHandle job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid || !AssignProcessToJobObject(job, Process.GetCurrentProcess().SafeHandle))
        {
            int error = Marshal.GetLastWin32Error();
            job.Dispose();
            throw new Win32Exception(error, "The test process could not join a job object of its own.");
        }
        return job;
    }

    /// <summary>Stops the tested executable's processes that belong to <paramref name="job"/>.</summary>
    private static void StopLeftoverProcesses(SafeFileHandle job)
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
                    if (IsProcessInJob(process.SafeHandle, job, out bool member)
                        && member
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(SafeProcessHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);
}
