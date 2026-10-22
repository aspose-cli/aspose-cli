using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using System.Runtime.ExceptionServices;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>A resource a document names that was left out of the output, and why.</summary>
/// <param name="Reference">
/// The reference as the document wrote it. A local path is named only from the input folder: a
/// path below it relative to it, any other one by its file name, so no result carries a local
/// folder.
/// </param>
/// <param name="Reason">Why it was left out, such as <c>not found</c> or <c>outside the input folder</c>.</param>
public sealed record OmittedResource(string Reference, string Reason);

/// <summary>Owns one document's external resource policy, budgets and callback lifetime.</summary>
public sealed class LocalDocumentResourceLoader : IDisposable
{
    /// <summary>The most omitted resources an omission warning names; it counts the rest.</summary>
    public const int ListedResources = 10;

    private const int ReferenceLength = 120;
    private readonly Uri _baseUri;
    private readonly string _root;
    private readonly VerifiedFileBoundary _boundary;
    private readonly ResourceBudgetLedger _budgets;
    private readonly int _maximumItems;
    private readonly long _maximumItemBytes;
    private readonly long _maximumTotalBytes;
    private readonly object _gate = new();
    private readonly List<OmittedResource> _omitted = [];
    private int _items;
    private long _totalBytes;
    private bool _disposed;
    private ExceptionDispatchInfo? _failure;
    private Exception? _refusedImage;

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
        _root = Path.GetDirectoryName(budgets.Inputs.ResourceOriginFor(documentPath))!;
        _baseUri = new Uri(new Uri(Path.EndsInDirectorySeparator(_root)
            ? _root : _root + Path.DirectorySeparatorChar).AbsoluteUri);
        _boundary = new VerifiedFileBoundary(_root);
        // The gate names the files it blocks for this document as this loader names them.
        NetworkGate.NameFilesBelow(_root);
        _maximumItems = maximumItems;
        _maximumItemBytes = maximumItemBytes;
        _maximumTotalBytes = maximumTotalBytes;
    }

    /// <summary>Canonical base used by native importers for relative resource references.</summary>
    public string BaseUri => _baseUri.AbsoluteUri;

    /// <summary>
    /// The resources left out so far, each reference once with the first reason it was left out
    /// for: an engine that asks for one resource several times leaves it out once.
    /// </summary>
    public IReadOnlyList<OmittedResource> Omitted { get { lock (_gate) { return [.. _omitted]; } } }

    public int OmittedCount { get { lock (_gate) { return _omitted.Count; } } }

    private const string DefaultHint = "Copy each file the document needs into the input's folder, or a folder below it, and reference it by a relative path; network resources are not loaded. Then review the incomplete output.";

    /// <summary>
    /// The completeness warning for external resources a document engine was denied, known only
    /// by their number, or null when none were.
    /// </summary>
    public static Warning? OmissionWarning(int omitted) => omitted <= 0 ? null : new Warning(WarningCodes.RemoteResourcesBlocked, $"{omitted} resource(s) the document references were omitted.")
    {
        Hint = DefaultHint,
        AffectsCompleteness = true,
    };

    /// <summary>
    /// The completeness warning naming the resources left out and why, at most
    /// <see cref="ListedResources"/> of them, or null when none were. Every product reports
    /// omitted resources through this one warning; a reference several loaders left out is
    /// named once.
    /// </summary>
    public static Warning? OmissionWarning(IEnumerable<OmittedResource> omitted, string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(omitted);
        OmittedResource[] distinct = [.. omitted.DistinctBy(static item => item.Reference, StringComparer.Ordinal)];
        if (distinct.Length == 0)
        {
            return null;
        }

        return new Warning(WarningCodes.RemoteResourcesBlocked, $"{distinct.Length} resource(s) the document references were omitted: {Describe(distinct)}.")
        {
            Hint = hint ?? DefaultHint,
            AffectsCompleteness = true,
        };
    }

    /// <summary>
    /// Names the first <see cref="ListedResources"/> omitted resources with their reasons, as
    /// the omission warning does, and counts the rest.
    /// </summary>
    public static string Describe(IReadOnlyCollection<OmittedResource> omitted)
    {
        ArgumentNullException.ThrowIfNull(omitted);
        string listed = string.Join(", ", omitted.Take(ListedResources).Select(static item => $"{item.Reference} ({item.Reason})"));
        return omitted.Count > ListedResources ? $"{listed}, and {omitted.Count - ListedResources} more" : listed;
    }

    /// <summary>The completeness warning for this document's omitted resources, after any fatal callback failure.</summary>
    public Warning? Warning
    {
        get
        {
            ThrowIfFailed();
            return OmissionWarning(Omitted);
        }
    }

    /// <summary>
    /// Propagates a fatal callback failure even if an engine caught it internally, and the
    /// refusal of an image once: it fails the load that read the image, not the document.
    /// </summary>
    public void ThrowIfFailed()
    {
        lock (_gate)
        {
            _failure?.Throw();
            if (_refusedImage is { } refused)
            {
                _refusedImage = null;
                throw refused;
            }

            _budgets.Deadline.ThrowIfExpired("document-resource");
        }
    }

    /// <summary>
    /// Reads a verified local resource in full. Rejected resources return an empty result and
    /// are recorded with their reason; shared budget, cancellation and deadline failures abort
    /// the operation. An image that
    /// <see cref="ImageInput"/> refuses, when the caller reads an image, fails the load that
    /// reads it: it is thrown here and once more by the next <see cref="ThrowIfFailed"/>, in case
    /// the engine swallowed it. An admitted SVG image comes back with only what it holds itself
    /// (<see cref="ImageInput"/>), and one that does not parse as XML is omitted.
    /// </summary>
    /// <param name="reference">The reference the document names, resolved against the input folder.</param>
    /// <param name="data">The resource's bytes, or empty when it was rejected.</param>
    /// <param name="image">Whether the document embeds the resource as an image, which must then be intact.</param>
    /// <param name="written">
    /// The reference as the document wrote it, when the engine passes a resolved one as
    /// <paramref name="reference"/>; it names an omitted resource.
    /// </param>
    public bool TryRead(string? reference, out byte[] data, bool image = false, string? written = null)
    {
        data = [];
        Exception? refused;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string named = string.IsNullOrWhiteSpace(written) ? reference ?? string.Empty : written;
            try
            {
                ThrowIfFailed();
                if (_items >= _maximumItems) { return Omit(named, $"over the limit of {_maximumItems} resources"); }
                _items++;
                if (Resolve(reference, out string? path) is { } unresolved)
                {
                    return Omit(named, unresolved);
                }
                if (!FilePaths.IsWithin(_root, path!, allowEqual: false))
                {
                    return Omit(named, "outside the input folder");
                }
                using VerifiedReadLease? stream = _boundary.TryOpenRead(path!);
                if (stream is null)
                {
                    return Omit(named, File.Exists(path) || Directory.Exists(path) ? "not an ordinary file" : "not found");
                }
                long length = stream.Length;
                if (length > _maximumItemBytes || length > Array.MaxLength)
                {
                    return Omit(named, "too large");
                }
                if (length > _maximumTotalBytes - _totalBytes)
                {
                    return Omit(named, "over the total size limit for resources");
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
                        return Omit(named, "changed while it was read");
                    }
                    offset += read;
                }
                _budgets.Deadline.ThrowIfExpired("document-resource");
                if (stream.ReadByte() != -1 || stream.Length != length)
                {
                    return Omit(named, "changed while it was read");
                }
                refused = image ? ImageInput.Refusal(buffer, _budgets, path!, ImageInput.Corrupt) : null;
                byte[]? content = image && refused is null ? ImageInput.KeepInternal(buffer) : buffer;
                if (content is null)
                {
                    // An SVG that cannot be cleaned is left out like any refused resource.
                    return Omit(named, "not a supported image");
                }

                if (refused is null)
                {
                    NetworkGate.Permit(path!);
                    data = content;
                    return true;
                }
            }
            catch (Exception exception) when (exception is CliException or OperationCanceledException)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Omit(named, "unreadable");
            }

            _refusedImage = refused;
        }

        throw refused;
    }

    /// <summary>Resolves a reference to a local path, or returns why it names none.</summary>
    private string? Resolve(string? reference, out string? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return "empty reference";
        }
        if (FilePaths.IsRemoteOrDevice(reference))
        {
            return "network address";
        }
        try
        {
            if (!Uri.TryCreate(_baseUri, reference, out Uri? resolved))
            {
                return "not a file reference";
            }
            if (!resolved.IsFile || resolved.IsUnc)
            {
                return "network address";
            }
            if (!string.IsNullOrEmpty(resolved.Query) || !string.IsNullOrEmpty(resolved.Fragment))
            {
                return "not a file reference";
            }
            // The boundary validates decoded segments, including ADS and Win32 aliases.
            path = resolved.LocalPath;
            return null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return "not a file reference";
        }
    }

    private bool Omit(string reference, string reason)
    {
        var omitted = new OmittedResource(ReferenceName(reference), reason);
        if (!_omitted.Exists(item => string.Equals(item.Reference, omitted.Reference, StringComparison.Ordinal)))
        {
            _omitted.Add(omitted);
        }

        return false;
    }

    /// <summary>
    /// How a warning names a reference: as written, unless it is a local path or file URI, which
    /// is named relative to the input folder when below it and by its file name otherwise; a
    /// network address loses its query. Long references are shortened.
    /// </summary>
    public string ReferenceName(string reference) => NameOf(reference, _root);

    /// <inheritdoc cref="ReferenceName(string)"/>
    /// <param name="reference">The reference as the document wrote it.</param>
    /// <param name="root">The input folder, or null when there is none: a local file is then named by its file name.</param>
    internal static string NameOf(string reference, string? root)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string named = reference.Trim();
        if (named.Length == 0)
        {
            return "(empty)";
        }

        try
        {
            string? local = Uri.TryCreate(named, UriKind.Absolute, out Uri? uri)
                ? uri.IsFile ? uri.LocalPath : null
                : Path.IsPathFullyQualified(named) || named.StartsWith('\\') || named.StartsWith('/') ? named : null;
            if (local is not null)
            {
                string full = Path.GetFullPath(local);
                named = root is not null && FilePaths.IsWithin(root, full, allowEqual: false)
                    ? Path.GetRelativePath(root, full)
                    : Path.GetFileName(full.TrimEnd('\\', '/'));
            }
            else if (uri is not null && !string.IsNullOrEmpty(uri.Query))
            {
                named = uri.GetLeftPart(UriPartial.Path);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            named = "(unreadable reference)";
        }

        return named.Length <= ReferenceLength ? named : named[..ReferenceLength] + "...";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}
