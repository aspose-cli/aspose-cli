using System.Diagnostics;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Retires one owned resource immediately and disposes it after its last borrowed lease ends.</summary>
internal sealed class RetainedResource<T>(T resource) : IDisposable where T : class, IDisposable
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private T? _resource = resource ?? throw new ArgumentNullException(nameof(resource));
    private int _references = 1;
    private bool _retired;
    internal Task Completion => _completion.Task;

    internal bool TryAcquire(out Lease? lease)
    {
        lock (_gate)
        {
            if (_retired) { lease = null; return false; }
            _references++;
            lease = new Lease(this);
            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_retired) { return; }
            _retired = true;
        }
        Release();
    }

    private void Release()
    {
        T? closing = null;
        lock (_gate)
        {
            if (--_references == 0) { closing = _resource; _resource = null; }
        }
        if (closing is null) { return; }
        try { closing.Dispose(); _completion.TrySetResult(); }
        catch (Exception error)
        {
            _completion.TrySetException(error);
            _ = _completion.Task.Exception;
            Trace.TraceWarning("A retired resource could not be disposed ({0}).", error.GetType().Name);
        }
    }

    internal sealed class Lease(RetainedResource<T> owner) : IDisposable
    {
        private RetainedResource<T>? _owner = owner;
        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}
