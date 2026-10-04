using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Bounds and names extracted files, then publishes through the shared transaction. Two
/// extracted items with the same name get distinct names; a file already in the directory is
/// replaced only when the caller allows it, and is otherwise refused as every output is.
/// </summary>
public sealed class ExtractionGuard : IDisposable
{
    private readonly ExtractionBudgetLedger _budget;
    private readonly ExtractionPlan _plan;
    private readonly AtomicOutputSetWriter _transaction;
    private readonly bool _overwrite;
    private bool _disposed;

    /// <summary>Creates a guarded extraction into <paramref name="root"/>.</summary>
    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="root">The extraction directory, which may not exist yet.</param>
    /// <param name="overwrite">Whether an existing file in the directory may be replaced (<c>--overwrite</c>).</param>
    /// <param name="maxItems">The most files and directories the extraction may create.</param>
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
        string fullRoot = Path.GetFullPath(root);
        _plan = new ExtractionPlan(fullRoot);
        _transaction = new AtomicOutputSetWriter(new SafeFileWriter(resourceBudgets), fullRoot, "extraction", faults);
    }

    private string ReserveRelativePath(string suggestedPath, long sizeBytes, bool flatten)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string relative = ExtractionPathValidator.NormalizeRelativePath(suggestedPath, flatten);
        _budget.ReserveFile(sizeBytes);
        return _plan.ReserveFile(relative, suggestedPath);
    }

    public string CreateDirectory(string suggestedPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _budget.ReserveDirectory();
        return _plan.CreateDirectory(ExtractionPathValidator.NormalizeRelativePath(suggestedPath.TrimEnd('/', '\\'), false), suggestedPath);
    }

    public string WriteAllBytes(string suggestedName, byte[] bytes) =>
        Write(suggestedName, bytes.LongLength, stream => stream.Write(bytes), flatten: true);

    public string Write(string suggestedPath, long sizeBytes, Action<Stream> write, bool flatten = false)
    {
        ArgumentNullException.ThrowIfNull(write);
        try
        {
            string target = ReserveRelativePath(suggestedPath, sizeBytes, flatten);
            long reserved = Math.Max(0, sizeBytes);
            long limit = _budget.WriteLimit(reserved);
            FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
            _transaction.StagePrepared(target, _overwrite, requestedBackup: null, original, staged =>
            {
                using var file = new FileStream(staged, FileMode.Truncate, FileAccess.Write, FileShare.None);
                using Stream bounded = _budget.Bound(file, limit, suggestedPath);
                write(bounded);
                bounded.Flush();
                _budget.Reconcile(reserved, bounded.Length);
                FilePublicationSnapshot current = FilePublicationSnapshot.Capture(target);
                if (!original.VersionEquals(current)) { throw CliErrors.OutputConflict(target, original, current); }
            });
            return target;
        }
        catch { Dispose(); throw; }
    }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (string directory in _plan.Directories) { _transaction.EnsureDirectory(directory); }
        _transaction.Commit();
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _transaction.Dispose();
    }
}
