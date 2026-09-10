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
            PublishToWorker();
        }
        else
        {
            Prepare();
            plan.Transition(PublicationTransactionState.Publishing);
            foreach (PublicationJournalEntry entry in plan.Journal.Entries)
            {
                Publish(entry);
            }
        }

        beforeCommit?.Invoke();
        plan.Transition(PublicationTransactionState.Committed);
        return plan.Journal.Entries
            .OrderBy(static entry => entry.Index)
            .Select(static entry => entry.Size)
            .ToArray();
    }

    private void PublishToWorker()
    {
        foreach (PublicationJournalEntry entry in plan.Journal.Entries)
        {
            WorkerOutputSession.RegisterFile(
                entry.Target,
                entry.Staged,
                entry.Overwrite,
                entry.RequestedBackup,
                entry.Original,
                entry.StagedSnapshot);
            entry.State = PublicationEntryState.Published;
            plan.Persist();
        }
    }

    private void Prepare()
    {
        string backups = CreateBackupDirectory();
        foreach (PublicationJournalEntry entry in plan.Journal.Entries)
        {
            EnsureTargetUnchanged(entry);
            if (entry.Original.Exists)
            {
                PrepareBackup(entry, backups);
                PrepareRequestedBackup(entry);
            }

            entry.State = PublicationEntryState.Prepared;
            plan.Persist();
        }

        plan.Transition(PublicationTransactionState.Prepared);
    }

    private string CreateBackupDirectory()
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
        return backups;
    }

    private void PrepareBackup(
        PublicationJournalEntry entry,
        string backups)
    {
        plan.Faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Backup,
            entry.Index,
            entry.Target));
        entry.Backup = Path.Combine(
            backups,
            $"{entry.Index + 1:000000}.backup");
        File.Copy(entry.Target, entry.Backup, overwrite: false);
        FilePublicationDurabilityAdapter.FlushFile(entry.Backup);
        entry.Original.Metadata?.ApplyContentAttributes(entry.Backup);
        if (!entry.Original.ContentMatches(entry.Backup))
        {
            throw new IOException(
                $"Backup verification failed for '{entry.Target}'.");
        }
        entry.BackupSnapshot = FilePublicationSnapshot.Capture(entry.Backup);
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
        FilePublicationDurabilityAdapter.FlushFile(requestedBackup);
        entry.Original.Metadata?.ApplyContentAttributes(requestedBackup);
        OutputPathValidator.EnsureParentUnchanged(
            requestedBackup,
            entry.RequestedBackupParentIdentity);
    }

    private void Publish(PublicationJournalEntry entry)
    {
        entry.State = PublicationEntryState.Publishing;
        plan.Persist();
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
        entry.PublishedSnapshot = FilePublicationAtomicSwap.Publish(
            entry.Staged,
            entry.Target,
            entry.Overwrite,
            entry.Original,
            entry.Original,
            entry.Displaced,
            entry.StagedSnapshot);
        CaptureDisplaced(entry);
        if (!entry.StagedSnapshot.ContentMatches(entry.Target))
        {
            throw new IOException(
                $"Published target '{entry.Target}' did not match its staged content.");
        }

        entry.State = PublicationEntryState.Published;
        plan.Persist();
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
        plan.Persist();
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
