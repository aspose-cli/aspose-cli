using System.Security.Cryptography;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// The documents the viewer service has open. Opening the same file the same
/// way returns the document already being watched, so running <c>preview</c>
/// twice lands on one tab rather than two renders of the same file. Every
/// opener names a holder, and a document stays open until its last holder
/// releases it: the <c>preview</c> command holds a document once however often
/// it opens it, and each App tab holds it on its own, so closing one never
/// takes the document from the other.
/// </summary>
internal sealed class ViewerDocuments : IDisposable
{
    /// <summary>The holder of every document opened by the <c>preview</c> command.</summary>
    public static readonly object PreviewHolder = new();

    private readonly object _gate = new();
    private readonly SemaphoreSlim _opening = new(1, 1);
    private readonly Dictionary<string, LiveDocument> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<object>> _holders = new(StringComparer.Ordinal);
    private readonly RenderWorkerSupervisor _worker;
    private readonly ViewerStorage _storage;
    private readonly LocalServiceResourceLimits _limits;
    private bool _disposed;

    public ViewerDocuments(
        RenderWorkerSupervisor worker,
        ViewerStorage storage,
        LocalServiceResourceLimits limits)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
    }

    /// <summary>Documents in the order they were opened.</summary>
    public IReadOnlyList<LiveDocument> All
    {
        get { lock (_gate) { return _byId.Values.ToArray(); } }
    }

    /// <summary>
    /// Opens a document for <paramref name="holder"/> and renders its first
    /// revision, or hands back the one already open for the same file and options.
    /// </summary>
    public LiveDocument Open(
        string path,
        LiveDocumentOptions options,
        object holder,
        OperationDeadline? deadline = null)
    {
        ArgumentNullException.ThrowIfNull(holder);
        using var owned = deadline is null ? OperationDeadline.Start(_limits.RenderTimeout) : null;
        OperationDeadline operation = deadline ?? owned!;
        _opening.Wait(operation.Token);
        try { return OpenCore(path, options, holder, operation); }
        catch (OperationCanceledException) { operation.ThrowIfExpired("preview-open"); throw; }
        finally { _opening.Release(); }
    }

    private LiveDocument OpenCore(
        string path,
        LiveDocumentOptions options,
        object holder,
        OperationDeadline deadline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        string source = Path.GetFullPath(path);
        if (!File.Exists(source))
        {
            throw CliErrors.FileNotFound(source);
        }

        LiveDocument document;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Find(source, options) is { } reused)
            {
                _holders[reused.Id].Add(holder);
                reused.RecordActivity();
                return reused;
            }

            string id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            document = new LiveDocument(
                id,
                source,
                _storage.CreateDocumentRoot(id),
                _worker,
                options,
                _limits);
            _byId[id] = document;
            _holders[id] = [holder];
        }

        try
        {
            RenderWorkerResponse first = document.Open(deadline);
            if (!first.Ok)
            {
                throw ViewerErrors.FromWorker(first, source);
            }
            return document;
        }
        catch
        {
            Release(document.Id, holder);
            throw;
        }
    }

    public LiveDocument? Find(string id)
    {
        lock (_gate)
        {
            return _byId.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Ends <paramref name="holder"/>'s hold on one document, and closes the
    /// document and reclaims its revisions when no one else holds it.
    /// </summary>
    /// <returns>Whether the holder held the document.</returns>
    public bool Release(string id, object holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        LiveDocument? document;
        lock (_gate)
        {
            if (!_holders.TryGetValue(id, out HashSet<object>? holders) || !holders.Remove(holder))
            {
                return false;
            }
            if (holders.Count > 0)
            {
                return true;
            }
            _holders.Remove(id);
            _byId.Remove(id, out document);
        }
        document?.Dispose();
        return true;
    }

    public void Dispose()
    {
        LiveDocument[] documents;
        lock (_gate)
        {
            _disposed = true;
            documents = _byId.Values.ToArray();
            _byId.Clear();
            _holders.Clear();
        }
        Exception? failure = null;
        foreach (LiveDocument document in documents)
        {
            try { document.Dispose(); } catch (Exception exception) { failure ??= exception; }
        }
        try { _storage.Dispose(); } catch (Exception exception) { failure ??= exception; }
        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    private LiveDocument? Find(string source, LiveDocumentOptions options) =>
        _byId.Values.FirstOrDefault(document =>
            string.Equals(
                document.SourcePath,
                source,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && document.Matches(options));
}
