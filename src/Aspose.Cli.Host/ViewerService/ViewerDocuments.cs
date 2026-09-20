using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// The documents the viewer service has open. Opening the same file the same
/// way returns the document already being watched, so running <c>preview</c>
/// twice lands on one tab rather than two renders of the same file.
/// </summary>
internal sealed class ViewerDocuments : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LiveDocument> _byId = new(StringComparer.Ordinal);
    private readonly RenderWorkerSupervisor _worker;
    private readonly PreviewSessionStorage _storage;
    private readonly LocalServiceResourceLimits _limits;
    private bool _disposed;

    public ViewerDocuments(
        RenderWorkerSupervisor worker,
        PreviewSessionStorage storage,
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
    /// Opens a document and renders its first revision, or hands back the one
    /// already open for the same file and options.
    /// </summary>
    public LiveDocument Open(string path, LiveDocumentOptions options)
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
                reused.RecordActivity();
                return reused;
            }

            string id = LocalHttpRequestSecurity.RandomToken();
            document = new LiveDocument(
                id,
                source,
                Path.Combine(_storage.Root, id),
                _worker,
                options,
                _limits);
            _byId[id] = document;
        }

        try
        {
            RenderWorkerResponse first = document.Open();
            if (!first.Ok)
            {
                throw ViewerErrors.FromWorker(first, source);
            }
            return document;
        }
        catch
        {
            Close(document.Id);
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

    /// <summary>Closes one document and reclaims its revisions.</summary>
    public bool Close(string id)
    {
        LiveDocument? document;
        lock (_gate)
        {
            if (!_byId.Remove(id, out document))
            {
                return false;
            }
        }
        document.Dispose();
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
        }
        foreach (LiveDocument document in documents)
        {
            document.Dispose();
        }
        _storage.Dispose();
    }

    private LiveDocument? Find(string source, LiveDocumentOptions options) =>
        _byId.Values.FirstOrDefault(document =>
            string.Equals(
                document.SourcePath,
                source,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && document.Matches(options));
}
