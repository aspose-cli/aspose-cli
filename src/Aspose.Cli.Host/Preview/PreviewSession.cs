using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Orchestrates one live-preview session over a single watched file: it
/// renders versions through the injected <see cref="PreviewRenderer"/>, swaps
/// immutable <see cref="PreviewSnapshot"/>s for the server to serve, and
/// broadcasts <c>activity</c>/<c>update</c>/<c>status</c> events through the hub (plus a
/// <c>focus</c> event after an update whenever an editing command left a
/// fresh change hint on the sideband). A single
/// background loop performs every change-driven render, so renders never
/// overlap; a change arriving mid-render just marks the session dirty and the
/// loop immediately runs another round. Failures keep the last good snapshot
/// on screen and surface as a <c>status</c> event instead of tearing the
/// session down; a deleted file parks the session until the file reappears.
/// The session does not own either store or the hub; the caller wires and
/// disposes them.
/// </summary>
internal sealed class PreviewSession : IDisposable
{
    private const int UnlockAttempts = 5;
    private const int StatusMessageMaxLength = 200;
    private static readonly TimeSpan UnlockInitialDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan HintMaxAge = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HintRetryDelay = TimeSpan.FromMilliseconds(25);
    internal static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(2);
    private const int HintRetryAttempts = 200;

    private readonly object _gate = new();
    private readonly object _renderLock = new();
    private readonly HashSet<string> _seenHintIds = [];
    private readonly string _filePath;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly PreviewRenderer _renderer;
    private readonly bool _supportsState;
    private readonly Action<ProductPreviewPayload>? _validateHint;
    private readonly PreviewVersionStore _store;
    private readonly PreviewViewPublicationStore _viewPublications;
    private readonly LiveEventHub _hub;
    private readonly LocalServiceResourceLimits _limits;
    private readonly FileChangeMonitor _monitor;
    private readonly Thread _loop;
    private PreviewPublishedState _published = new(
        Snapshot: null,
        Revision: 0,
        State: null);
    private int _revision;
    private long _lastActivityAt;
    private bool _dirty;
    private bool _initialRendered;
    private volatile bool _disposed;

    /// <summary>
    /// Creates the session and starts watching immediately, so a save landing
    /// during the initial render is not lost; the loop only consumes changes
    /// once <see cref="RenderInitial"/> has produced revision 1.
    /// </summary>
    /// <param name="filePath">The document file to monitor and render.</param>
    /// <param name="resourceBudgets">Shared admission and deadline ledger.</param>
    /// <param name="renderer">Renders the file through a bounded artifact sink.</param>
    /// <param name="supportsState">Whether the renderer accepts interactive state.</param>
    /// <param name="store">Version-directory lifecycle; owned by the caller.</param>
    /// <param name="viewPublications">Bounded immutable view lifecycle; owned by the caller.</param>
    /// <param name="hub">Event broadcaster; owned by the caller.</param>
    /// <param name="quietPeriod">Debounce window for file-change bursts.</param>
    public PreviewSession(
        string filePath,
        ResourceBudgetLedger resourceBudgets,
        PreviewRenderer renderer,
        bool supportsState,
        PreviewVersionStore store,
        PreviewViewPublicationStore viewPublications,
        LiveEventHub hub,
        TimeSpan quietPeriod,
        Action<ProductPreviewPayload>? validateHint = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(viewPublications);
        ArgumentNullException.ThrowIfNull(hub);

        _filePath = Path.GetFullPath(filePath);
        _resourceBudgets = resourceBudgets;
        _renderer = renderer;
        _supportsState = supportsState;
        _validateHint = validateHint;
        _store = store;
        _viewPublications = viewPublications;
        _hub = hub;
        _limits = LocalServiceResourceLimits.Resolve();
        _lastActivityAt = Environment.TickCount64;

        _monitor = new FileChangeMonitor(_filePath, quietPeriod);
        _monitor.Changed += MarkDirty;
        _loop = new Thread(RenderLoop) { IsBackground = true, Name = "aspose-preview-session" };
        _loop.Start();
    }

    /// <summary>The snapshot to serve right now; null until the first render completes.</summary>
    public PreviewSnapshot? Current => Published.Snapshot;

    /// <summary>The immutable document snapshot and validated state published together.</summary>
    public PreviewPublishedState Published => Volatile.Read(ref _published);

    /// <summary>Number of the most recent render attempt; 0 before the first.</summary>
    public int Revision => Volatile.Read(ref _revision);

    /// <summary>Last validated browser state for the current published document revision.</summary>
    public PreviewInteractiveState InteractiveState
    {
        get
        {
            PreviewPublishedState published = Published;
            return new PreviewInteractiveState(
                published.Revision,
                published.State);
        }
    }

    /// <summary>
    /// One-line human-oriented notifications about render rounds (for example
    /// <c>revision 3 rendered</c> or <c>render failed: FILE_CORRUPT</c>).
    /// Raised on whichever thread performed the round: the initial render's
    /// calling thread or the background loop. Handlers must therefore be
    /// thread-safe. Diagnostics only: the command layer forwards them to
    /// stderr; they are not part of any contract.
    /// </summary>
    public event Action<string>? Diagnostic;

    /// <summary>
    /// Renders revision 1 synchronously on the calling thread. Failures
    /// propagate unchanged; a session that cannot produce its first version
    /// should not start serving at all. Once this returns, the background
    /// loop starts consuming file changes (including any that accumulated
    /// while the initial render ran).
    /// </summary>
    public PreviewRenderOutcome RenderInitial()
    {
        PreviewRenderOutcome outcome = RenderDocumentRound();
        lock (_gate)
        {
            _initialRendered = true;
            Monitor.PulseAll(_gate);
        }

        return outcome;
    }

    /// <summary>
    /// Requests a render round as if the file had changed. Asynchronous: the
    /// background loop picks the request up, so overlapping calls coalesce
    /// exactly like a burst of file events.
    /// </summary>
    public void RenderNow() => QueueRender();

    /// <summary>
    /// Synchronously renders validated product-owned state into a new opaque,
    /// immutable view publication. The publication never replaces
    /// <see cref="Current"/> and is not reused by document refreshes.
    /// </summary>
    public PreviewViewPublicationStore.PreviewViewLease PublishView(
        ProductPreviewPayload state,
        int sourceRevision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceRevision);
        ProductPreviewPayload immutableState = state with
        {
            Payload = state.Payload.Clone(),
        };

        lock (_renderLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            PreviewPublishedState published = Published;
            int currentRevision = published.Revision;
            if (sourceRevision != currentRevision)
            {
                throw new CliException(
                    ErrorCodes.PreviewStateStale,
                    $"Preview state revision {sourceRevision} is stale; the current revision is {currentRevision}.",
                    hint: "Retry the same navigation intent against the current preview revision.",
                    details: new JsonObject
                    {
                        ["requestedRevision"] = sourceRevision,
                        ["currentRevision"] = currentRevision,
                    });
            }
            if (!File.Exists(_filePath))
            {
                throw CliErrors.FileNotFound(_filePath);
            }

            FileUnlockProbe.WaitReadable(
                _filePath,
                UnlockAttempts,
                UnlockInitialDelay);
            _resourceBudgets.AdmitFile(_filePath);
            RecordActivity();
            if (!_supportsState)
            {
                throw new InvalidOperationException(
                    "This product does not render interactive preview state.");
            }
            PreviewViewPublicationStore.PreviewViewLease publication =
                _viewPublications.Publish(
                    sourceRevision,
                    sink => _renderer(new PreviewRenderContext(
                        sink,
                        immutableState)));
            Volatile.Write(
                ref _published,
                published with { State = immutableState });
            RecordActivity();
            return publication;
        }
    }

    /// <summary>
    /// Blocks the calling thread until the session should end and reports
    /// why: the token was signalled (<see cref="WaitOutcome.CancelRequested"/>),
    /// the deadline elapsed (<see cref="WaitOutcome.DeadlineExpired"/>), no
    /// clients were connected and nothing rendered for
    /// <paramref name="idleAfter"/> (<see cref="WaitOutcome.IdleExpired"/>),
    /// or the session was disposed (<see cref="WaitOutcome.Completed"/>).
    /// </summary>
    /// <param name="deadline">Overall wall-clock budget from now; null runs unbounded.</param>
    /// <param name="idleAfter">
    /// Idle window; <see cref="Timeout.InfiniteTimeSpan"/> or any
    /// non-positive value disables the idle watchdog.
    /// </param>
    /// <param name="cancellation">Signalled by the caller to end the session (e.g. Ctrl+C).</param>
    public WaitOutcome Wait(TimeSpan? deadline, TimeSpan idleAfter, CancellationToken cancellation)
    {
        long startedAt = Environment.TickCount64;
        bool idleEnabled = idleAfter > TimeSpan.Zero;

        while (true)
        {
            if (cancellation.IsCancellationRequested)
            {
                return WaitOutcome.CancelRequested;
            }

            if (_disposed)
            {
                return WaitOutcome.Completed;
            }

            if (deadline is { } budget && Environment.TickCount64 - startedAt >= (long)budget.TotalMilliseconds)
            {
                return WaitOutcome.DeadlineExpired;
            }

            if (idleEnabled
                && _hub.ClientCount == 0
                && Environment.TickCount64 - Volatile.Read(ref _lastActivityAt) >= (long)idleAfter.TotalMilliseconds)
            {
                return WaitOutcome.IdleExpired;
            }

            // Polling beats juggling four wake sources here: 50 ms keeps every
            // exit path prompt at no measurable cost for a long-running preview.
            if (cancellation.WaitHandle.WaitOne(WaitPollInterval))
            {
                return WaitOutcome.CancelRequested;
            }
        }
    }

    /// <summary>
    /// Stops the monitor and the render loop. Idempotent. The store and the
    /// hub stay alive; the caller owns their teardown order.
    /// </summary>
    public void Dispose()
    {
        _ = Stop(DefaultStopTimeout);
    }

    /// <summary>
    /// Requests shutdown and waits only for the supplied bound. A false result
    /// means the owner must keep the stores and event hub alive until
    /// <see cref="WaitUntilStopped"/> completes on a background cleanup path.
    /// </summary>
    internal bool Stop(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        RequestStop();
        var remaining = Stopwatch.StartNew();
        if (Thread.CurrentThread != _loop
            && !_loop.Join(timeout))
        {
            return false;
        }

        TimeSpan lockTimeout = timeout - remaining.Elapsed;
        if (lockTimeout < TimeSpan.Zero)
        {
            lockTimeout = TimeSpan.Zero;
        }
        if (!Monitor.TryEnter(_renderLock, lockTimeout))
        {
            return false;
        }
        Monitor.Exit(_renderLock);
        return true;
    }

    /// <summary>Waits for a previously requested stop away from user flow.</summary>
    internal void WaitUntilStopped()
    {
        if (Thread.CurrentThread != _loop)
        {
            _loop.Join();
        }
        lock (_renderLock)
        {
            // Acquiring the render gate proves that document and state
            // publication have both left their owned stores.
        }
    }

    private void RequestStop()
    {
        bool disposeMonitor;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            disposeMonitor = true;
            Monitor.PulseAll(_gate);
        }

        if (disposeMonitor)
        {
            _monitor.Changed -= MarkDirty;
            _monitor.Dispose();
        }
    }

    private void MarkDirty() => QueueRender();

    private void QueueRender()
    {
        bool announce;
        int revision;
        lock (_gate)
        {
            announce = !_dirty && _initialRendered;
            _dirty = true;
            revision = Volatile.Read(ref _revision) + 1;
            Monitor.PulseAll(_gate);
        }

        if (announce)
        {
            var activity = new JsonObject
            {
                ["revision"] = revision,
                ["state"] = "rendering",
                ["scope"] = "document",
            };
            _hub.Broadcast("activity", activity.ToJsonString());
        }
    }

    private void RenderLoop()
    {
        while (true)
        {
            lock (_gate)
            {
                while (!_disposed && !(_dirty && _initialRendered))
                {
                    Monitor.Wait(_gate);
                }

                if (_disposed)
                {
                    return;
                }

                _dirty = false;
            }

            // Outside the gate: a change arriving during the round re-sets
            // _dirty and the loop immediately runs again.
            RunChangeRound();
        }
    }

    private void RunChangeRound()
    {
        if (!File.Exists(_filePath))
        {
            // The file disappeared (deleted, or mid-replace). Stay alive, keep
            // serving the last good snapshot; the watcher's Created/Renamed
            // event marks the session dirty again when it comes back.
            BroadcastStatus(Revision, ErrorCodes.FileNotFound.Name, $"Waiting for {Path.GetFileName(_filePath)} to reappear.");
            return;
        }

        // Advisory: writers often still hold the file right after the change
        // event. If it never frees up within the budget, the render is
        // attempted anyway and its failure reports through the status path.
        FileUnlockProbe.WaitReadable(_filePath, UnlockAttempts, UnlockInitialDelay);

        try
        {
            // A watched file changing between render rounds is the feature,
            // not a TOCTOU violation. Admit the newly stable revision here;
            // InputSource still verifies that it does not change again
            // between this boundary and the engine read.
            _resourceBudgets.AdmitFile(_filePath);
            RenderDocumentRound();
        }
        catch (Exception ex)
        {
            string code = ex is CliException cli ? cli.Code.Name : ErrorCodes.Internal.Name;
            string safeMessage = ex.Message.Replace(_filePath, Path.GetFileName(_filePath), StringComparison.OrdinalIgnoreCase);
            BroadcastStatus(Revision, code, safeMessage);
            Diagnostic?.Invoke($"render failed: {code}");
        }
    }

    /// <summary>
    /// One render attempt: burn the next revision number, render into a fresh
    /// version directory (timing the renderer for the update event's
    /// <c>renderMs</c>), swap the snapshot, announce it, prune old versions.
    /// Throws on failure; the revision number stays burnt (status events
    /// report it) but the previous snapshot keeps serving.
    /// </summary>
    private PreviewRenderOutcome RenderDocumentRound()
    {
        lock (_renderLock)
        {
            int revision = Volatile.Read(ref _revision) + 1;
            Volatile.Write(ref _revision, revision);
            RecordActivity();

            string directory = _store.CreateVersionDirectory(revision);
            PreviewRenderOutcome outcome;
            var renderTimer = Stopwatch.StartNew();
            try
            {
                var sink = new BoundedPreviewArtifactSink(directory, _limits);
                outcome = _renderer(new PreviewRenderContext(sink));
                sink.EnsureComplete();
                PrivateUserStorage.ProtectTree(directory);
                PreviewArtifactManifest manifest =
                    PreviewArtifactManifest.Validate(
                        directory,
                        outcome.EntryFileName,
                        _limits);
                outcome = outcome with
                {
                    EntryFileName = manifest.EntryFileName,
                };
                PreviewSnapshot snapshot = BuildSnapshot(
                    revision,
                    directory,
                    manifest,
                    _limits);
                Volatile.Write(
                    ref _published,
                    new PreviewPublishedState(
                        snapshot,
                        revision,
                        State: null));
            }
            catch
            {
                // Do not let failed attempts pile up on disk; the exception
                // itself propagates to the caller/status path untouched.
                LocalFileCleanup.DeleteDirectory(directory);
                throw;
            }

            renderTimer.Stop();
            RecordActivity();

            var update = new JsonObject
            {
                ["revision"] = revision,
                ["scope"] = "document",
                ["renderMs"] = renderTimer.ElapsedMilliseconds,
            };
            _hub.Broadcast("update", update.ToJsonString());
            ScheduleFocusHint(revision);
            Diagnostic?.Invoke(string.Create(CultureInfo.InvariantCulture, $"revision {revision} rendered"));

            // Keep the current and the previous version: responses may still
            // be streaming assets of revision-1 while this swap happens.
            _store.Prune(revision - 1);
            return outcome;
        }
    }

    /// <summary>
    /// Forwards a fresh edit hint (see <see cref="PreviewHintChannel"/>) as a
    /// <c>focus</c> event, right after the snapshot swap it belongs to was
    /// announced by its <c>update</c>. Strictly best-effort: a missing,
    /// stale, corrupt or already-seen hint is skipped silently, and no
    /// failure here may ever disturb the render loop. If atomic publication
    /// wins the race with sideband publication, a bounded asynchronous retry
    /// accepts only a hint for the still-current revision. Hint consumption
    /// runs under the render lock, which also guards the seen-id set.
    /// </summary>
    private void ScheduleFocusHint(int revision)
    {
        if (TryBroadcastFocusHint(revision) || !_initialRendered)
        {
            return;
        }

        // Atomic publication can wake the file monitor just before the
        // successful editing command writes its decorative sideband hint.
        // Retry away from the render thread so this race cannot make focus
        // delivery flaky or add latency to snapshot publication.
        _ = RetryFocusHintAsync(revision);
    }

    private async Task RetryFocusHintAsync(int revision)
    {
        for (int attempt = 0;
             attempt < HintRetryAttempts && !_disposed;
             attempt++)
        {
            await Task.Delay(HintRetryDelay).ConfigureAwait(false);
            if (revision != Revision || _disposed)
            {
                return;
            }
            if (TryBroadcastFocusHint(revision))
            {
                return;
            }
        }
    }

    private bool TryBroadcastFocusHint(int revision)
    {
        try
        {
            lock (_renderLock)
            {
                if (revision != Revision
                    || !PreviewHintChannel.TryConsume(
                        _filePath,
                        HintMaxAge,
                        _seenHintIds,
                        out PreviewHint? hint))
                {
                    return false;
                }

                var targets = new JsonArray();
                foreach (ProductPreviewPayload target in hint.Targets)
                {
                    _validateHint?.Invoke(target);
                    targets.Add(
                        PreviewHintChannel.CreateTransportObject(target));
                }

                var focus = new JsonObject
                {
                    ["revision"] = revision,
                    ["targets"] = targets,
                };
                _hub.Broadcast("focus", focus.ToJsonString());
                return true;
            }
        }
        catch (Exception)
        {
            // The spotlight is decorative; rendering must never pay for it.
            return false;
        }
    }

    /// <summary>
    /// A render that produced exactly one file (an <c>.html</c> entry
    /// document) is held in memory and its directory deleted (an inline
    /// snapshot): requests are then served without touching the disk.
    /// Anything else stays a directory snapshot served from disk: multi-file
    /// renders with their satellite assets, and any non-HTML entry. The
    /// image view's single PNG frame must be streamed as bytes, never read
    /// as a string.
    /// </summary>
    private static PreviewSnapshot BuildSnapshot(
        int revision,
        string directory,
        PreviewArtifactManifest manifest,
        LocalServiceResourceLimits limits)
    {
        string entryFileName = manifest.EntryFileName;
        bool singleHtmlEntryFile =
            manifest.FileCount == 1
            && manifest.EntryLength
                <= limits.MaximumInlineHtmlBytes
            && entryFileName.EndsWith(
                ".html",
                StringComparison.OrdinalIgnoreCase);
        if (!singleHtmlEntryFile)
        {
            return new PreviewSnapshot(
                revision,
                directory,
                entryFileName,
                InlineHtml: null)
            {
                ArtifactManifest = manifest,
            };
        }

        string html;
        using (FileStream entry =
               manifest.TryOpenRead(entryFileName)
               ?? throw new InvalidDataException(
                   "The validated preview entry disappeared before publication."))
        using (var reader = new StreamReader(entry))
        {
            html = reader.ReadToEnd();
        }

        LocalFileCleanup.DeleteDirectory(directory);
        return new PreviewSnapshot(revision, DirectoryPath: null, entryFileName, html);
    }

    private void BroadcastStatus(int revision, string code, string message)
    {
        var status = new JsonObject
        {
            ["revision"] = revision,
            ["state"] = "error",
            ["code"] = code,
            ["message"] = ToSingleLine(message),
        };
        _hub.Broadcast("status", status.ToJsonString());
    }

    /// <summary>Status messages travel in one SSE data line: flatten and cap them.</summary>
    private static string ToSingleLine(string message)
    {
        string flattened = message.ReplaceLineEndings(" ").Trim();
        return flattened.Length <= StatusMessageMaxLength
            ? flattened
            : flattened[..StatusMessageMaxLength] + "...";
    }

    private void RecordActivity() => Volatile.Write(ref _lastActivityAt, Environment.TickCount64);

}
