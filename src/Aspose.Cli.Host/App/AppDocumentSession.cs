using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.App;

/// <summary>
/// Atomically switches the document the Web App shows. The App renders
/// nothing itself: it opens the file in the viewer service and frames the
/// address that service serves it under, so one renderer serves the App and
/// the preview command alike.
/// </summary>
internal sealed class AppDocumentSession : IDisposable
{
    private readonly object _gate = new();
    // AppHost serializes mutations; this gate protects short immutable-state reads only.
    private readonly Dictionary<string, RetainedResource<OwnedTemporaryFile>> _uploads = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly List<Task> _retiredUploads = [];
    private bool _disposed;
    private readonly ProductCatalog _catalog;
    private readonly Func<CommandContext> _createContext;
    private readonly AppPreferencesStore _preferences;
    private readonly AppLog _log;
    private readonly ViewerDocuments _documents;
    private readonly LocalServiceResourceLimits _limits = LocalServiceResourceLimits.Resolve();
    private readonly Func<string, string> _address;
    private readonly string _root;
    private readonly FontSearchProfile _fontProfile;
    private DocumentLease? _current;

    public event Action? Activity;

    public AppDocumentSession(
        ProductCatalog catalog,
        Func<CommandContext> createContext,
        AppPreferencesStore preferences,
        AppLog log,
        ViewerDocuments documents,
        Func<string, string> address,
        string rootDirectory,
        FontSearchProfile fontProfile)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _createContext = createContext;
        _preferences = preferences;
        _log = log;
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _address = address ?? throw new ArgumentNullException(nameof(address));
        _root = Path.GetFullPath(rootDirectory);
        _fontProfile = fontProfile ?? throw new ArgumentNullException(nameof(fontProfile));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_root);
    }

    public AppDocumentSnapshot? Snapshot
    {
        get { lock (_gate) { return _current?.State; } }
    }

    public string? FileName => Read(static lease => lease.FileName);

    public string? ProductId => Read(static lease => lease.ProductId);

    public void Open(string filePath, bool uploadedCopy, string? displayFileName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string full = Path.GetFullPath(filePath);
        if (!File.Exists(full))
        {
            throw CliErrors.FileNotFound(full);
        }

        CommandContext context = _createContext();
        InputSizeGuard.Ensure(
            context.ResourceBudgets,
            full,
            InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable));
        ProductDefinition product = _catalog.ResolveExistingFile(
            full,
            operation: "app");

        string displayName = Path.GetFileName(displayFileName ?? full);
        DocumentLease next = CreateLease(
            context,
            full,
            displayName,
            uploadedCopy,
            product);
        try
        {
            if (!uploadedCopy)
            {
                _preferences.RecordRecent(full, next.ProductId, next.View);
            }
        }
        catch
        {
            DisposePrevious(next);
            throw;
        }
        DocumentLease? previous;
        lock (_gate)
        {
            previous = _current;
            _current = next;
        }

        DisposePrevious(previous);
        if (previous?.UploadedCopy is true && !string.Equals(previous.Path, full,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        { DiscardUpload(previous.Path); }

        _log.Write(
            $"opened {product.Manifest.Id} file '{Path.GetFileName(full)}' ({next.View})");
    }

    public async Task<string> StoreUploadAsync(
        string fileName,
        Stream input,
        long contentLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        long limit = InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable);
        if (contentLength > limit)
        {
            throw CliErrors.FileTooLarge(contentLength, limit);
        }

        string safeName = Path.GetFileName(fileName);
        if (safeName.Length == 0)
        {
            throw CliErrors.FormatUnsupported(
                string.Empty,
                _catalog.DefaultOwnerFormats());
        }

        UploadPaths paths = CreateUploadPaths();
        FileInfo[] existing = new DirectoryInfo(paths.Files)
            .EnumerateFiles()
            .ToArray();
        long existingBytes = ValidateUploadQuota(
            existing,
            contentLength);
        return await PersistUploadAsync(
            paths,
            safeName,
            input,
            limit,
            existingBytes,
            cancellationToken).ConfigureAwait(false);
    }

    private UploadPaths CreateUploadPaths()
    {
        string uploads = PrivateUserStorage.EnsureDirectory(
            Path.Combine(_root, "uploads"));
        return new UploadPaths(
            PrivateUserStorage.EnsureDirectory(
                Path.Combine(uploads, "files")),
            PrivateUserStorage.EnsureDirectory(
                Path.Combine(uploads, "staging")));
    }

    private long ValidateUploadQuota(
        IReadOnlyCollection<FileInfo> existing,
        long contentLength)
    {
        if (existing.Count >= _limits.MaximumUploadFiles)
        {
            throw CliErrors.UploadBudgetExceeded(
                "file count",
                existing.Count + 1L,
                _limits.MaximumUploadFiles);
        }

        long existingBytes = existing.Aggregate(
            0L,
            static (total, file) =>
                checked(total + file.Length));
        long remaining =
            _limits.MaximumUploadSessionBytes - existingBytes;
        if (contentLength >= 0 && contentLength > remaining)
        {
            throw CliErrors.UploadBudgetExceeded(
                "total bytes",
                checked(existingBytes + contentLength),
                _limits.MaximumUploadSessionBytes);
        }
        return existingBytes;
    }

    private async Task<string> PersistUploadAsync(
        UploadPaths paths,
        string safeName,
        Stream input,
        long fileLimit,
        long existingBytes,
        CancellationToken cancellationToken)
    {
        string id = Guid.NewGuid().ToString("N");
        string staged = Path.Combine(paths.Staging, id + ".upload");
        string destination = Path.Combine(
            paths.Files,
            $"{id}-{safeName}");
        var owned = OwnedTemporaryFile.Create(staged);
        try
        {
            PrivateUserStorage.ProtectFile(staged);
            owned.BindProducedFile();
            await WriteStagedUploadAsync(staged, input, fileLimit, existingBytes, cancellationToken).ConfigureAwait(false);
            owned.MoveTo(destination);
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _uploads.Add(destination, new RetainedResource<OwnedTemporaryFile>(owned));
            }
            return destination;
        }
        catch { owned.Dispose(); throw; }
    }

    private async Task WriteStagedUploadAsync(
        string staged,
        Stream input,
        long fileLimit,
        long existingBytes,
        CancellationToken cancellationToken)
    {
        using var output = new FileStream(staged, FileMode.Open, FileAccess.Write, FileShare.Read);
        long sessionRemaining = Math.Max(
            0,
            _limits.MaximumUploadSessionBytes - existingBytes);
        long copyLimit = Math.Min(
            fileLimit,
            sessionRemaining);
        await BoundedStreamCopy.CopyAsync(
            input,
            output,
            copyLimit,
            total => fileLimit <= sessionRemaining
                ? CliErrors.FileTooLarge(total, fileLimit)
                : CliErrors.UploadBudgetExceeded(
                    "total bytes",
                    checked(existingBytes + total),
                    _limits.MaximumUploadSessionBytes),
            cancellationToken).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Renders the open document again, so a license saved while it was open
    /// reaches what the person is looking at.
    /// </summary>
    public void Refresh()
    {
        DocumentLease? current;
        lock (_gate) { current = _current; }
        if (current is not null)
        {
            _documents.Find(current.DocumentId)?.Refresh();
            Activity?.Invoke();
        }
    }

    public void RefreshPreferences(string productId, string desiredView)
    {
        DocumentLease? current;
        lock (_gate) { current = _current; }
        if (current is not null && current.ProductId == productId && current.View != desiredView)
        { Open(current.Path, current.UploadedCopy, current.FileName); }
    }

    internal void DiscardUpload(string path)
    {
        RetainedResource<OwnedTemporaryFile>? upload;
        lock (_gate)
        {
            if (!_uploads.Remove(path, out upload)) { return; }
            TrackRetirement(upload.Completion);
        }
        upload.Dispose();
    }

    public void ClearUploads()
    {
        DocumentLease? closing = null;
        lock (_gate)
        {
            if (_current?.UploadedCopy is true) { closing = _current; _current = null; }
        }
        DisposePrevious(closing);
        RetireUploads();
    }

    private void RetireUploads()
    {
        RetainedResource<OwnedTemporaryFile>[] uploads;
        lock (_gate)
        {
            uploads = _uploads.Values.ToArray();
            _uploads.Clear();
            foreach (var upload in uploads) { TrackRetirement(upload.Completion); }
        }
        foreach (var upload in uploads) { upload.Dispose(); }
    }

    private void TrackRetirement(Task completion)
    {
        _retiredUploads.RemoveAll(static task => task.IsCompletedSuccessfully);
        _retiredUploads.Add(completion);
    }

    private void DeleteEmptyStorage()
    {
        string uploads = Path.Combine(_root, "uploads");
        foreach (string directory in new[] { Path.Combine(uploads, "files"), Path.Combine(uploads, "staging"), uploads, _root })
        { LocalFileCleanup.DeleteDirectory(directory, recursive: false); }
    }

    public void Dispose()
    {
        DocumentLease? closing;
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            closing = _current;
            _current = null;
        }
        DisposePrevious(closing);
        RetireUploads();
        Task completed;
        lock (_gate) { completed = Task.WhenAll(_retiredUploads); }
        DeferredResourceCleanup.CompleteOrDefer(completed.IsCompletedSuccessfully,
            () => completed.GetAwaiter().GetResult(), DeleteEmptyStorage,
            "aspose-app-upload-cleanup", "retired App upload storage");
    }

    private void DisposePrevious(DocumentLease? previous)
    {
        try { previous?.Dispose(); }
        catch (Exception exception)
        {
            _log.Write($"previous preview cleanup failed: {exception.GetType().Name}");
        }
    }

    /// <summary>
    /// Opens the document in the viewer service and keeps the upload it was
    /// read from alive for as long as the App shows it.
    /// </summary>
    private DocumentLease CreateLease(
        CommandContext context,
        string path,
        string displayName,
        bool uploadedCopy,
        ProductDefinition product)
    {
        string view = _preferences.Current.PreviewView(product);
        RetainedResource<OwnedTemporaryFile>.Lease? input = null;
        lock (_gate)
        {
            if (_uploads.TryGetValue(path, out var upload)) { upload.TryAcquire(out input); }
            if (uploadedCopy && input is null) { throw CliErrors.FileNotFound(path); }
        }
        try
        {
            LiveDocument opened = _documents.Open(path, new LiveDocumentOptions
            {
                Product = product.Manifest.Id,
                View = view,
                FontDirectories = _fontProfile.IsAmbient ? null : _fontProfile.Directories,
            });
            Activity?.Invoke();
            return new DocumentLease(
                path,
                new AppDocumentSnapshot(
                    displayName,
                    uploadedCopy || input is not null,
                    opened.Current?.Product ?? product.Manifest.Id,
                    opened.Current?.View ?? view,
                    _address(opened.Id)),
                opened.Id,
                input,
                _documents);
        }
        catch
        {
            input?.Dispose();
            throw;
        }
    }

    private string? Read(Func<DocumentLease, string> selector)
    {
        lock (_gate)
        {
            return _current is null ? null : selector(_current);
        }
    }

    private sealed record UploadPaths(
        string Files,
        string Staging);

    /// <summary>
    /// One document open in the viewer service on the App's behalf, together
    /// with the upload it was read from. Closing it releases both.
    /// </summary>
    private sealed record DocumentLease(
        string Path,
        AppDocumentSnapshot State,
        string DocumentId,
        IDisposable? Upload,
        ViewerDocuments Documents) : IDisposable
    {
        public string FileName => State.FileName;
        public bool UploadedCopy => State.UploadedCopy;
        public string ProductId => State.ProductId;
        public string View => State.View;

        public void Dispose()
        {
            try
            {
                Documents.Close(DocumentId);
            }
            catch (ObjectDisposedException)
            {
                // The service is ending; the document goes with it.
            }
            finally
            {
                Upload?.Dispose();
            }
        }
    }
}

internal sealed record AppDocumentSnapshot(
    string FileName, bool UploadedCopy, string ProductId, string View, string PreviewUrl);
