using System.Diagnostics;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

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

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<ProcessStartInfo> _startInfo;
    private readonly TimeSpan _timeout;
    private readonly Action<string>? _diagnostic;
    private Worker? _worker;
    private CliException? _terminationFailure;
    private int _nextId;
    private int _disposed;

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
        get
        {
            _gate.Wait();
            try { return _worker?.Process.Id; }
            finally { _gate.Release(); }
        }
    }

    /// <summary>One deadline covers admission, startup, I/O and the optional worker recycle.</summary>
    public RenderWorkerResponse Render(RenderWorkerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        TimeSpan budget = TimeSpan.FromMilliseconds(Budget(request.TimeoutMs));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        long ceiling = checked(Environment.TickCount64 + (long)budget.TotalMilliseconds);
        using var deadline = OperationDeadline.FromAbsoluteTick(budget,
            Math.Min(request.ExpiresAtTick ?? ceiling, ceiling), cancellation.Token);
        bool entered = false;
        try
        {
            _gate.Wait(deadline.Token);
            entered = true;
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_terminationFailure is { } failure) { throw failure; }
            request = request with { Id = ++_nextId, TimeoutMs = (int)budget.TotalMilliseconds,
                ExpiresAtTick = deadline.ExpiresAtTick };
            for (int attempt = 0; attempt < 2; attempt++)
            {
                deadline.ThrowIfExpired("render-start");
                RenderWorkerResponse? response;
                try
                {
                    Worker worker = _worker ??= Start();
                    response = ExchangeAsync(worker, request, deadline.Token).GetAwaiter().GetResult();
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException
                    or System.ComponentModel.Win32Exception)
                {
                    response = null;
                }
                deadline.ThrowIfExpired("render-response");
                if (response is { Recycle: false }) { return response; }
                Retire(response is null ? "the render worker ended before answering" : "recycling the render worker");
            }
            return Failure(request, ErrorCodes.Internal, "the render worker could not answer");
        }
        catch (Exception exception) when (exception is OperationCanceledException
            || exception is CliException failure && failure.Code == ErrorCodes.OperationTimeout)
        {
            if (entered) { Retire("the render was cancelled"); }
            if (!deadline.IsExpired) { throw; }
            return Failure(request, ErrorCodes.OperationTimeout,
                $"the render did not finish within {Math.Max(1, (int)Math.Ceiling(budget.TotalSeconds))} seconds");
        }
        finally { if (entered) { _gate.Release(); } }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        _shutdown.Cancel();
        _gate.Wait();
        try { Retire(null); }
        finally { _gate.Release(); _shutdown.Dispose(); }
    }

    private static async Task<RenderWorkerResponse?> ExchangeAsync(
        Worker worker, RenderWorkerRequest request, CancellationToken token)
    {
        await ProcessPipeMessages.WriteAsync(worker.Input, request, token).ConfigureAwait(false);
        RenderWorkerResponse? response = await ProcessPipeMessages
            .ReadOrEndAsync<RenderWorkerResponse>(worker.Output, token).ConfigureAwait(false);
        if (response is not null && response.Id != request.Id)
        { throw new InvalidDataException("The render worker returned a mismatched response."); }
        return response;
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
        if (worker is null)
        {
            return;
        }
        if (reason is not null)
        {
            _diagnostic?.Invoke(reason);
        }
        try
        {
            worker.Dispose(TerminationGrace, _diagnostic);
            _worker = null;
            _terminationFailure = null;
        }
        catch (CliException exception) when (exception.Code == ErrorCodes.WorkerTerminationFailed)
        {
            _terminationFailure = exception;
            throw;
        }
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

    private int Budget(int requested) =>
        requested > 0 ? Math.Min(requested, (int)_timeout.TotalMilliseconds) : (int)_timeout.TotalMilliseconds;

    private static RenderWorkerResponse Failure(RenderWorkerRequest request, ErrorCode code, string message) =>
        new() { Id = request.Id, Ok = false, Code = code.Name, Exit = (int)code.ExitCode, Message = message };

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
            bool stopped = false;
            try
            {
                try { Process.StandardInput.Close(); } catch (IOException) { }
                stopped = Process.WaitForExit((int)grace.TotalMilliseconds);
                if (!stopped)
                {
                    try { Process.Kill(entireProcessTree: true); }
                    catch (Exception exception) when (exception is InvalidOperationException
                        or System.ComponentModel.Win32Exception or NotSupportedException) { }
                    Job?.Dispose();
                    stopped = Process.WaitForExit((int)grace.TotalMilliseconds);
                }
                if (!stopped) { throw CliErrors.WorkerTerminationFailed(Process.Id); }
                if (Errors is { IsCompletedSuccessfully: true, Result.Length: > 0 } errors)
                { diagnostic?.Invoke("render worker: " + errors.Result); }
            }
            finally
            {
                if (stopped) { Job?.Dispose(); Process.Dispose(); }
            }
        }
    }
}
