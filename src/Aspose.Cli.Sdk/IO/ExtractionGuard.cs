using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Bounds, plans, and transactionally publishes extracted files.</summary>
public sealed class ExtractionGuard : IDisposable
{
    private readonly string _workingRoot;
    private readonly bool _workerStagingOnly;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly ExtractionBudgetLedger _budget;
    private readonly ExtractionPlan _plan;
    private readonly ExtractionTransaction _transaction;

    public ExtractionGuard(
        ResourceBudgetLedger resourceBudgets,
        string root,
        int maxItems = 1000,
        long maxBytes = 512L * 1024 * 1024)
        : this(
            resourceBudgets,
            root,
            maxItems,
            maxBytes,
            NoPublicationFaultInjector.Instance)
    {
    }

    internal ExtractionGuard(
        ResourceBudgetLedger resourceBudgets,
        string root,
        int maxItems,
        long maxBytes,
        IPublicationFaultInjector faults)
    {
        _resourceBudgets = resourceBudgets
            ?? throw new ArgumentNullException(nameof(resourceBudgets));
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(faults);
        string fullRoot = Path.GetFullPath(root);
        _workerStagingOnly = WorkerOutputSession.IsActive;
        _workingRoot = _workerStagingOnly
            ? WorkerOutputSession.CreatePrivateDirectory("extraction")
            : fullRoot;
        _budget = new ExtractionBudgetLedger(maxItems, maxBytes);
        _plan = new ExtractionPlan(fullRoot, _workingRoot, _workerStagingOnly);
        _transaction = new ExtractionTransaction(
            _workerStagingOnly,
            _workingRoot,
            faults,
            _plan.CreatedDirectories);
    }

    public string Reserve(string suggestedName, long sizeBytes) =>
        ReserveRelativePath(
            suggestedName,
            sizeBytes,
            flatten: true,
            overwrite: false);

    public string ReserveRelativePath(
        string suggestedPath,
        long sizeBytes,
        bool flatten = false,
        bool overwrite = false)
    {
        string relativePath = ExtractionPathValidator.NormalizeRelativePath(
            suggestedPath,
            flatten);
        _budget.ReserveFile(sizeBytes);
        return _plan.ReserveFile(relativePath, suggestedPath, overwrite);
    }

    public string CreateDirectory(string suggestedPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(suggestedPath);
        string relativePath = ExtractionPathValidator.NormalizeRelativePath(
            suggestedPath.TrimEnd('/', '\\'),
            flatten: false);
        _budget.ReserveDirectory();
        string target = _plan.CreateDirectory(relativePath, suggestedPath);
        if (_workerStagingOnly)
        {
            _transaction.EnrollWorkerDirectory(target);
        }
        return target;
    }

    public string WriteAllBytes(string suggestedName, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Write(
            suggestedName,
            bytes.LongLength,
            stream => stream.Write(bytes),
            flatten: true,
            overwrite: false);
    }

    public string CopyFile(string suggestedName, string sourcePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        long size = new FileInfo(sourcePath).Length;
        return Write(
            suggestedName,
            size,
            output =>
            {
                using FileStream input = File.OpenRead(sourcePath);
                input.CopyTo(output);
            },
            flatten: true,
            overwrite: false);
    }

    public string Write(
        string suggestedPath,
        long sizeBytes,
        Action<Stream> write,
        bool flatten = false,
        bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(write);
        string target = ReserveRelativePath(
            suggestedPath,
            sizeBytes,
            flatten,
            overwrite);
        long reservedBytes = Math.Max(0, sizeBytes);
        long writeLimit = _budget.WriteLimit(reservedBytes);
        string workingTarget = _workerStagingOnly
            ? _plan.ToWorkingPath(target)
            : target;
        string parent = Path.GetDirectoryName(workingTarget) ?? _workingRoot;
        _plan.EnsureWorkingDirectory(parent);
        ExtractionPathValidator.EnsureNoLinks(parent);
        string temp = Path.Combine(
            parent,
            $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.extracting");
        FilePublicationSnapshot original =
            FilePublicationSnapshot.Capture(target);
        bool replacing = original.Exists;
        ExtractionBackup? backup = null;
        OwnedTemporaryFile? temporary = null;
        try
        {
            if (replacing && !_workerStagingOnly)
            {
                backup = CreateBackup(target, parent, original);
            }

            temporary = OwnedTemporaryFile.Create(temp);
            long actualBytes = WriteStaged(
                temp,
                writeLimit,
                suggestedPath,
                write);
            temporary.BindProducedFile();
            _budget.Reconcile(reservedBytes, actualBytes);
            _resourceBudgets.Consume(
                ResourceBudgetKinds.OutputBytes,
                actualBytes,
                "bytes",
                "extraction-output");
            FilePublicationDurabilityAdapter.FlushFile(temp);
            original.Metadata?.ApplyContentAttributes(temp);
            FilePublicationSnapshot staged =
                FilePublicationSnapshot.Capture(temp);
            EnsureTargetUnchanged(target, original);
            Publish(
                target,
                workingTarget,
                temp,
                backup,
                overwrite,
                original,
                staged);
            temporary.MarkPublished();
            return target;
        }
        catch (Exception failure)
        {
            if (backup is not null)
            {
                ExtractionTransaction.DeleteForCleanup(
                    backup.Path,
                    backup.Snapshot);
            }
            if (!_transaction.HasPublishedOutputs)
            {
                throw;
            }
            PublicationRecoveryReport recovery = _transaction.RollBack();
            throw CliErrors.OutputPublicationFailure(failure, recovery);
        }
        finally
        {
            temporary?.Dispose();
        }
    }

    public void Commit() => _transaction.Commit();

    public void Dispose() => _transaction.DisposeIncomplete();

    private static ExtractionBackup CreateBackup(
        string target,
        string parent,
        FilePublicationSnapshot original)
    {
        string backup = Path.Combine(
            parent,
            $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.rollback");
        using var temporary = OwnedTemporaryFile.Create(backup);
        File.Copy(target, backup, overwrite: true);
        temporary.BindProducedFile();
        FilePublicationDurabilityAdapter.FlushFile(backup);
        original.Metadata?.ApplyContentAttributes(backup);
        FilePublicationSnapshot snapshot =
            FilePublicationSnapshot.Capture(backup);
        temporary.MarkPublished();
        return new ExtractionBackup(backup, snapshot);
    }

    private long WriteStaged(
        string temp,
        long writeLimit,
        string suggestedPath,
        Action<Stream> write)
    {
        using var output = new FileStream(
            temp,
            FileMode.Truncate,
            FileAccess.Write,
            FileShare.None);
        using Stream bounded = _budget.Bound(
            output,
            writeLimit,
            suggestedPath);
        write(bounded);
        bounded.Flush();
        return bounded.Length;
    }

    private static void EnsureTargetUnchanged(
        string target,
        FilePublicationSnapshot original)
    {
        FilePublicationSnapshot beforePublish =
            FilePublicationSnapshot.Capture(target);
        if (!original.VersionEquals(beforePublish))
        {
            throw CliErrors.OutputConflict(target, original, beforePublish);
        }
    }

    private void Publish(
        string target,
        string workingTarget,
        string temp,
        ExtractionBackup? backup,
        bool overwrite,
        FilePublicationSnapshot original,
        FilePublicationSnapshot staged)
    {
        ExtractionPathValidator.EnsureNoLinks(
            Path.GetDirectoryName(workingTarget) ?? _workingRoot);
        if (_workerStagingOnly)
        {
            File.Move(temp, workingTarget, overwrite: true);
        }
        else
        {
            FilePublicationAtomicSwap.Publish(
                temp,
                workingTarget,
                overwrite,
                original,
                original,
                expectedStage: staged);
        }
        ExtractionPathValidator.EnsureNoLinks(workingTarget);
        if (_workerStagingOnly)
        {
            PublishWorker(
                target,
                workingTarget,
                overwrite,
                original,
                staged);
        }
        else if (original.Exists)
        {
            _transaction.EnrollReplaced(
                target,
                backup!.Path,
                backup.Snapshot,
                original,
                staged);
        }
        else
        {
            _transaction.EnrollCreated(target, staged);
        }
    }

    private void PublishWorker(
        string target,
        string workingTarget,
        bool overwrite,
        FilePublicationSnapshot original,
        FilePublicationSnapshot staged)
    {
        WorkerOutputSession.RegisterFile(
            target,
            workingTarget,
            overwrite,
            backupPath: null,
            original,
            staged);
        _transaction.EnrollWorkerFile(
            target,
            workingTarget,
            overwrite,
            original,
            staged);
    }

    private sealed record ExtractionBackup(
        string Path,
        FilePublicationSnapshot Snapshot);
}
