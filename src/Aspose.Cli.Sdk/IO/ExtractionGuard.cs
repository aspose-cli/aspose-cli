using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Bounds and names extracted files, then publishes through the shared transaction.</summary>
public sealed class ExtractionGuard : IDisposable
{
    private readonly ExtractionBudgetLedger _budget;
    private readonly ExtractionPlan _plan;
    private readonly AtomicOutputSetWriter _transaction;
    private bool _disposed;

    public ExtractionGuard(ResourceBudgetLedger resourceBudgets, string root,
        int maxItems = PublicationLimits.MaximumEntries, long maxBytes = 512L * 1024 * 1024)
        : this(resourceBudgets, root, maxItems, maxBytes, NoPublicationFaultInjector.Instance) { }

    internal ExtractionGuard(ResourceBudgetLedger resourceBudgets, string root,
        int maxItems, long maxBytes, IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        if (maxItems is < 1 or > PublicationLimits.MaximumEntries)
        { throw CliErrors.OptionInvalid("--max-items", $"must be between 1 and {PublicationLimits.MaximumEntries}", "Use a supported extraction item budget."); }
        _budget = new ExtractionBudgetLedger(maxItems,
            Math.Min(maxBytes, resourceBudgets.Remaining(ResourceBudgetKinds.OutputBytes)));
        _plan = new ExtractionPlan(Path.GetFullPath(root));
        _plan.EnsureRoot();
        _transaction = new AtomicOutputSetWriter(new SafeFileWriter(resourceBudgets), Path.GetFullPath(root), "extraction", faults);
    }

    public string Reserve(string suggestedName, long sizeBytes) => ReserveRelativePath(suggestedName, sizeBytes, flatten: true);

    public string ReserveRelativePath(string suggestedPath, long sizeBytes, bool flatten = false, bool overwrite = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string relative = ExtractionPathValidator.NormalizeRelativePath(suggestedPath, flatten);
        _budget.ReserveFile(sizeBytes);
        return _plan.ReserveFile(relative, suggestedPath, overwrite);
    }

    public string CreateDirectory(string suggestedPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _budget.ReserveDirectory();
        return _plan.CreateDirectory(ExtractionPathValidator.NormalizeRelativePath(suggestedPath.TrimEnd('/', '\\'), false), suggestedPath);
    }

    public string WriteAllBytes(string suggestedName, byte[] bytes) =>
        Write(suggestedName, bytes.LongLength, stream => stream.Write(bytes), flatten: true);

    public string CopyFile(string suggestedName, string sourcePath) =>
        Write(suggestedName, new FileInfo(sourcePath).Length, output =>
        {
            using var input = File.OpenRead(sourcePath);
            input.CopyTo(output);
        }, flatten: true);

    public string Write(string suggestedPath, long sizeBytes, Action<Stream> write, bool flatten = false, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(write);
        try
        {
            string target = ReserveRelativePath(suggestedPath, sizeBytes, flatten, overwrite);
            long reserved = Math.Max(0, sizeBytes);
            long limit = _budget.WriteLimit(reserved);
            FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
            _transaction.StagePrepared(target, overwrite, requestedBackup: null, original, staged =>
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
