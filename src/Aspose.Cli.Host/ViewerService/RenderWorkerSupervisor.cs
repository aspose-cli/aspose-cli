using System.Diagnostics;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Keeps one render worker warm and renders through it. The worker is a child
/// of this process that holds the product engines hot, so only the first
/// render of a session pays for engine startup. A worker that died, hung or
/// asked to be recycled is replaced; the request is then sent once more, so a
/// crash or a license change costs one render rather than the session.
/// Renders are serialized: one document at a time keeps memory bounded and
/// the newest revision first in line.
/// </summary>
internal sealed class RenderWorkerSupervisor : IDisposable
{
    private static readonly TimeSpan TerminationGrace = TimeSpan.FromSeconds(2);
    private const int StderrTailBytes = 8 * 1024;

    private readonly object _gate = new();
    private readonly Func<ProcessStartInfo> _startInfo;
    private readonly TimeSpan _timeout;
    private readonly Action<string>? _diagnostic;
    private Worker? _worker;
    private int _nextId;
    private bool _disposed;

    /// <param name="startInfo">
    /// Builds the child process for this executable, with the working
    /// directory, configuration and license options of the service.
    /// </param>
    /// <param name="timeout">Hard bound for one render, including worker startup.</param>
    /// <param name="diagnostic">Receives one-line worker notices.</param>
    public RenderWorkerSupervisor(
        Func<ProcessStartInfo> startInfo,
        TimeSpan timeout,
        Action<string>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _startInfo = startInfo;
        _timeout = timeout;
        _diagnostic = diagnostic;
    }

    /// <summary>Process id of the running worker, or null while none runs.</summary>
    public int? ProcessId
    {
        get { lock (_gate) { return _worker?.Process.Id; } }
    }

    /// <summary>Renders one request, restarting the worker when it cannot answer.</summary>
    public RenderWorkerResponse Render(RenderWorkerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            request = request with { Id = ++_nextId, TimeoutMs = Budget(request.TimeoutMs) };
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Worker worker;
                try
                {
                    worker = _worker ??= Start();
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    return Failure(request, ErrorCodes.Internal.Name, "the render worker could not be started");
                }

                RenderWorkerResponse? response = Exchange(worker, request, out bool timedOut);
                if (response is { Recycle: false })
                {
                    return response;
                }

                Retire(response is null && !timedOut
                    ? "the render worker ended before answering"
                    : response is null ? "the render worker did not answer in time" : "recycling the render worker");
                if (timedOut)
                {
                    return Failure(
                        request,
                        ErrorCodes.OperationTimeout.Name,
                        $"the render did not finish within {(int)_timeout.TotalSeconds} seconds");
                }
            }

            return Failure(request, ErrorCodes.Internal.Name, "the render worker could not answer");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Retire(null);
        }
    }

    /// <summary>
    /// Sends one request and waits for its answer. Returns null when the
    /// worker ended or ran past the bound; <paramref name="timedOut"/> tells
    /// the two apart.
    /// </summary>
    private RenderWorkerResponse? Exchange(Worker worker, RenderWorkerRequest request, out bool timedOut)
    {
        timedOut = false;
        try
        {
            ProcessPipeMessages.WriteAsync(worker.Input, request, CancellationToken.None)
                .GetAwaiter().GetResult();
            Task<RenderWorkerResponse?> answer = ReadAnswerAsync(worker, request.Id);
            if (!answer.Wait(_timeout))
            {
                timedOut = true;
                Observe(answer);
                return null;
            }
            return answer.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or ObjectDisposedException or AggregateException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<RenderWorkerResponse?> ReadAnswerAsync(Worker worker, int id)
    {
        while (true)
        {
            RenderWorkerResponse? response = await ProcessPipeMessages
                .ReadOrEndAsync<RenderWorkerResponse>(worker.Output, CancellationToken.None)
                .ConfigureAwait(false);
            if (response is null || response.Id == id)
            {
                return response;
            }
        }
    }

    private Worker Start()
    {
        ProcessStartInfo start = _startInfo();
        start.ArgumentList.Add(RenderWorkerProtocol.CommandName);
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        var process = new Process { StartInfo = start };
        process.Start();
        var worker = new Worker(process, WindowsProcessJob.TryAttach(process));
        worker.Errors = DrainAsync(process.StandardError);
        return worker;
    }

    private void Retire(string? reason)
    {
        Worker? worker = _worker;
        _worker = null;
        if (worker is null)
        {
            return;
        }
        if (reason is not null)
        {
            _diagnostic?.Invoke(reason);
        }
        worker.Dispose(TerminationGrace, _diagnostic);
    }

    /// <summary>
    /// Keeps the worker's error pipe empty so it never blocks on a full
    /// buffer, and remembers its tail for the diagnostic of a failure.
    /// </summary>
    private static async Task<string> DrainAsync(StreamReader errors)
    {
        var tail = new Queue<string>();
        int bytes = 0;
        while (await errors.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            tail.Enqueue(line);
            bytes += line.Length;
            while (bytes > StderrTailBytes && tail.Count > 1)
            {
                bytes -= tail.Dequeue().Length;
            }
        }
        return string.Join(' ', tail);
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            static faulted => _ = faulted.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private int Budget(int requested) =>
        requested > 0 ? Math.Min(requested, (int)_timeout.TotalMilliseconds) : (int)_timeout.TotalMilliseconds;

    private static RenderWorkerResponse Failure(RenderWorkerRequest request, string code, string message) =>
        new() { Id = request.Id, Ok = false, Code = code, Message = message };

    private sealed record Worker(Process Process, IDisposable? Job)
    {
        public Stream Input => Process.StandardInput.BaseStream;

        public Stream Output => Process.StandardOutput.BaseStream;

        public Task<string>? Errors { get; set; }

        /// <summary>
        /// Ends the worker: closing its input asks it to exit, and anything
        /// still alive after the grace period is killed with its children.
        /// </summary>
        public void Dispose(TimeSpan grace, Action<string>? diagnostic)
        {
            try
            {
                try { Process.StandardInput.Close(); } catch (IOException) { }
                if (!Process.WaitForExit((int)grace.TotalMilliseconds))
                {
                    Process.Kill(entireProcessTree: true);
                    Process.WaitForExit((int)grace.TotalMilliseconds);
                }
                if (Errors is { IsCompletedSuccessfully: true, Result.Length: > 0 } errors)
                {
                    diagnostic?.Invoke("render worker: " + errors.Result);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                // The worker had already gone; nothing is left to reclaim.
            }
            finally
            {
                Job?.Dispose();
                Process.Dispose();
            }
        }
    }
}
