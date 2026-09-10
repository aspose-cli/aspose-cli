using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Watches one file and raises a single debounced <see cref="Changed"/> per
/// burst of file-system events. Editors and this CLI alike replace files
/// atomically (write a temp sibling, then move it over the target — the
/// <see cref="IO.SafeFileWriter"/> pattern; Excel performs a similar replace
/// dance), which surfaces as several raw events in quick succession, often a
/// Renamed whose <em>old</em> name is the temp file. The monitor therefore
/// watches the whole parent directory, filters by the exact file name itself
/// (a rename counts when either side matches), and only raises once the file
/// has been quiet for the configured period.
/// </summary>
internal sealed class FileChangeMonitor : IDisposable
{
    private readonly object _gate = new();
    private readonly string _fileName;
    private readonly TimeSpan _quietPeriod;
    private readonly FileSystemWatcher _watcher;
    private readonly Thread _pump;
    private long _lastEventAt;
    private bool _pending;
    private bool _disposed;

    /// <summary>
    /// Starts watching immediately.
    /// </summary>
    /// <param name="filePath">The file to watch; its parent directory must exist.</param>
    /// <param name="quietPeriod">
    /// How long the file must stay quiet after the last event before
    /// <see cref="Changed"/> is raised. Zero raises as soon as the pump sees
    /// the event.
    /// </param>
    public FileChangeMonitor(string filePath, TimeSpan quietPeriod)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(quietPeriod, TimeSpan.Zero);

        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The watched file path has no parent directory.", nameof(filePath));
        _fileName = Path.GetFileName(fullPath);
        _quietPeriod = quietPeriod;

        _watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                | NotifyFilters.Size | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;

        _pump = new Thread(Pump) { IsBackground = true, Name = "aspose-preview-debounce" };
        _pump.Start();
    }

    /// <summary>
    /// Raised once per settled burst of events on the watched file, on a
    /// background thread. Handlers should be quick; the pump does not record
    /// further raises until the handler returns (new events keep accumulating
    /// in the meantime and produce a follow-up raise).
    /// </summary>
    public event Action? Changed;

    /// <summary>Stops watching. Idempotent and safe to call from any thread.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Monitor.PulseAll(_gate);
        }

        _watcher.Dispose();
        if (Thread.CurrentThread != _pump)
        {
            // Best effort: the pump is a background thread, so a handler that
            // is still running cannot block process exit either way.
            _pump.Join(TimeSpan.FromSeconds(1));
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (Matches(e.Name))
        {
            RecordHit();
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        // Either side matching counts: temp-file -> target covers atomic
        // replacement, target -> backup covers Excel's save-replace dance.
        if (Matches(e.Name) || Matches(e.OldName))
        {
            RecordHit();
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e) =>
        // The watcher's internal buffer overflowed and events were lost.
        // Treat it as a change so the preview re-renders instead of going
        // stale — a spurious render is cheap, a missed one is not.
        RecordHit();

    private bool Matches(string? relativeName) =>
        // Case-insensitive on every platform: file systems that are case
        // sensitive would at worst trigger a spurious re-render for a
        // same-named sibling, whereas a case mismatch on Windows would mean
        // missing real changes.
        relativeName is not null
            && string.Equals(Path.GetFileName(relativeName), _fileName, StringComparison.OrdinalIgnoreCase);

    private void RecordHit()
    {
        lock (_gate)
        {
            _lastEventAt = Environment.TickCount64;
            _pending = true;
            Monitor.PulseAll(_gate);
        }
    }

    private void Pump()
    {
        while (true)
        {
            bool raise = false;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (!_pending)
                {
                    Monitor.Wait(_gate);
                }
                else
                {
                    long remaining = (long)_quietPeriod.TotalMilliseconds
                        - (Environment.TickCount64 - _lastEventAt);
                    if (remaining > 0)
                    {
                        // Not quiet yet; sleep the remainder. A new event
                        // during the sleep refreshes _lastEventAt, so the next
                        // iteration recomputes and keeps waiting — that is the
                        // debounce.
                        Monitor.Wait(_gate, (int)Math.Min(remaining, int.MaxValue));
                    }
                    else
                    {
                        _pending = false;
                        raise = true;
                    }
                }
            }

            if (raise)
            {
                // Outside the lock: a slow handler must not stall event
                // recording (hits during the handler simply schedule the next
                // raise).
                Changed?.Invoke();
            }
        }
    }
}
