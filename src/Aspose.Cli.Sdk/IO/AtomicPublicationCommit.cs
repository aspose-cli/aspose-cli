using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Prepares backups and publishes every staged output in order.</summary>
internal sealed class AtomicPublicationCommit(AtomicPublicationPlan plan)
{
    public IReadOnlyList<long> Execute(Action? beforeCommit = null)
    {
        if (plan.Journal.State != PublicationTransactionState.Staging)
        {
            throw new InvalidOperationException(
                $"Cannot commit a transaction in state {plan.Journal.State}.");
        }

        if (plan.WorkerStagingOnly)
        {
            plan.ResourceBudgets?.Deadline.ThrowIfExpired("worker-handoff-start");
            beforeCommit?.Invoke();
            PublishToWorker();
            plan.Journal.State = PublicationTransactionState.Committed;
            return plan.Journal.Entries.Select(static entry => entry.Size).ToArray();
        }
        else
        {
            Prepare();
            RecordPublicationIntent();
            foreach (PublicationJournalEntry entry in plan.Journal.Entries)
            {
                Publish(entry);
            }
        }

        plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-commit");
        beforeCommit?.Invoke();
        plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-commit-record");
        plan.Transition(PublicationTransactionState.Committed);
        if (!plan.WorkerStagingOnly) { plan.ResourceBudgets?.MarkOutputsCommitted(); }
        return plan.Journal.Entries
            .OrderBy(static entry => entry.Index)
            .Select(static entry => entry.Size)
            .ToArray();
    }

    private void PublishToWorker() => plan.Worker!.RegisterBatch(
        plan.Journal.Entries, plan.OutputDirectories, plan.ResourceBudgets!.Deadline);

    /// <summary>
    /// Writes one write-ahead record naming every target as possibly published before the
    /// first swap. Recovery tells a target that was never swapped from one that was by the
    /// staged and displaced files, so a record per target would only make an output set's
    /// journal writes quadratic in its size.
    /// </summary>
    private void RecordPublicationIntent()
    {
        foreach (PublicationJournalEntry entry in plan.Journal.Entries)
        {
            entry.State = PublicationEntryState.Publishing;
        }
        plan.Transition(PublicationTransactionState.Publishing);
    }

    private void Prepare()
    {
        CreateBackupDirectory();
        foreach (PublicationJournalEntry entry in plan.Journal.Entries)
        {
            if (entry.InputPath is { } input && entry.InputSnapshot is { } expectedInput)
            {
                FileFingerprints.EnsureUnchanged(input,
                    new Aspose.Cli.Sdk.Contracts.FileFingerprint { Sha256 = expectedInput.Sha256!.ToLowerInvariant() },
                    FileFingerprints.Capture(input));
            }
            plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-prepare");
            EnsureTargetUnchanged(entry);
            if (entry.RequestedBackup is { } backup && entry.RequestedBackupOriginal is { } expectedBackup)
            {
                FilePublicationSnapshot current = FilePublicationSnapshot.Capture(backup);
                if (!expectedBackup.VersionEquals(current)) { throw CliErrors.OutputConflict(backup, expectedBackup, current); }
            }
            if (entry.Original.Exists)
            {
                PrepareBackup(entry);
                PrepareRequestedBackup(entry);
            }

            // Backup paths were recorded when the set was sealed; recovery verifies a backup
            // against the original, so one record after every backup exists is enough.
            entry.State = PublicationEntryState.Prepared;
        }

        plan.Transition(PublicationTransactionState.Prepared);
    }

    private void CreateBackupDirectory()
    {
        string backups = Path.Combine(plan.StagingDirectory, "backups");
        Directory.CreateDirectory(backups);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                backups,
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute);
        }
    }

    private void PrepareBackup(PublicationJournalEntry entry)
    {
        plan.Faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Backup,
            entry.Index,
            entry.Target));
        string backup = entry.Backup
            ?? throw new InvalidOperationException($"Publication entry '{entry.Target}' has no backup path.");
        File.Copy(entry.Target, backup, overwrite: false);
        DurableFile.Flush(backup);
        entry.Original.Metadata?.ApplyContentAttributes(backup);
        if (!entry.Original.ContentMatches(backup))
        {
            throw new IOException(
                $"Backup verification failed for '{entry.Target}'.");
        }
        entry.BackupSnapshot = FilePublicationSnapshot.Capture(backup);
    }

    private static void PrepareRequestedBackup(PublicationJournalEntry entry)
    {
        if (entry.RequestedBackup is not { } requestedBackup
            || File.Exists(requestedBackup))
        {
            return;
        }

        OutputPathValidator.EnsureParentUnchanged(
            requestedBackup,
            entry.RequestedBackupParentIdentity);

        string? requestedDirectory = Path.GetDirectoryName(requestedBackup);
        if (requestedDirectory is not null)
        {
            Directory.CreateDirectory(requestedDirectory);
        }

        File.Copy(entry.Target, requestedBackup, overwrite: false);
        DurableFile.Flush(requestedBackup);
        entry.Original.Metadata?.ApplyContentAttributes(requestedBackup);
        OutputPathValidator.EnsureParentUnchanged(
            requestedBackup,
            entry.RequestedBackupParentIdentity);
    }

    private void Publish(PublicationJournalEntry entry)
    {
        plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-file");
        plan.Faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Publish,
            entry.Index,
            entry.Target));
        if (entry.DeleteTarget)
        {
            PublishDeletion(entry);
            return;
        }

        plan.Lease?.EnsureCovers(entry.Target);
        OutputPathValidator.EnsureParentUnchanged(
            entry.Target,
            entry.TargetParentIdentity);
        plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-replace");
        entry.PublishedSnapshot = FilePublicationAtomicSwap.Publish(
            entry.Staged,
            entry.Target,
            entry.Overwrite,
            entry.Original,
            entry.Original,
            entry.Displaced,
            entry.StagedSnapshot);
        if (OperatingSystem.IsWindows() && !entry.Original.Exists)
        {
            FilePublicationMetadata.ResetAccessToInherited(entry.Target);
            entry.PublishedSnapshot = FilePublicationSnapshot.Capture(entry.Target);
        }
        plan.ResourceBudgets?.RefreshAdmissionAfterPublication(entry.Target);
        CaptureDisplaced(entry);
        if (!entry.StagedSnapshot.ContentMatches(entry.Target))
        {
            throw new IOException(
                $"Published target '{entry.Target}' did not match its staged content.");
        }

        // Recorded durably with the commit; until then recovery recognizes the swap by evidence.
        entry.State = PublicationEntryState.Published;
    }

    private void PublishDeletion(PublicationJournalEntry entry)
    {
        if (entry.Displaced is null)
        {
            throw new InvalidOperationException(
                $"Deletion transaction for '{entry.Target}' has no displaced path.");
        }
        plan.Lease?.EnsureCovers(entry.Target);
        OutputPathValidator.EnsureParentUnchanged(
            entry.Target,
            entry.TargetParentIdentity);
        FilePublicationSnapshot current =
            FilePublicationSnapshot.Capture(entry.Target);
        if (!entry.Original.VersionEquals(current))
        {
            throw CliErrors.OutputConflict(
                entry.Target,
                entry.Original,
                current);
        }
        plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-delete");
        File.Move(entry.Target, entry.Displaced, overwrite: false);
        FilePublicationSnapshot displaced =
            FilePublicationSnapshot.Capture(entry.Displaced);
        if (!entry.Original.VersionEquals(displaced))
        {
            if (!File.Exists(entry.Target))
            {
                File.Move(entry.Displaced, entry.Target, overwrite: false);
            }
            throw CliErrors.OutputConflict(
                entry.Target,
                entry.Original,
                FilePublicationSnapshot.Capture(entry.Target));
        }

        entry.DisplacedSnapshot = displaced;
        entry.PublishedSnapshot = FilePublicationSnapshot.Missing;
        entry.State = PublicationEntryState.Published;
    }

    private static void CaptureDisplaced(PublicationJournalEntry entry)
    {
        if (entry.Displaced is null)
        {
            return;
        }
        FilePublicationSnapshot displaced =
            FilePublicationSnapshot.Capture(entry.Displaced);
        if (!entry.Original.VersionEquals(displaced))
        {
            throw new IOException(
                $"Displaced target verification failed for '{entry.Target}'.");
        }
        entry.DisplacedSnapshot = displaced;
    }

    private static void EnsureTargetUnchanged(PublicationJournalEntry entry)
    {
        OutputPathValidator.EnsureParentUnchanged(
            entry.Target,
            entry.TargetParentIdentity);
        FilePublicationSnapshot actual =
            FilePublicationSnapshot.Capture(entry.Target);
        if (!entry.Original.VersionEquals(actual))
        {
            throw CliErrors.OutputConflict(
                entry.Target,
                entry.Original,
                actual);
        }
    }
}
