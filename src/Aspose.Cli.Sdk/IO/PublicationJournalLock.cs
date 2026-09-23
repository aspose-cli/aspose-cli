using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Execution;

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

    internal static PublicationJournalLock Acquire(string path, OperationDeadline? deadline = null,
        IPublicationFaultInjector? faults = null)
    {
        string canonical = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()) { canonical = canonical.ToUpperInvariant(); }
        string name = "aspose-publication-journal-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var mutex = new Mutex(false, name, new NamedWaitHandleOptions
        {
            CurrentUserOnly = true,
            CurrentSessionOnly = false,
        });
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

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        // All call sites are lexical, synchronous journal operations: ownership never crosses a thread switch.
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}