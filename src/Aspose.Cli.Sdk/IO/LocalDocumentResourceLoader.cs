namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Resolves bounded local document resources without delegating URI fetching
/// to a product engine.
/// </summary>
public sealed class LocalDocumentResourceLoader
{
    private const int DefaultMaximumItems = 256;
    private const long DefaultMaximumItemBytes = 32L * 1024 * 1024;
    private const long DefaultMaximumTotalBytes = 128L * 1024 * 1024;

    private readonly Uri _baseUri;
    private readonly ResourcePathBoundary _boundary;
    private readonly int _maximumItems;
    private readonly long _maximumItemBytes;
    private readonly long _maximumTotalBytes;
    private readonly object _gate = new();
    private int _items;
    private long _totalBytes;

    /// <summary>Creates a loader rooted beside the owning document.</summary>
    public LocalDocumentResourceLoader(
        string documentPath,
        int maximumItems = DefaultMaximumItems,
        long maximumItemBytes = DefaultMaximumItemBytes,
        long maximumTotalBytes = DefaultMaximumTotalBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        if (maximumItems < 1 || maximumItemBytes < 1 || maximumTotalBytes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumItems),
                "Local resource limits must be positive.");
        }

        string fullDocumentPath = Path.GetFullPath(documentPath);
        string root = Path.GetDirectoryName(fullDocumentPath)
            ?? throw new ArgumentException("The document path has no parent directory.", nameof(documentPath));
        _baseUri = new Uri(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar);
        _boundary = new ResourcePathBoundary(root);
        _maximumItems = maximumItems;
        _maximumItemBytes = maximumItemBytes;
        _maximumTotalBytes = maximumTotalBytes;
    }

    /// <summary>
    /// Reads one existing file below the document root. Network, protocol-relative,
    /// data, UNC, escaping, linked and over-budget resources return <see langword="false"/>.
    /// </summary>
    public bool TryRead(string? reference, out byte[] data)
    {
        data = [];
        if (string.IsNullOrWhiteSpace(reference)
            || reference.StartsWith("//", StringComparison.Ordinal)
            || reference.StartsWith("\\\\", StringComparison.Ordinal)
            || !Uri.TryCreate(_baseUri, reference, out Uri? resolved)
            || !resolved.IsFile
            || resolved.IsUnc)
        {
            return false;
        }

        string path;
        try
        {
            path = Path.GetFullPath(resolved.LocalPath);
            if (!_boundary.Contains(path) || !File.Exists(path))
            {
                return false;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException
            or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        long length;
        try
        {
            length = new FileInfo(path).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        lock (_gate)
        {
            if (length > _maximumItemBytes
                || _items >= _maximumItems
                || length > _maximumTotalBytes - _totalBytes)
            {
                return false;
            }

            _items++;
            _totalBytes += length;
        }

        try
        {
            data = File.ReadAllBytes(path);
            return data.LongLength == length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
