using System.ComponentModel;
using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// The one way the host starts and ends a child process it owns. A child started with
/// <c>killOnClose</c> is bound to a Windows job that kills its whole tree when this process
/// ends; a long-lived service that must outlive its launcher is started without one. Stopping
/// kills the tree, releases the job and confirms the exit within <see cref="TerminationGrace"/>;
/// an exit that cannot be confirmed is <c>WORKER_TERMINATION_FAILED</c>, because the child may
/// still write.
/// </summary>
internal sealed class OwnedProcess : IDisposable
{
    /// <summary>How long a killed child may take to exit before it counts as unterminated.</summary>
    public static readonly TimeSpan TerminationGrace = TimeSpan.FromSeconds(10);

    private readonly Func<Process, TimeSpan, Task<bool>> _waitForExit;
    private IDisposable? _job;

    private OwnedProcess(Process process, Func<Process, TimeSpan, Task<bool>> waitForExit)
    {
        Process = process;
        _waitForExit = waitForExit;
    }

    public Process Process { get; }

    /// <summary>
    /// Starts <paramref name="start"/> as is. A child that started but could not be bound to its
    /// job is stopped before the failure propagates.
    /// </summary>
    public static OwnedProcess Start(ProcessStartInfo start, bool killOnClose) =>
        Start(start, killOnClose, WaitForExitAsync);

    /// <summary>As <see cref="Start(ProcessStartInfo, bool)"/>, with the exit confirmation a test supplies.</summary>
    internal static OwnedProcess Start(
        ProcessStartInfo start, bool killOnClose, Func<Process, TimeSpan, Task<bool>> waitForExit)
    {
        ArgumentNullException.ThrowIfNull(start);
        Process process = Process.Start(start)
            ?? throw new IOException("The child process could not be started.");
        var owned = new OwnedProcess(process, waitForExit);
        if (!killOnClose) { return owned; }
        try
        {
            owned._job = WindowsProcessJob.Attach(process);
            return owned;
        }
        catch
        {
            using (owned) { owned.Stop(); }
            throw;
        }
    }

    /// <summary>
    /// Ends the child: with a positive <paramref name="naturalExit"/> it first closes the child's
    /// input and waits that long for it to exit on its own; then it kills the tree.
    /// </summary>
    /// <exception cref="CliException"><c>WORKER_TERMINATION_FAILED</c> when the exit is not confirmed.</exception>
    public void Stop(TimeSpan naturalExit = default)
    {
        if (naturalExit > TimeSpan.Zero)
        {
            if (Process.StartInfo.RedirectStandardInput)
            {
                try { Process.StandardInput.Close(); }
                catch (IOException) { }
            }
            if (_waitForExit(Process, naturalExit).GetAwaiter().GetResult())
            {
                ReleaseJob();
                return;
            }
        }
        if (!TryStopAsync().GetAwaiter().GetResult())
        {
            throw CliErrors.WorkerTerminationFailed(Process.Id);
        }
    }

    /// <summary>
    /// Kills the tree, releases the job, runs <paramref name="afterKill"/> (such as closing the
    /// caller's pipes) and reports whether the exit was confirmed within the grace.
    /// </summary>
    public async Task<bool> TryStopAsync(Action? afterKill = null)
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Closing the kill-on-close job and the exit confirmation below remain the boundary.
        }
        ReleaseJob();
        afterKill?.Invoke();
        return await _waitForExit(Process, TerminationGrace).ConfigureAwait(false);
    }

    /// <summary>Releases the job, which kills a child still bound to it, and the process handle.</summary>
    public void Dispose()
    {
        ReleaseJob();
        Process.Dispose();
    }

    private void ReleaseJob()
    {
        _job?.Dispose();
        _job = null;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var grace = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        return process.HasExited;
    }
}
