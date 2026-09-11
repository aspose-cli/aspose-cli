using Aspose.Cli.Sdk.Errors;
using System.Runtime.ExceptionServices;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Owns one document's external resource policy, budgets and callback lifetime.</summary>
public sealed class LocalDocumentResourceLoader : IDisposable
{
    private readonly Uri _baseUri;
    private readonly VerifiedFileBoundary _boundary;
    private readonly ResourceBudgetLedger _budgets;
    private readonly int _maximumItems;
    private readonly long _maximumItemBytes;
    private readonly long _maximumTotalBytes;
    private readonly object _gate = new();
    private int _items;
    private long _totalBytes;
    private int _omitted;
    private bool _disposed;
    private ExceptionDispatchInfo? _failure;

    public LocalDocumentResourceLoader(
        string documentPath,
        ResourceBudgetLedger budgets,
        int maximumItems = 256,
        long maximumItemBytes = 32L * 1024 * 1024,
        long maximumTotalBytes = 128L * 1024 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        if (maximumItems < 1 || maximumItemBytes < 1 || maximumTotalBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumItems),
                "Local resource limits must be positive.");
        }
        string root = Path.GetDirectoryName(Path.GetFullPath(documentPath))!;
        _baseUri = new Uri(new Uri(Path.EndsInDirectorySeparator(root)
            ? root : root + Path.DirectorySeparatorChar).AbsoluteUri);
        _boundary = new VerifiedFileBoundary(root);
        _maximumItems = maximumItems;
        _maximumItemBytes = maximumItemBytes;
        _maximumTotalBytes = maximumTotalBytes;
    }

    public int OmittedCount { get { lock (_gate) { return _omitted; } } }

    /// <summary>Propagates a fatal callback failure even if an engine caught it internally.</summary>
    public void ThrowIfFailed()
    {
        lock (_gate)
        {
            _failure?.Throw();
            _budgets.Deadline.ThrowIfExpired("document-resource");
        }
    }

    /// <summary>
    /// Reads a verified local resource in full. Rejected resources return an empty result;
    /// shared budget, cancellation and deadline failures abort the operation.
    /// </summary>
    public bool TryRead(string? reference, out byte[] data)
    {
        data = [];
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                ThrowIfFailed();
                if (_items >= _maximumItems) { return Omit(); }
                _items++;
                if (!TryResolve(reference, out string? path))
                {
                    return Omit();
                }
                using VerifiedReadLease? stream = _boundary.TryOpenRead(path!);
                if (stream is null)
                {
                    return Omit();
                }
                long length = stream.Length;
                if (length > _maximumItemBytes || length > Array.MaxLength
                    || length > _maximumTotalBytes - _totalBytes)
                {
                    return Omit();
                }
                _totalBytes += length;
                // Reserve before allocating or reading; failed attempts cannot reclaim a budget.
                _budgets.Consume(ResourceBudgetKinds.InputBytes, length, "bytes", "document-resource");
                _budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, length, "bytes", "document-resource");
                byte[] buffer = new byte[(int)length];
                int offset = 0;
                while (offset < buffer.Length)
                {
                    _budgets.Deadline.ThrowIfExpired("document-resource");
                    int read = stream.Read(buffer, offset, Math.Min(64 * 1024, buffer.Length - offset));
                    if (read == 0)
                    {
                        return Omit();
                    }
                    offset += read;
                }
                _budgets.Deadline.ThrowIfExpired("document-resource");
                if (stream.ReadByte() != -1 || stream.Length != length)
                {
                    return Omit();
                }
                data = buffer;
                return true;
            }
            catch (Exception exception) when (exception is CliException or OperationCanceledException)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Omit();
            }
        }
    }

    private bool TryResolve(string? reference, out string? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(reference)
            || reference.StartsWith("//", StringComparison.Ordinal)
            || reference.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            if (!Uri.TryCreate(_baseUri, reference, out Uri? resolved)
                || !resolved.IsFile || resolved.IsUnc
                || !string.IsNullOrEmpty(resolved.Query)
                || !string.IsNullOrEmpty(resolved.Fragment))
            {
                return false;
            }
            // The boundary validates decoded segments, including ADS and Win32 aliases.
            path = resolved.LocalPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private bool Omit()
    {
        _omitted++;
        return false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}
