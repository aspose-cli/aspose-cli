using System.Collections.Frozen;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;

namespace Aspose.Cli.Sdk.IO.Extraction;

/// <summary>
/// Bounds extracted files and publishes them through the shared transaction, each under a
/// plain file name the command chooses and never under one a document gives: a file the
/// document names, such as an attachment, is numbered by its kind instead. A file already in
/// the directory is replaced only when the caller allows it, and is otherwise refused as every
/// output is; the transaction refuses one that is an input either way.
/// </summary>
public sealed class ExtractionGuard : IDisposable
{
    /// <summary>
    /// The extensions a file the document names keeps: documents, images and plain text that
    /// open in a viewer, never a program, script, shortcut or shell setting. Any other is
    /// <c>.bin</c>.
    /// </summary>
    private static readonly FrozenSet<string> SafeExtensions = new[]
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp", ".rtf",
        ".txt", ".csv", ".md", ".json", ".xml",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp",
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly ExtractionBudgetLedger _budget;
    private readonly string _root;
    private readonly AtomicOutputSetWriter _transaction;
    private readonly bool _overwrite;
    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Creates a guarded extraction into <paramref name="root"/>.</summary>
    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="root">The extraction directory, which may not exist yet.</param>
    /// <param name="overwrite">Whether an existing file in the directory may be replaced (<c>--overwrite</c>).</param>
    /// <param name="maxItems">The most files the extraction may create.</param>
    /// <param name="maxBytes">The most bytes the extraction may write.</param>
    public ExtractionGuard(ResourceBudgetLedger resourceBudgets, string root, bool overwrite,
        int maxItems = PublicationLimits.MaximumEntries, long maxBytes = 512L * 1024 * 1024)
        : this(resourceBudgets, root, overwrite, maxItems, maxBytes, NoPublicationFaultInjector.Instance) { }

    internal ExtractionGuard(ResourceBudgetLedger resourceBudgets, string root, bool overwrite,
        int maxItems, long maxBytes, IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        if (maxItems is < 1 or > PublicationLimits.MaximumEntries)
        { throw CliErrors.OptionInvalid("--max-items", $"must be between 1 and {PublicationLimits.MaximumEntries}", "Use a supported extraction item budget."); }
        _overwrite = overwrite;
        _budget = new ExtractionBudgetLedger(maxItems,
            Math.Min(maxBytes, resourceBudgets.Remaining(ResourceBudgetKinds.OutputBytes)));
        _root = Path.GetFullPath(root);
        _transaction = new AtomicOutputSetWriter(new SafeFileWriter(resourceBudgets), _root, "extraction", faults);
    }

    /// <summary>
    /// Writes one file of <paramref name="bytes"/> as <see cref="Stage"/> does and returns the
    /// facts of the file it publishes, from which <c>FileObjects.Companion</c> describes it in a
    /// result.
    /// </summary>
    /// <param name="name">The file name the command chose, such as <c>table-001.csv</c>.</param>
    /// <param name="bytes">The file's content.</param>
    public PublishedFile StageAllBytes(string name, byte[] bytes) =>
        Stage(name, bytes.LongLength, stream => stream.Write(bytes));

    /// <summary>
    /// Writes a file the document names, such as an attachment, as the next of its
    /// <paramref name="kind"/>: <c>&lt;kind&gt;-0001</c> and on, with the extension of
    /// <paramref name="documentName"/> when it is a safe one and <c>.bin</c> otherwise. The
    /// document's name is no part of the path; a result reports it as a value.
    /// </summary>
    /// <param name="kind">What the file is, such as <c>attachment</c>.</param>
    /// <param name="documentName">The name the document gives the file, untrusted; null when it gives none.</param>
    /// <param name="sizeBytes">The size the document declares, reserved against the budget.</param>
    /// <param name="write">Writes the content.</param>
    public PublishedFile StageNamedByDocument(string kind, string? documentName, long sizeBytes, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        int number = _numbers.GetValueOrDefault(kind) + 1;
        _numbers[kind] = number;
        return Stage($"{kind}-{number:0000}{SafeExtension(documentName)}", sizeBytes, write);
    }

    /// <summary>
    /// Writes one file named <paramref name="name"/> into the directory and returns the facts of
    /// the file it publishes, taken from the staged copy.
    /// </summary>
    /// <param name="name">The plain file name the command chose, never one from the document.</param>
    /// <param name="sizeBytes">The expected size, reserved against the budget.</param>
    /// <param name="write">Writes the content.</param>
    public PublishedFile Stage(string name, long sizeBytes, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || OutputPathValidator.IsUnsafeSegment(name))
        {
            throw new ArgumentException($"'{name}' is no plain file name the command chose.", nameof(name));
        }
        try
        {
            _budget.ReserveFile(sizeBytes);
            string target = Path.Combine(_root, name);
            long reserved = Math.Max(0, sizeBytes);
            long limit = _budget.WriteLimit(reserved);
            FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
            StagedOutput output = _transaction.StagePrepared(target, _overwrite, requestedBackup: null, original, staged =>
            {
                using var file = new FileStream(staged, FileMode.Truncate, FileAccess.Write, FileShare.None);
                using Stream bounded = _budget.Bound(file, limit, name);
                write(bounded);
                bounded.Flush();
                _budget.Reconcile(reserved, bounded.Length);
                FilePublicationSnapshot current = FilePublicationSnapshot.Capture(target);
                if (!original.VersionEquals(current)) { throw PublicationErrors.OutputConflict(target, original, current); }
            });
            return output.Published;
        }
        catch { Dispose(); throw; }
    }

    /// <summary>Runs after the extraction is published; the write pipeline records the publication.</summary>
    internal Action? Committed { get; init; }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transaction.Commit();
        Committed?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _transaction.Dispose();
    }

    // The extension of a name the document gave, when it is a safe one; .bin otherwise.
    private static string SafeExtension(string? documentName)
    {
        string extension = Path.GetExtension(documentName ?? string.Empty).ToLowerInvariant();
        return SafeExtensions.Contains(extension) ? extension : ".bin";
    }
}
