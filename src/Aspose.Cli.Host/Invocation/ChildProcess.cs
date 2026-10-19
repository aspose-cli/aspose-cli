using System.Buffers;
using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>How one child process ended and what it wrote.</summary>
internal sealed record ChildProcessResult(int ExitCode, byte[] Stdout, byte[] Stderr);

/// <summary>A child wrote more than its caller accepts on one stream.</summary>
internal sealed class ChildOutputLimitException(string stream, int limit)
    : Exception($"The CLI {stream} exceeded the {limit}-byte limit.");

/// <summary>
/// The one way the host runs a short-lived child of its own: start it as an
/// <see cref="OwnedProcess"/> bound to a kill-on-close job, read stdout and
/// stderr within one byte limit, and wait for it under the caller's token.
/// When anything fails — the token, a stream over its limit, a companion
/// task — the whole process tree is stopped before the failure propagates;
/// an exit that cannot be confirmed becomes <c>WORKER_TERMINATION_FAILED</c>.
/// The exit code and the output are the caller's to interpret.
/// </summary>
internal static class ChildProcess
{
    /// <param name="start">The child; this method sets its window and redirection flags.</param>
    /// <param name="maximumOutputBytes">Limit of stdout and of stderr, each.</param>
    /// <param name="cancellationToken">Ends the child early.</param>
    /// <param name="companions">
    /// Work that runs beside the started child, such as feeding its stdin;
    /// a failed companion ends the child like any other failure.
    /// </param>
    public static async Task<ChildProcessResult> RunAsync(
        ProcessStartInfo start,
        int maximumOutputBytes,
        CancellationToken cancellationToken,
        Func<Process, CancellationToken, IReadOnlyList<Task>>? companions = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputBytes);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using OwnedProcess owned = OwnedProcess.Start(start, killOnClose: true);
        Process process = owned.Process;
        Task[] observed = [];
        try
        {
            Task<byte[]> stdout = ReadBoundedAsync(
                process.StandardOutput.BaseStream, "stdout", maximumOutputBytes, cancellationToken);
            Task<byte[]> stderr = ReadBoundedAsync(
                process.StandardError.BaseStream, "stderr", maximumOutputBytes, cancellationToken);
            observed = [.. companions?.Invoke(process, cancellationToken) ?? [], stdout, stderr];
            // Observe each fault as it happens: a full pipe must not leave the child blocked.
            await AwaitEachAsync([.. observed, process.WaitForExitAsync(cancellationToken)], cancellationToken)
                .ConfigureAwait(false);
            return new ChildProcessResult(
                process.ExitCode,
                await stdout.ConfigureAwait(false),
                await stderr.ConfigureAwait(false));
        }
        catch
        {
            if (!await StopAsync(owned, observed).ConfigureAwait(false))
            {
                throw CliErrors.WorkerTerminationFailed(process.Id);
            }
            throw;
        }
    }

    private static async Task AwaitEachAsync(IReadOnlyList<Task> tasks, CancellationToken token)
    {
        var pending = tasks.ToList();
        while (pending.Count > 0)
        {
            Task completed = await Task.WhenAny(pending).WaitAsync(token).ConfigureAwait(false);
            pending.Remove(completed);
            await completed.ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        string name,
        int limit,
        CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var output = new MemoryStream();
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (read == 0)
                {
                    return output.ToArray();
                }
                if (output.Length + read > limit)
                {
                    throw new ChildOutputLimitException(name, limit);
                }
                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Kills the tree, closes its pipes before the exit is awaited (a child that exits only on
    /// end of input would otherwise count as unterminated) and reports whether its exit was
    /// confirmed within the grace.
    /// </summary>
    private static async Task<bool> StopAsync(OwnedProcess owned, Task[] observed)
    {
        Process process = owned.Process;
        return await owned.TryStopAsync(() =>
        {
            if (process.StartInfo.RedirectStandardInput)
            {
                process.StandardInput.Dispose();
            }
            process.StandardOutput.Dispose();
            process.StandardError.Dispose();
            // Observe the remaining faults without extending the termination budget.
            _ = Task.WhenAll(observed).ContinueWith(
                static task => { _ = task.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }).ConfigureAwait(false);
    }
}
