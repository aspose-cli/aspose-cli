using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Bounds and names extracted files, then publishes through the shared transaction.</summary>
public sealed class ExtractionGuard : IDisposable
{
    private readonly ExtractionBudgetLedger _budget;
    private readonly ExtractionPlan _plan;
    private readonly AtomicOutputSetWriter _transaction;
    private bool _committed;
    private bool _disposed;

    public ExtractionGuard(ResourceBudgetLedger resourceBudgets, string root,
        int maxItems = 1000, long maxBytes = 512L * 1024 * 1024)
        : this(resourceBudgets, root, maxItems, maxBytes, NoPublicationFaultInjector.Instance) { }

    internal ExtractionGuard(ResourceBudgetLedger resourceBudgets, string root,
        int maxItems, long maxBytes, IPublicationFaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        _budget = new ExtractionBudgetLedger(maxItems, maxBytes);
        _plan = new ExtractionPlan(Path.GetFullPath(root), WorkerOutputSession.IsActive);
        _plan.EnsureRoot();
        try { _transaction = new AtomicOutputSetWriter(new SafeFileWriter(resourceBudgets), Path.GetFullPath(root), "extraction", faults); }
        catch { _plan.RemoveCreatedDirectories(); throw; }
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
        if (WorkerOutputSession.IsActive)
        {
            foreach (string directory in _plan.Directories) { WorkerOutputSession.RegisterDirectory(directory); }
        }
        _transaction.Commit();
        _committed = true;
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        try { _transaction.Dispose(); }
        finally { if (!_committed) { _plan.RemoveCreatedDirectories(); } }
    }
}
