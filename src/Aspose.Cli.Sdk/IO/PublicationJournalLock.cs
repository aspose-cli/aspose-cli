using System.Security.Cryptography;
using System.Text;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Serializes synchronous access to one journal across the current user's processes and sessions.
/// The OS owns the named object's lifetime; publication does not leave a lock file for every transaction.
/// </summary>
internal sealed class PublicationJournalLock : IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;

    private PublicationJournalLock(Mutex mutex) => _mutex = mutex;

    /// <summary>
    /// Acquires the lock only if it is free now. A held lock proves a live process is using the
    /// journal: the lock of a process that died is granted, as abandoned.
    /// </summary>
    internal static PublicationJournalLock? TryAcquire(string path)
    {
        Mutex mutex = Open(path);
        bool acquired;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (acquired) { return new PublicationJournalLock(mutex); }
        mutex.Dispose();
        return null;
    }

    internal static PublicationJournalLock Acquire(string path, OperationDeadline? deadline = null,
        IPublicationFaultInjector? faults = null)
    {
        Mutex mutex = Open(path);
        using OperationDeadline? owned = deadline is null ? OperationDeadline.Start(TimeSpan.FromSeconds(30)) : null;
        OperationDeadline wait = deadline ?? owned!;
        bool acquired = false;
        try
        {
            while (!acquired)
            {
                wait.ThrowIfExpired("publication-journal-lock");
                try { acquired = mutex.WaitOne(0); }
                catch (AbandonedMutexException)
                {
                    // Ownership is granted after abandonment. The caller still validates the bounded journal.
                    acquired = true;
                }
                if (!acquired)
                {
                    faults?.Hit(new PublicationFaultPoint(PublicationFaultKind.JournalLockWait, -1, path));
                    if (wait.Token.WaitHandle.WaitOne(25)) { wait.ThrowIfExpired("publication-journal-lock"); }
                }
            }
            wait.ThrowIfExpired("publication-journal-lock");
            return new PublicationJournalLock(mutex);
        }
        catch
        {
            if (acquired) { mutex.ReleaseMutex(); }
            mutex.Dispose();
            throw;
        }
    }

    private static Mutex Open(string path)
    {
        string canonical = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()) { canonical = canonical.ToUpperInvariant(); }
        string name = "aspose-publication-journal-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new Mutex(false, name, new NamedWaitHandleOptions
        {
            CurrentUserOnly = true,
            CurrentSessionOnly = false,
        });
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        // All call sites are lexical, synchronous journal operations: ownership never crosses a thread switch.
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}