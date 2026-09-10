using System.Diagnostics;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Owns a bounded set of immutable, opaque view publications for one preview
/// session. A publication is never replaced: navigation state creates a new
/// token, while the watched document continues to use its independent current
/// snapshot.
/// </summary>
internal sealed class PreviewViewPublicationStore : IDisposable
{
    internal const int DefaultMaximumEntries = 16;
    internal static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(30);

    private readonly object _gate = new();
    private readonly object _publishGate = new();
    private readonly string _root;
    private readonly LocalServiceResourceLimits _limits;
    private readonly int _maximumEntries;
    private readonly long _maximumBytes;
    private readonly long _lifetimeTicks;
    private readonly Func<long> _timestampNow;
    private readonly Timer _expiryTimer;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingCleanup = new(
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
    private long _accessSequence;
    private long _totalBytes;
    private bool _disposed;

    public PreviewViewPublicationStore(
        string rootDirectory,
        LocalServiceResourceLimits limits)
        : this(
            rootDirectory,
            DefaultMaximumEntries,
            limits ?? throw new ArgumentNullException(nameof(limits)),
            DefaultLifetime,
            static () => Stopwatch.GetTimestamp(),
            StopwatchTicks(DefaultLifetime))
    {
    }

    internal PreviewViewPublicationStore(
        string rootDirectory,
        int maximumEntries,
        long maximumBytes,
        TimeSpan lifetime,
        Func<DateTimeOffset> utcNow)
        : this(
            rootDirectory,
            maximumEntries,
            TestLimits(maximumBytes),
            lifetime,
            UtcTicks(utcNow),
            lifetime.Ticks)
    {
    }

    private PreviewViewPublicationStore(
        string rootDirectory,
        int maximumEntries,
        LocalServiceResourceLimits limits,
        TimeSpan lifetime,
        Func<long> timestampNow,
        long lifetimeTicks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ArgumentNullException.ThrowIfNull(limits);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        _root = Path.GetFullPath(rootDirectory);
        _limits = limits;
        _maximumEntries = maximumEntries;
        _maximumBytes = limits.MaximumSnapshotBytes;
        _lifetimeTicks = lifetimeTicks;
        _timestampNow = timestampNow
            ?? throw new ArgumentNullException(nameof(timestampNow));
        PrivateUserStorage.EnsureDirectory(_root);
        TimeSpan sweepInterval = lifetime < TimeSpan.FromMinutes(1)
            ? lifetime
            : TimeSpan.FromMinutes(1);
        _expiryTimer = new Timer(
            static state => ((PreviewViewPublicationStore)state!).SweepExpired(),
            this,
            sweepInterval,
            sweepInterval);
    }

    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Materializes and atomically admits one immutable publication. Rendering
    /// happens outside the store lock; admission evicts only inactive LRU
    /// entries and never lets count or bytes exceed their configured bounds.
    /// </summary>
    public PreviewViewLease Publish(
        int sourceRevision,
        Func<IPreviewArtifactSink, PreviewRenderOutcome> materialize)
    {
        ArgumentNullException.ThrowIfNull(materialize);
        lock (_publishGate)
        {
            return PublishExclusive(sourceRevision, materialize);
        }
    }

    private PreviewViewLease PublishExclusive(
        int sourceRevision,
        Func<IPreviewArtifactSink, PreviewRenderOutcome> materialize)
    {
        RetryPendingCleanup();
        lock (_gate)
        {
            ThrowIfDisposed();
        }
        string token = LocalHttpRequestSecurity.RandomToken();
        string ownedDirectory = Path.Combine(_root, token);
        try
        {
            PrivateUserStorage.EnsureDirectory(ownedDirectory);
            var sink = new BoundedPreviewArtifactSink(
                ownedDirectory,
                _limits);
            PreviewRenderOutcome outcome = materialize(sink)
                ?? throw new InvalidDataException(
                    "A preview view renderer returned no outcome.");
            sink.EnsureComplete();
            PrivateUserStorage.ProtectTree(ownedDirectory);
            PreviewArtifactManifest manifest = PreviewArtifactManifest.Validate(
                ownedDirectory,
                outcome.EntryFileName,
                _limits);
            var snapshot = new PreviewSnapshot(
                sourceRevision,
                ownedDirectory,
                manifest.EntryFileName,
                InlineHtml: null)
            {
                ArtifactManifest = manifest,
            };
            PreviewViewPublication publication = CreatePublication(
                token,
                ownedDirectory,
                snapshot);
            long bytes = SnapshotBytes(snapshot);
            List<PreviewViewPublication> discarded = [];
            try
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    long now = _timestampNow();
                    CollectExpired(now, discarded);
                    if (bytes > _maximumBytes)
                    {
                        throw CliErrors.PreviewBudgetExceeded(
                            "view publication bytes",
                            bytes,
                            _maximumBytes);
                    }

                    while (_entries.Count >= _maximumEntries
                        || checked(_totalBytes + bytes) > _maximumBytes)
                    {
                        Entry? victim = _entries.Values
                            .Where(static entry => entry.ActiveReaders == 0)
                            .OrderBy(static entry => entry.LastAccess)
                            .ThenBy(static entry => entry.Publication.Token, StringComparer.Ordinal)
                            .FirstOrDefault();
                        if (victim is null)
                        {
                            throw CliErrors.PreviewBudgetExceeded(
                                "active view publications",
                                _entries.Count + 1,
                                _maximumEntries);
                        }

                        Remove(victim, discarded);
                    }

                    var entry = new Entry(
                        publication,
                        bytes,
                        now,
                        ++_accessSequence);
                    _entries.Add(token, entry);
                    _totalBytes = checked(_totalBytes + bytes);
                    entry.ActiveReaders++;
                    return CreateLease(entry);
                }
            }
            finally
            {
                DeletePublications(discarded);
            }
        }
        catch
        {
            DeleteOwnedDirectory(ownedDirectory);
            throw;
        }
    }

    /// <summary>
    /// Acquires a response-scoped lease. Eviction and expiry may retire an
    /// entry, but its files are not deleted until the final active response
    /// releases the lease.
    /// </summary>
    public bool TryAcquire(
        string token,
        out PreviewViewLease? lease)
    {
        RetryPendingCleanup();
        lease = null;
        if (!IsToken(token))
        {
            return false;
        }

        List<PreviewViewPublication> discarded = [];
        bool acquired = false;
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            CollectExpired(_timestampNow(), discarded);
            if (!_entries.TryGetValue(token, out Entry? entry)
                || entry.Retired)
            {
                acquired = false;
            }
            else
            {
                entry.ActiveReaders++;
                entry.LastAccess = ++_accessSequence;
                lease = CreateLease(entry);
                acquired = true;
            }
        }

        DeletePublications(discarded);
        return acquired;
    }

    public void Dispose()
    {
        _expiryTimer.Dispose();
        List<PreviewViewPublication> discarded = [];
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (Entry entry in _entries.Values.ToArray())
            {
                entry.Retired = true;
                if (entry.ActiveReaders == 0)
                {
                    Remove(entry, discarded);
                }
            }

        }

        DeletePublications(discarded);
    }

    internal static bool IsToken(string? token) =>
        token is { Length: 64 }
        && token.All(static character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

    private void Release(Entry entry)
    {
        PreviewViewPublication? discarded = null;
        lock (_gate)
        {
            if (entry.ActiveReaders <= 0)
            {
                return;
            }

            entry.ActiveReaders--;
            if (entry.ActiveReaders == 0 && entry.Retired)
            {
                var publications = new List<PreviewViewPublication>(1);
                Remove(entry, publications);
                discarded = publications.SingleOrDefault();
            }

        }

        if (discarded is not null)
        {
            DeletePublication(discarded);
        }
    }

    private PreviewViewLease CreateLease(Entry entry) => new(
        entry.Publication,
        () => Release(entry));

    private void CollectExpired(
        long now,
        List<PreviewViewPublication> discarded)
    {
        foreach (Entry entry in _entries.Values.ToArray())
        {
            if (now < entry.CreatedAt
                || now - entry.CreatedAt < _lifetimeTicks)
            {
                continue;
            }

            entry.Retired = true;
            if (entry.ActiveReaders == 0)
            {
                Remove(entry, discarded);
            }
        }
    }

    private void SweepExpired()
    {
        RetryPendingCleanup();
        List<PreviewViewPublication> discarded = [];
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            CollectExpired(_timestampNow(), discarded);
        }

        DeletePublications(discarded);
    }

    private void Remove(
        Entry entry,
        List<PreviewViewPublication> discarded)
    {
        if (!_entries.Remove(entry.Publication.Token))
        {
            return;
        }

        _totalBytes -= entry.Bytes;
        discarded.Add(entry.Publication);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static PreviewViewPublication CreatePublication(
        string token,
        string ownedDirectory,
        PreviewSnapshot snapshot)
    {
        string owned = NormalizeDirectory(ownedDirectory);
        if (snapshot.InlineHtml is not null
            || snapshot.DirectoryPath is not { } snapshotDirectory
            || !DirectoriesEqual(snapshotDirectory, owned)
            || snapshot.ArtifactManifest is not { } manifest
            || !DirectoriesEqual(manifest.Root, owned))
        {
            throw new InvalidDataException(
                "A directory-backed view snapshot must use its store-owned token directory.");
        }

        return new PreviewViewPublication(token, owned, snapshot);
    }

    private static bool DirectoriesEqual(
        string left,
        string right) =>
        string.Equals(
            NormalizeDirectory(left),
            NormalizeDirectory(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string NormalizeDirectory(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static long SnapshotBytes(PreviewSnapshot snapshot)
    {
        if (snapshot.ArtifactManifest is { } manifest)
        {
            return manifest.TotalBytes;
        }

        throw new InvalidDataException(
            "A directory-backed view publication requires a validated artifact manifest.");
    }

    private static Func<long> UtcTicks(
        Func<DateTimeOffset> utcNow)
    {
        ArgumentNullException.ThrowIfNull(utcNow);
        return () => utcNow().UtcTicks;
    }

    private static long StopwatchTicks(TimeSpan lifetime) =>
        checked((long)Math.Ceiling(
            lifetime.TotalSeconds * Stopwatch.Frequency));

    private static LocalServiceResourceLimits TestLimits(long maximumBytes) =>
        new(
            MaximumConcurrentRequests: 4,
            MaximumSseClients: 4,
            SseQueueCapacity: 4,
            SseWriteTimeout: TimeSpan.FromSeconds(1),
            MaximumSnapshotFiles: 128,
            MaximumSnapshotFileBytes: maximumBytes,
            MaximumSnapshotBytes: maximumBytes,
            MaximumInlineHtmlBytes: maximumBytes,
            MaximumUploadFiles: 4,
            MaximumUploadSessionBytes: maximumBytes);

    private void DeletePublications(
        IEnumerable<PreviewViewPublication> publications)
    {
        foreach (PreviewViewPublication publication in publications)
        {
            DeletePublication(publication);
        }
    }

    private void DeletePublication(
        PreviewViewPublication publication) =>
        DeleteOwnedDirectory(publication.OwnedDirectory);

    private void DeleteOwnedDirectory(string ownedDirectory)
    {
        string normalized;
        try
        {
            normalized = NormalizeDirectory(ownedDirectory);
            string? parent = Path.GetDirectoryName(normalized);
            if (parent is null
                || !PreviewOwnedDirectory.CanDelete(
                    _root,
                    normalized,
                    IsToken))
            {
                QueueCleanup(normalized);
                return;
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            QueueCleanup(ownedDirectory);
            return;
        }

        if (LocalFileCleanup.DeleteDirectory(normalized))
        {
            lock (_gate)
            {
                _pendingCleanup.Remove(normalized);
            }
            return;
        }

        QueueCleanup(normalized);
    }

    private void RetryPendingCleanup()
    {
        string[] pending;
        lock (_gate)
        {
            pending = _pendingCleanup.ToArray();
        }

        foreach (string path in pending)
        {
            DeleteOwnedDirectory(path);
        }
    }

    private void QueueCleanup(string path)
    {
        lock (_gate)
        {
            _pendingCleanup.Add(path);
        }
    }

    private sealed class Entry(
        PreviewViewPublication publication,
        long bytes,
        long createdAt,
        long lastAccess)
    {
        public PreviewViewPublication Publication { get; } = publication;
        public long Bytes { get; } = bytes;
        public long CreatedAt { get; } = createdAt;
        public long LastAccess { get; set; } = lastAccess;
        public int ActiveReaders { get; set; }
        public bool Retired { get; set; }
    }

    internal sealed class PreviewViewLease : IDisposable
    {
        private Action? _release;

        internal PreviewViewLease(
            PreviewViewPublication publication,
            Action release)
        {
            Publication = publication;
            _release = release;
        }

        internal PreviewViewPublication Publication { get; }

        public string Token => Publication.Token;

        public string OwnedDirectory => Publication.OwnedDirectory;

        public PreviewSnapshot Snapshot => Publication.Snapshot;

        public void Dispose() =>
            Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

/// <summary>An immutable view snapshot addressed only by its opaque token.</summary>
internal sealed record PreviewViewPublication(
    string Token,
    string OwnedDirectory,
    PreviewSnapshot Snapshot);
