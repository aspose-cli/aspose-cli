using Aspose.Cli.Host.Catalog;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.App;

/// <summary>Atomically switches the file rendered inside the Web App.</summary>
internal sealed class AppDocumentSession : IDisposable
{
    private readonly object _gate = new();
    private readonly ProductCatalog _catalog;
    private readonly AppLicenseState _licenses;
    private readonly AppPreferencesStore _preferences;
    private readonly AppLog _log;
    private readonly string _root;
    private readonly FontSearchProfile _fontProfile;
    private readonly LocalServiceResourceLimits _limits =
        LocalServiceResourceLimits.Resolve();
    private AppPreviewMount? _mount;
    private PreviewLease? _current;

    public event Action? Activity;

    public AppDocumentSession(
        ProductCatalog catalog,
        AppLicenseState licenses,
        AppPreferencesStore preferences,
        AppLog log,
        string rootDirectory,
        FontSearchProfile fontProfile)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _licenses = licenses;
        _preferences = preferences;
        _log = log;
        _root = Path.GetFullPath(rootDirectory);
        _fontProfile = fontProfile ?? throw new ArgumentNullException(nameof(fontProfile));
        Directory.CreateDirectory(_root);
    }

    public string? FileName => Read(static lease => lease.FileName);

    public string? PreviewUrl => Read(static lease => lease.Url);

    public string? ProductId => Read(static lease => lease.ProductId);

    public bool UploadedCopy
    {
        get
        {
            lock (_gate)
            {
                return _current?.UploadedCopy ?? false;
            }
        }
    }

    public string? OriginalFilePath
    {
        get
        {
            lock (_gate)
            {
                return _current?.UploadedCopy is false ? _current.Path : null;
            }
        }
    }

    public void ConfigureMount(AppPreviewMount mount)
    {
        ArgumentNullException.ThrowIfNull(mount);
        lock (_gate)
        {
            if (_mount is not null)
            {
                throw new InvalidOperationException(
                    "The App preview mount is already configured.");
            }

            _mount = mount;
        }
    }

    public bool RoutePreview(
        System.Net.HttpListenerContext context,
        string path)
    {
        lock (_gate)
        {
            if (_current is null)
            {
                return false;
            }

            AppPreviewMount mount = _mount
                ?? throw new InvalidOperationException(
                    "The App preview mount is not configured.");
            return _current.Runtime.Route(
                context,
                mount.Port,
                path);
        }
    }

    public void Open(string filePath, bool uploadedCopy, string? displayFileName = null)
    {
        string full = Path.GetFullPath(filePath);
        if (!File.Exists(full))
        {
            throw CliErrors.FileNotFound(full);
        }

        CommandContext context = _licenses.CreatePreviewContext();
        InputSizeGuard.Ensure(
            context.ResourceBudgets,
            full,
            InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable));
        ProductDefinition product = _catalog.ResolveExistingFile(
            full,
            operation: "app");

        string displayName = Path.GetFileName(displayFileName ?? full);
        PreviewLease next = CreateLease(
            context,
            full,
            displayName,
            uploadedCopy,
            product);
        PreviewLease? previous;
        lock (_gate)
        {
            previous = _current;
            _current = next;
        }

        previous?.Dispose();
        if (previous?.UploadedCopy is true
            && !string.Equals(
                previous.Path,
                full,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)
            && !LocalFileCleanup.DeleteFile(previous.Path))
        {
            _log.Write(
                "superseded upload cleanup failed");
        }

        if (!uploadedCopy)
        {
            _preferences.RecordRecent(
                full,
                next.ProductId,
                next.View);
        }

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
        try
        {
            await WriteStagedUploadAsync(
                staged,
                input,
                fileLimit,
                existingBytes,
                cancellationToken).ConfigureAwait(false);
            File.Move(staged, destination);
            return destination;
        }
        catch
        {
            if (!LocalFileCleanup.DeleteFile(staged))
            {
                _log.Write("upload staging cleanup failed");
            }
            throw;
        }
    }

    private async Task WriteStagedUploadAsync(
        string staged,
        Stream input,
        long fileLimit,
        long existingBytes,
        CancellationToken cancellationToken)
    {
        using FileStream output = PrivateUserStorage.CreateFile(staged);
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

    public void ReopenForLicenseOrPreferences()
    {
        string? path;
        string? displayName;
        bool uploaded;
        lock (_gate)
        {
            path = _current?.Path;
            displayName = _current?.FileName;
            uploaded = _current?.UploadedCopy ?? false;
        }

        if (path is not null)
        {
            Open(path, uploaded, displayName);
        }
    }

    public void ClearUploads()
    {
        PreviewLease? closing = null;
        lock (_gate)
        {
            if (_current?.UploadedCopy is true)
            {
                closing = _current;
                _current = null;
            }
        }

        closing?.Dispose();
        LocalFileCleanup.DeleteDirectory(
            Path.Combine(_root, "uploads"));
    }

    public void Dispose()
    {
        PreviewLease? lease;
        lock (_gate)
        {
            lease = _current;
            _current = null;
        }

        lease?.Dispose();
        LocalFileCleanup.DeleteDirectory(_root);
    }

    private PreviewLease CreateLease(
        CommandContext context,
        string path,
        string displayName,
        bool uploadedCopy,
        ProductDefinition product)
    {
        AppPreviewMount mount = _mount
            ?? throw new InvalidOperationException(
                "The App preview mount is not configured.");
        string view = _preferences.Current.PreviewView(product);
        MountedPreview runtime = PreviewRuntime.Mount(
            new PreviewStartOptions(
                context.Activate(product),
                product,
                context.ResourceBudgets,
                path,
                RequestedPort: 0,
                Request: new ProductPreviewRequest(view, FontProfile: _fontProfile),
                Diagnostic: message =>
                {
                    _log.Write($"preview {message}");
                    Activity?.Invoke();
                },
                DisplayName: displayName),
            mount.Options);
        return new PreviewLease(
            path,
            displayName,
            uploadedCopy,
            product.Manifest.Id,
            view,
            mount.Url,
            runtime);
    }

    private string? Read(Func<PreviewLease, string> selector)
    {
        lock (_gate)
        {
            return _current is null ? null : selector(_current);
        }
    }

    private sealed record UploadPaths(
        string Files,
        string Staging);

    private sealed record PreviewLease(
        string Path,
        string FileName,
        bool UploadedCopy,
        string ProductId,
        string View,
        string Url,
        MountedPreview Runtime) : IDisposable
    {
        public void Dispose() => Runtime.Dispose();
    }
}

internal sealed record AppPreviewMount(
    int Port,
    PreviewMountOptions Options)
{
    public string Url =>
        $"http://127.0.0.1:{Port}{Options.DocumentPath}";
}
