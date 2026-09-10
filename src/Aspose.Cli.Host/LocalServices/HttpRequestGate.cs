namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Bounds local HTTP concurrency and drains admitted requests during
/// shutdown. Rejected work never reaches App or product code.
/// </summary>
internal sealed class HttpRequestGate : IDisposable
{
    private readonly object _sync = new();
    private readonly ManualResetEventSlim _drained =
        new(initialState: true);
    private int _active;
    private bool _accepting = true;
    private bool _disposed;

    public HttpRequestGate(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            capacity,
            1,
            nameof(capacity));
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Active
    {
        get
        {
            lock (_sync)
            {
                return _active;
            }
        }
    }

    public bool TryEnter(out IDisposable? lease)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_accepting || _active == Capacity)
            {
                lease = null;
                return false;
            }

            if (_active++ == 0)
            {
                _drained.Reset();
            }

            lease = new Lease(this);
            return true;
        }
    }

    public void StopAccepting()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _accepting = false;
            }
        }
    }

    public bool WaitForDrain(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero
            && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        return _drained.Wait(timeout);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            if (_active != 0)
            {
                throw new InvalidOperationException(
                    "The HTTP request gate was disposed with active requests.");
            }

            _disposed = true;
            _accepting = false;
        }

        _drained.Dispose();
    }

    private void Exit()
    {
        lock (_sync)
        {
            if (--_active == 0)
            {
                _drained.Set();
            }
        }
    }

    private sealed class Lease(HttpRequestGate owner)
        : IDisposable
    {
        private HttpRequestGate? _owner = owner;

        public void Dispose() =>
            Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
