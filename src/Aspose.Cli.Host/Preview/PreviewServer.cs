using System.Diagnostics;
using System.Net;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Hosts one loopback-only live-preview session and coordinates request
/// admission, routing, and response completion.
/// </summary>
internal sealed class PreviewServer : IDisposable
{
    internal static readonly TimeSpan DefaultStopTimeout =
        TimeSpan.FromSeconds(2);

    private readonly int _requestedPort;
    private readonly PreviewRequestPipeline _pipeline;
    private readonly HttpRequestGate _requests;
    private HttpListener? _listener;
    private Task? _acceptLoop;
    private volatile bool _stopping;
    private int _started;
    private int _stopped;
    private int _disposed;

    /// <summary>Creates the server; nothing is bound until <see cref="Start"/>.</summary>
    /// <param name="options">The session wiring; see <see cref="PreviewServerOptions"/>.</param>
    public PreviewServer(PreviewServerOptions options)
        : this(options, LocalServiceResourceLimits.Resolve())
    {
    }

    internal PreviewServer(
        int requestedPort,
        PreviewRequestPipeline pipeline)
        : this(
            requestedPort,
            pipeline,
            LocalServiceResourceLimits.Resolve())
    {
    }

    internal PreviewServer(
        PreviewServerOptions options,
        LocalServiceResourceLimits limits)
        : this(
            options?.RequestedPort ?? throw new ArgumentNullException(nameof(options)),
            new PreviewRequestPipeline(options.Requests),
            limits)
    {
    }

    private PreviewServer(
        int requestedPort,
        PreviewRequestPipeline pipeline,
        LocalServiceResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(limits);
        _requestedPort = requestedPort;
        _requests = new HttpRequestGate(
            limits.MaximumConcurrentRequests);
        _pipeline = pipeline;
    }

    /// <summary>
    /// Binds loopback prefixes, starts the accept loop, and returns the bound
    /// port. A requested port of 0 selects an HTTP-capable dynamic port.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>LOOPBACK_PORT_IN_USE</c> when no requested or candidate port can bind.
    /// </exception>
    public int Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            throw new InvalidOperationException(
                "The preview server is already started.");
        }

        LoopbackHttpListenerBinding binding =
            LoopbackHttpListenerBinder.Start(
                _requestedPort,
                ["127.0.0.1", "localhost"]);
        _listener = binding.Listener;
        _acceptLoop = Task.Run(
            () => AcceptLoopAsync(binding.Listener));
        return binding.Port;
    }

    /// <summary>
    /// Stops accepting requests and drains active requests. SSE streams are
    /// owned and closed by the live event hub.
    /// </summary>
    public void Stop()
    {
        _ = Stop(DefaultStopTimeout);
    }

    /// <summary>
    /// Stops admission and waits for accepted requests for at most the
    /// supplied duration. A false result requires deferred owner cleanup.
    /// </summary>
    internal bool Stop(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        var elapsed = Stopwatch.StartNew();
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            _stopping = true;
            _requests.StopAccepting();
            CloseListener();
        }

        TimeSpan remaining = Remaining(timeout, elapsed.Elapsed);
        if (!WaitForAcceptLoop(remaining))
        {
            return false;
        }

        remaining = Remaining(timeout, elapsed.Elapsed);
        return _requests.WaitForDrain(remaining);
    }

    /// <summary>Waits off the caller path for a previously requested stop.</summary>
    internal void WaitUntilStopped()
    {
        _ = WaitForAcceptLoop(Timeout.InfiniteTimeSpan);
        _ = _requests.WaitForDrain(Timeout.InfiniteTimeSpan);
    }

    private void CloseListener()
    {
        HttpListener? listener = _listener;
        if (listener is null)
        {
            return;
        }

        try
        {
            listener.Stop();
            listener.Close();
        }
        catch (Exception exception) when (
            exception is ObjectDisposedException or HttpListenerException)
        {
        }
    }

    /// <summary>Same as <see cref="Stop"/>.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        DeferredResourceCleanup.CompleteOrDefer(
            Stop(DefaultStopTimeout),
            WaitUntilStopped,
            _requests.Dispose,
            "aspose-preview-http-cleanup",
            "preview HTTP server");
    }

    private async Task AcceptLoopAsync(HttpListener listener)
    {
        while (true)
        {
            HttpListenerContext? context = await AcceptAsync(listener)
                .ConfigureAwait(false);
            if (context is null)
            {
                return;
            }

            if (!_requests.TryEnter(out IDisposable? requestLease))
            {
                _pipeline.RejectUnavailable(context.Response);
                continue;
            }

            _ = Task.Run(() =>
            {
                using (requestLease)
                {
                    _pipeline.Route(
                        context,
                        BoundPort());
                }
            });
        }
    }

    private async Task<HttpListenerContext?> AcceptAsync(
        HttpListener listener)
    {
        while (true)
        {
            try
            {
                return await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is HttpListenerException
                    or ObjectDisposedException
                    or InvalidOperationException)
            {
                if (_stopping || !listener.IsListening)
                {
                    return null;
                }
            }
        }
    }

    private int BoundPort() =>
        _listener?.Prefixes
            .Select(static prefix => new Uri(prefix).Port)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            "The preview server is not bound.");

    private static TimeSpan Remaining(TimeSpan timeout, TimeSpan elapsed) =>
        elapsed >= timeout ? TimeSpan.Zero : timeout - elapsed;

    private bool WaitForAcceptLoop(TimeSpan timeout)
    {
        try
        {
            return _acceptLoop?.Wait(timeout) ?? true;
        }
        catch (AggregateException exception)
        {
            Trace.TraceWarning(
                "The preview accept loop ended unexpectedly: {0}",
                exception.GetBaseException().GetType().Name);
            return true;
        }
    }
}
