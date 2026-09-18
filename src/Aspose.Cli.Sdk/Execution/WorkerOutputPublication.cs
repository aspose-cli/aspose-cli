using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>Hands a validated worker plan to the same durable publisher used in-process.</summary>
internal static class WorkerOutputPublisher
{
    internal static IReadOnlyList<long> Publish(WorkerOutputManifest manifest, ResourceBudgetLedger budgets, IPublicationFaultInjector? faults = null)
    {
        budgets.Deadline.ThrowIfExpired("worker-publication");
        var created = new OwnedOutputDirectories(deferred: false);
        bool committed = false;
        try
        {
            foreach (WorkerDirectoryEntry directory in manifest.Directories.OrderBy(entry => entry.Target.Length))
            {
                budgets.Deadline.ThrowIfExpired("worker-directory");
                bool exists = Directory.Exists(directory.Target);
                if (exists != directory.Existed || directory.Existed && OperatingSystem.IsWindows()
                    && directory.OriginalIdentity != FilePublicationOwnedDelete.TryGetDirectoryIdentity(directory.Target))
                { throw new IOException($"Worker output directory changed: '{directory.Target}'."); }
                if (!exists)
                {
                    created.Ensure(directory.Target);
                }
                OutputPathValidator.EnsureSafeDirectory(directory.Target);
            }
            IReadOnlyList<long> sizes = [];
            if (manifest.Entries.Count > 0)
            {
                string root = CommonRoot(manifest.Entries);
                using var transaction = new AtomicOutputSetWriter(new SafeFileWriter(budgets), root, "worker-publication", faults ?? NoPublicationFaultInjector.Instance);
                foreach (WorkerOutputEntry entry in manifest.Entries)
                {
                    budgets.Deadline.ThrowIfExpired("worker-stage");
                    if (entry.TargetParentIdentity is not null) { OutputPathValidator.EnsureParentUnchanged(entry.Target, entry.TargetParentIdentity); }
                    if (entry.BackupPath is { } backup && entry.BackupOriginal is { } expected)
                    {
                        if (!expected.VersionEquals(FilePublicationSnapshot.Capture(backup))) { throw CliErrors.OutputConflict(backup, expected, FilePublicationSnapshot.Capture(backup)); }
                        if (entry.BackupParentIdentity is not null) { OutputPathValidator.EnsureParentUnchanged(backup, entry.BackupParentIdentity); }
                    }
                    if (entry.DeleteTarget) { transaction.StageDeletionPrepared(entry.Target, entry.Original); }
                    else
                    {
                        StagedOutput staged = transaction.StagePrepared(entry.Target, entry.Overwrite, entry.BackupPath, entry.Original,
                            path => CopyCandidate(entry, path, budgets),
                            entry.InputPath is null ? null : FileWritePrecondition.FromSnapshot(entry.InputPath, entry.InputSnapshot!));
                        if (!entry.StagedSnapshot.ContentEquals(staged.Snapshot)) { throw new IOException("The worker output changed during publication staging."); }
                    }
                }
                sizes = transaction.Commit();
            }
            if (manifest.Entries.Count == 0) { budgets.Deadline.ThrowIfExpired("worker-complete"); }
            committed = true;
            foreach (WorkerPreviewHint hint in manifest.Hints) { PreviewHintChannel.TryWrite(hint.FilePath, hint.Targets); }
            return sizes;
        }
        finally
        {
            if (!committed)
            {
                created.CleanUp();
            }
        }
    }

    private static void CopyCandidate(WorkerOutputEntry entry, string destination, ResourceBudgetLedger budgets)
    {
        using var input = new FileStream(entry.Staged, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged)))
        { throw new IOException("The worker output was replaced after manifest validation."); }
        using var output = new FileStream(destination, FileMode.Truncate, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            budgets.Deadline.ThrowIfExpired("worker-stage-copy");
            output.Write(buffer, 0, read);
        }
        if (!entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged)))
        { throw new IOException("The worker output changed during staging."); }
    }

    private static string CommonRoot(IReadOnlyList<WorkerOutputEntry> entries) =>
        OutputSetPaths.CommonDirectory(entries.SelectMany(entry => entry.BackupPath is null
            ? new[] { Path.GetDirectoryName(entry.Target)! }
            : new[] { Path.GetDirectoryName(entry.Target)!, Path.GetDirectoryName(entry.BackupPath)! }));
}
