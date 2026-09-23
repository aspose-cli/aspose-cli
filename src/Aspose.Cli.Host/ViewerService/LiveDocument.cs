using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Host.Viewer;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>How one document is opened in the viewer.</summary>
internal sealed record LiveDocumentOptions
{
    /// <summary>Product id, or null to select the product from the document.</summary>
    public string? Product { get; init; }

    /// <summary>View id, or null for the product's live view.</summary>
    public string? View { get; init; }

    /// <summary>Password of an encrypted document.</summary>
    public string? Password { get; init; }

    /// <summary>Explicit license file, or null for the configured sources.</summary>
    public string? License { get; init; }

    /// <summary>Explicit font directories, or null for the ambient environment.</summary>
    public IReadOnlyList<string>? FontDirectories { get; init; }

    /// <summary>Presentation effect the viewer plays, for example <c>demo</c>.</summary>
    public string? Effect { get; init; }

    /// <summary>Upper bound of rendered parts.</summary>
    public int MaxParts { get; init; } = 512;

    public long MaxInputBytes { get; init; } = ResourceBudgetDefaults.DefaultInputBytes;

    /// <summary>Debounce window for bursts of file changes.</summary>
    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromMilliseconds(120);
}

/// <summary>One published render: an immutable directory and what is in it.</summary>
internal sealed record LiveRevision(
    int Number,
    string Product,
    string View,
    string License,
    int TotalParts,
    IReadOnlyDictionary<string, string> Digests,
    IReadOnlyDictionary<string, string> Addressed,
    ViewBundleManifest Files);

/// <summary>
/// One document open in the viewer. It watches the user's file, renders a
/// private copy of it through the warm worker so a writer is never blocked,
/// publishes each result as an immutable revision directory and announces it
/// to attached viewers. Renders never overlap: a change arriving mid-render
/// marks the document dirty and the loop immediately runs another round. A
/// failed render keeps the last good revision on screen and reports why; a
/// deleted file parks the document until it comes back.
/// </summary>
internal sealed class LiveDocument : IDisposable
{
    private const int UnlockAttempts = 5;
    private const int MessageMaxLength = 200;
    private static readonly TimeSpan UnlockInitialDelay = TimeSpan.FromMilliseconds(50);

    private readonly object _gate = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string _source;
    private readonly string _root;
    private readonly string _copy;
    private readonly RevisionStore _versions;
    private readonly RenderWorkerSupervisor _worker;
    private readonly LiveDocumentOptions _options;
    private readonly LocalServiceResourceLimits _limits;
    private readonly FileChangeMonitor _monitor;
    private readonly Thread _loop;
    private LiveRevision? _current;
    private LiveRevision? _previous;
    private int _revision;
    private long _lastActivityAt;
    private bool _dirty;
    private bool _opened;
    private volatile bool _disposed;
    private bool _preserveStorage;

    public LiveDocument(
        string id,
        string sourcePath,
        string root,
        RenderWorkerSupervisor worker,
        LiveDocumentOptions options,
        LocalServiceResourceLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(limits);

        Id = id;
        _source = Path.GetFullPath(sourcePath);
        _root = PrivateUserStorage.EnsureDirectory(Path.GetFullPath(root));
        // Products label parts after the file they rendered, so the copy
        // carries the document's own name rather than a private one.
        _copy = Path.Combine(PrivateUserStorage.EnsureDirectory(Path.Combine(_root, ViewerStorage.SourceDirectory)), FileName);
        _worker = worker;
        _options = options;
        _limits = limits;
        _versions = new RevisionStore(Path.Combine(_root, ViewerStorage.RevisionsDirectory));
        Events = new LiveEventHub();
        _lastActivityAt = Environment.TickCount64;
        _monitor = new FileChangeMonitor(_source, options.QuietPeriod);
        _monitor.Changed += MarkDirty;
        _loop = new Thread(RenderLoop) { IsBackground = true, Name = "aspose-viewer-document" };
        _loop.Start();
    }

    /// <summary>Opaque id this document is served under.</summary>
    public string Id { get; }

    /// <summary>Event stream every attached viewer listens to.</summary>
    public LiveEventHub Events { get; }

    /// <summary>Absolute path of the watched file.</summary>
    public string SourcePath => _source;

    /// <summary>Name people see in the viewer.</summary>
    public string FileName => Path.GetFileName(_source);

    /// <summary>The effect the viewer plays, for example <c>demo</c>.</summary>
    public string? Effect => _options.Effect;

    /// <summary>The revision being served, or null before the first render.</summary>
    public LiveRevision? Current => Volatile.Read(ref _current);

    /// <summary>
    /// A revision still being served: the current one, or the one before it
    /// while responses opened before the swap are still streaming.
    /// </summary>
    public LiveRevision? Find(int number) =>
        Current is { } current && current.Number == number ? current
            : Volatile.Read(ref _previous) is { } previous && previous.Number == number ? previous
            : null;

    /// <summary>The product presenter, once a render has reported it.</summary>
    public ViewPresentation? Presentation { get; private set; }

    /// <summary>Milliseconds since the last render or viewer activity.</summary>
    public long IdleMilliseconds => Environment.TickCount64 - Volatile.Read(ref _lastActivityAt);

    /// <summary>
    /// Renders the first revision on the calling thread, so opening a document
    /// fails where the person can see it. Afterwards the loop consumes file
    /// changes, including any that arrived while this render ran.
    /// </summary>
    public RenderWorkerResponse Open(OperationDeadline? deadline = null)
    {
        RenderWorkerResponse response = Render(deadline);
        lock (_gate)
        {
            _opened = true;
            Monitor.PulseAll(_gate);
        }
        return response;
    }

    /// <summary>Whether an open request for the same file lands on this document.</summary>
    public bool Matches(LiveDocumentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _options.Product == options.Product
            && _options.View == options.View
            && _options.Effect == options.Effect
            && _options.Password == options.Password
            && _options.License == options.License
            && _options.MaxInputBytes == options.MaxInputBytes
            && (_options.FontDirectories ?? []).SequenceEqual(options.FontDirectories ?? []);
    }

    /// <summary>Renders again as if the file had changed; bursts coalesce.</summary>
    public void Refresh() => MarkDirty();

    /// <summary>Records that a viewer is interacting, which defers idle exit.</summary>
    public void RecordActivity() => Volatile.Write(ref _lastActivityAt, Environment.TickCount64);

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

        _shutdown.Cancel();
        _monitor.Changed -= MarkDirty;
        _monitor.Dispose();
        if (Thread.CurrentThread != _loop) { _loop.Join(); }
        // Cancellation stops the worker before storage can be reclaimed, including an initial Open.
        _renderGate.Wait();
        try
        {
            Events.Dispose();
            if (!_preserveStorage)
            {
                _versions.Dispose();
                LocalFileCleanup.DeleteDirectory(_root);
            }
        }
        finally { _renderGate.Release(); _shutdown.Dispose(); }
    }

    private void MarkDirty()
    {
        int revision;
        bool announce;
        lock (_gate)
        {
            announce = !_dirty && _opened;
            _dirty = true;
            revision = Volatile.Read(ref _revision) + 1;
            Monitor.PulseAll(_gate);
        }
        if (announce)
        {
            Broadcast("rendering", new JsonObject { ["revision"] = revision });
        }
    }

    private void RenderLoop()
    {
        while (true)
        {
            lock (_gate)
            {
                while (!_disposed && !(_dirty && _opened))
                {
                    Monitor.Wait(_gate);
                }
                if (_disposed)
                {
                    return;
                }
                _dirty = false;
            }

            // Outside the gate: a change arriving during the round sets the
            // flag again and the loop immediately runs another round.
            try { _ = Render(); }
            catch (OperationCanceledException)
            {
                if (_disposed) { return; }
                Report(Volatile.Read(ref _revision), ErrorCodes.OperationTimeout.Name, "The render deadline expired.");
            }
            catch (Exception exception)
            {
                // This thread serves every open document of the per-user service; one failed
                // background round must not end the process. The last good revision stays live.
                if (_disposed) { return; }
                Report(Volatile.Read(ref _revision), ErrorCodes.Internal.Name,
                    $"The render failed unexpectedly ({exception.GetType().Name}).");
            }
        }
    }

    private RenderWorkerResponse Render(OperationDeadline? operation = null)
    {
        using var ownedDeadline = operation is null ? OperationDeadline.Start(_limits.RenderTimeout) : null;
        OperationDeadline deadline = operation ?? ownedDeadline!;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, _shutdown.Token);
        _renderGate.Wait(cancellation.Token);
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            int revision = Volatile.Read(ref _revision) + 1;
            Volatile.Write(ref _revision, revision);
            RecordActivity();
            var timer = Stopwatch.StartNew();
            string directory = _versions.CreateVersionDirectory(revision);
            try
            {
                CopySource(cancellation.Token);
                RenderWorkerResponse response = _worker.Render(new RenderWorkerRequest
                {
                    Id = 0,
                    Source = _copy,
                    SourceOrigin = _source,
                    Output = directory,
                    MaxParts = _options.MaxParts,
                    TimeoutMs = (int)(deadline.OriginalBudget ?? _limits.RenderTimeout).TotalMilliseconds,
                    ExpiresAtTick = deadline.ExpiresAtTick,
                    MaxInputBytes = _options.MaxInputBytes,
                    Product = _options.Product,
                    View = _options.View,
                    Password = _options.Password,
                    License = _options.License,
                    FontDirectories = _options.FontDirectories,
                    Presentation = Presentation is null,
                }, cancellation.Token);
                if (!response.Ok)
                {
                    LocalFileCleanup.DeleteDirectory(directory);
                    Report(revision, response.Code ?? ErrorCodes.Internal.Name, response.Message ?? "the render failed");
                    return response;
                }

                deadline.ThrowIfExpired("render-publication");
                cancellation.Token.ThrowIfCancellationRequested();
                Publish(revision, directory, response, timer.ElapsedMilliseconds);
                return response;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CliException
                or InvalidDataException or System.Text.Json.JsonException)
            {
                if (exception is CliException termination && termination.Code == ErrorCodes.WorkerTerminationFailed)
                {
                    _preserveStorage = true;
                    // An unconfirmed producer must also prevent the session owner's stale sweep.
                    try { File.WriteAllText(Path.Combine(_root, ".worker-unconfirmed"), termination.Message); }
                    catch (Exception marker) when (marker is IOException or UnauthorizedAccessException) { }
                }
                else { LocalFileCleanup.DeleteDirectory(directory); }
                string code = exception switch
                {
                    CliException cli => cli.Code.Name,
                    // The worker produced a bundle that failed manifest validation.
                    InvalidDataException or System.Text.Json.JsonException => ErrorCodes.RenderFailed.Name,
                    _ => ErrorCodes.FileNotFound.Name,
                };
                Report(revision, code, exception.Message);
                return new RenderWorkerResponse { Id = 0, Ok = false, Code = code,
                    Exit = (int)(exception is CliException failure ? failure.ExitCode : ExitCode.InputError),
                    Message = exception.Message };
            }
            finally
            {
                RecordActivity();
            }
        }
        finally { _renderGate.Release(); }
    }

    /// <summary>
    /// Renders a copy rather than the watched file: the writer keeps its file,
    /// and the render sees one stable state of it.
    /// </summary>
    private void CopySource(CancellationToken cancellationToken)
    {
        if (!File.Exists(_source))
        {
            throw CliErrors.FileNotFound(_source);
        }
        FileUnlockProbe.WaitReadable(_source, UnlockAttempts, UnlockInitialDelay, cancellationToken);
        using var input = new FileStream(_source, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var output = new FileStream(_copy, FileMode.Create, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
        BoundedStreamCopy.CopyAsync(input, output, _options.MaxInputBytes,
            size => CliErrors.FileTooLarge(size, _options.MaxInputBytes), cancellationToken).GetAwaiter().GetResult();
    }

    private void Publish(int revision, string directory, RenderWorkerResponse response, long renderMs)
    {
        LiveRevision? previous = Current;
        (IReadOnlyDictionary<string, string> digests, IReadOnlyDictionary<string, string> addressed) =
            ReadParts(directory);
        var published = new LiveRevision(
            revision,
            response.Product ?? previous?.Product ?? string.Empty,
            response.View ?? previous?.View ?? string.Empty,
            response.License ?? previous?.License ?? string.Empty,
            response.TotalParts,
            digests,
            addressed,
            ViewBundleManifest.Validate(directory, RenderWorkerProtocol.ManifestFileName, _limits));
        if (response.PresenterScript is { } script)
        {
            Presentation = new ViewPresentation(script, response.PresenterStylesheet);
        }
        Volatile.Write(ref _previous, previous);
        Volatile.Write(ref _current, published);
        var changed = new JsonArray();
        foreach ((string id, string digest) in digests)
        {
            if (previous is null || !previous.Digests.TryGetValue(id, out string? was) || was != digest)
            {
                changed.Add(id);
            }
        }
        Broadcast("update", new JsonObject
        {
            ["revision"] = revision,
            ["renderMs"] = renderMs,
            ["license"] = published.License,
            ["changed"] = previous is null ? null : changed,
        });
        _versions.Prune(revision - 1);
    }

    /// <summary>
    /// Reads what a published revision contains: the digest of every part by
    /// part id, and the file behind every digest. Parts are served by digest,
    /// so a part that survives an edit keeps its address and the viewer never
    /// downloads it again.
    /// </summary>
    private static (IReadOnlyDictionary<string, string> Digests, IReadOnlyDictionary<string, string> Addressed)
        ReadParts(string directory)
    {
        var digests = new Dictionary<string, string>(StringComparer.Ordinal);
        var addressed = new Dictionary<string, string>(StringComparer.Ordinal);
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(
            Path.Combine(directory, RenderWorkerProtocol.ManifestFileName)))
            ?? throw new InvalidDataException("The rendered view manifest is empty.");
        foreach (JsonNode? part in manifest["parts"]!.AsArray())
        {
            string digest = part!["digest"]?.GetValue<string>() ?? string.Empty;
            digests[part["id"]!.GetValue<string>()] = digest;
            addressed[Address(digest)] = part["file"]!.GetValue<string>();
        }
        return (digests, addressed);
    }

    /// <summary>The address a part is served under: the hex of its digest.</summary>
    internal static string Address(string digest) =>
        digest.StartsWith("sha256:", StringComparison.Ordinal) ? digest["sha256:".Length..] : digest;

    private void Report(int revision, string code, string message) =>
        Broadcast("error", new JsonObject
        {
            ["revision"] = revision,
            ["code"] = code,
            ["message"] = Flatten(message.Replace(_source, FileName, StringComparison.OrdinalIgnoreCase)),
        });

    private void Broadcast(string name, JsonObject payload) =>
        Events.Broadcast(name, payload.ToJsonString());

    /// <summary>Messages travel in one event line: flatten and cap them.</summary>
    private static string Flatten(string message)
    {
        string single = message.ReplaceLineEndings(" ").Trim();
        return single.Length <= MessageMaxLength ? single : single[..MessageMaxLength] + "...";
    }

    /// <summary>The hello state a viewer attaches with.</summary>
    public string Hello()
    {
        LiveRevision? current = Current;
        return new JsonObject
        {
            ["revision"] = current?.Number ?? 0,
            ["file"] = FileName,
            ["product"] = current?.Product,
            ["view"] = current?.View,
            ["license"] = current?.License,
            ["effect"] = Effect,
        }.ToJsonString();
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{FileName} (revision {Current?.Number ?? 0})");
}
