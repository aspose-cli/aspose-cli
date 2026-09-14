using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Validates and stages unique publication targets.</summary>
internal sealed class AtomicPublicationStaging(
    AtomicPublicationPlan plan,
    SafeFileWriter writer)
{
    public StagedOutput Stage(
        string targetPath,
        bool overwrite,
        Action<string> write)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentNullException.ThrowIfNull(write);
        if (plan.Journal.State != PublicationTransactionState.Staging)
        {
            throw new InvalidOperationException(
                $"Cannot stage an output while the transaction is {plan.Journal.State}.");
        }

        string target = Path.GetFullPath(targetPath);
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        return StagePrepared(target, overwrite, requestedBackup: null, original, write);
    }

    public StagedOutput StagePrepared(
        string target,
        bool overwrite,
        string? requestedBackup,
        FilePublicationSnapshot original,
        Action<string> write,
        FileWritePrecondition? inputPrecondition = null,
        Action<string, Stream>? inspect = null,
        Action<string>? verify = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (plan.Journal.State != PublicationTransactionState.Staging) { throw new InvalidOperationException("The output set is sealed."); }
        plan.Lease?.EnsureCovers(target);
        target = OutputPathValidator.NormalizeFile(target);
        requestedBackup = requestedBackup is null
            ? null
            : OutputPathValidator.NormalizeFile(requestedBackup, phase: "backup");
        EnsureUnique(target, requestedBackup);
        FilePublicationSnapshot? backupOriginal = requestedBackup is null ? null : FilePublicationSnapshot.Capture(requestedBackup);
        if (!overwrite && original.Exists)
        {
            throw CliErrors.OutputExists(target);
        }
        FilePhysicalIdentity? targetParentIdentity = plan.WorkerStagingOnly
            ? null
            : OutputPathValidator.CaptureParentIdentity(target);
        FilePhysicalIdentity? requestedBackupParentIdentity =
            plan.WorkerStagingOnly || requestedBackup is null
                ? null
                : OutputPathValidator.CaptureParentIdentity(requestedBackup);

        int index = plan.Journal.Entries.Count;
        string staged = plan.StagedPath(target, index);
        plan.CreatedStagingDirectories.Add(Path.GetDirectoryName(staged)!);
        if (plan.WorkerStagingOnly) { PrivateUserStorage.EnsureDirectory(Path.GetDirectoryName(staged)!); }
        else { FilePublicationMetadata.PrepareOutputDirectory(Path.GetDirectoryName(staged)!, Path.GetDirectoryName(target)!); }
        FilePublicationSnapshot stagedSnapshot;
        using (var temporary = OwnedTemporaryFile.Create(staged))
        {
            write(staged);
            temporary.BindInspectAndVerify(inspect, verify);
            original.Metadata?.ApplyAccess(staged);
            original.Metadata?.ApplyContentAttributes(staged);
            temporary.BindProducedFile();
            temporary.FlushBound();
            stagedSnapshot = temporary.CaptureBoundSnapshot();
            writer.ConsumeOutput(stagedSnapshot.Length, "output-set-stage");
            temporary.MarkPublished();
        }

        long size = stagedSnapshot.Length;
        // Staging files are private content carriers. Replaying a Windows
        // owner here would require WRITE_OWNER even though the caller has all
        // permissions needed to edit the target contents.
        if (!plan.WorkerStagingOnly)
        {
            OutputPathValidator.EnsureParentUnchanged(
                target,
                targetParentIdentity);
            if (requestedBackup is not null)
            {
                OutputPathValidator.EnsureParentUnchanged(
                    requestedBackup,
                    requestedBackupParentIdentity);
            }
        }
        plan.Journal.Entries.Add(new PublicationJournalEntry
        {
            Index = index,
            Target = target,
            Staged = staged,
            Overwrite = overwrite,
            RequestedBackup = requestedBackup,
            RequestedBackupOriginal = backupOriginal,
            InputPath = inputPrecondition?.Path,
            InputSnapshot = inputPrecondition?.Snapshot,
            Original = original,
            StagedSnapshot = stagedSnapshot,
            TargetParentIdentity = targetParentIdentity,
            RequestedBackupParentIdentity = requestedBackupParentIdentity,
            Displaced = original.Exists && !plan.WorkerStagingOnly
                ? plan.DisplacedPath(index)
                : null,
            Size = size,
        });
        plan.Persist();
        return new StagedOutput(plan.Journal.Entries[^1]);
    }

    public void StageDeletionPrepared(
        string target,
        FilePublicationSnapshot original)
    {
        if (plan.Journal.State != PublicationTransactionState.Staging)
        {
            throw new InvalidOperationException(
                $"Cannot stage a deletion while the transaction is {plan.Journal.State}.");
        }
        if (!original.Exists)
        {
            throw new InvalidOperationException(
                $"Cannot stage deletion of missing target '{target}'.");
        }

        plan.Lease?.EnsureCovers(target);
        target = OutputPathValidator.NormalizeFile(target);
        EnsureUnique(target);
        FilePhysicalIdentity? targetParentIdentity = plan.WorkerStagingOnly
            ? null
            : OutputPathValidator.CaptureParentIdentity(target);
        int index = plan.Journal.Entries.Count;
        string staged = plan.StagedPath(target, index);
        plan.CreatedStagingDirectories.Add(Path.GetDirectoryName(staged)!);
        if (plan.WorkerStagingOnly) { PrivateUserStorage.EnsureDirectory(Path.GetDirectoryName(staged)!); }
        else { FilePublicationMetadata.PrepareOutputDirectory(Path.GetDirectoryName(staged)!, Path.GetDirectoryName(target)!); }
        FilePublicationSnapshot stagedSnapshot;
        using (var temporary = OwnedTemporaryFile.Create(staged))
        {
            temporary.BindInspectAndVerify(inspect: null, verify: null);
            temporary.FlushBound();
            stagedSnapshot = temporary.CaptureBoundSnapshot();
            temporary.MarkPublished();
        }
        if (!plan.WorkerStagingOnly)
        {
            OutputPathValidator.EnsureParentUnchanged(
                target,
                targetParentIdentity);
        }

        plan.Journal.Entries.Add(new PublicationJournalEntry
        {
            Index = index,
            Target = target,
            Staged = staged,
            Overwrite = true,
            Original = original,
            StagedSnapshot = stagedSnapshot,
            TargetParentIdentity = targetParentIdentity,
            Displaced = plan.DisplacedPath(index),
            DeleteTarget = true,
            Size = 0,
        });
        plan.Persist();
    }

    private void EnsureUnique(string target, string? backup = null)
    {
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (plan.Journal.Entries.Any(
                item => comparer.Equals(item.Target, target)
                    || item.RequestedBackup is not null && comparer.Equals(item.RequestedBackup, target)
                    || backup is not null && (comparer.Equals(item.Target, backup)
                        || item.RequestedBackup is not null && comparer.Equals(item.RequestedBackup, backup)))
            || backup is not null && comparer.Equals(target, backup))
        {
            throw CliErrors.OptionInvalid(
                "--name-template",
                $"produces duplicate output '{target}'",
                "Include {n}, {pages} or {bookmark} so every output name is unique.");
        }
    }
}
