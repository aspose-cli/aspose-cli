using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Product-neutral lifetime for one already-started foreground service.
/// Shutdown is guaranteed to invoke the owning component exactly once.
/// </summary>
internal sealed class HostedCommandLifecycle
{
    private readonly Func<TimeSpan?, CancellationToken, WaitOutcome> _wait;
    private readonly Action _shutdown;
    private int _shutdownStarted;

    public HostedCommandLifecycle(
        ResultEnvelope startup,
        bool once,
        TimeSpan idleAfter,
        Func<TimeSpan?, CancellationToken, WaitOutcome> wait,
        Action shutdown)
    {
        Startup = startup ?? throw new ArgumentNullException(nameof(startup));
        Once = once;
        IdleAfter = idleAfter;
        _wait = wait ?? throw new ArgumentNullException(nameof(wait));
        _shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
    }

    public ResultEnvelope Startup { get; }

    public bool Once { get; }

    public TimeSpan IdleAfter { get; }

    public WaitOutcome Wait(
        TimeSpan? deadline,
        CancellationToken cancellationToken) =>
        _wait(deadline, cancellationToken);

    public void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 0)
        {
            _shutdown();
        }
    }
}
