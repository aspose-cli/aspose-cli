using Aspose.Cli.Sdk.Errors;
using Microsoft.Win32.SafeHandles;

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
            // An in-place edit's input is its target, so one snapshot serves both checks.
            FilePublicationSnapshot? target = null;
            if (entry.InputPath is { } input && entry.InputSnapshot is { } expectedInput)
            {
                string? actualInput;
                if (FilePaths.Same(input, entry.Target))
                {
                    target = CaptureInPlaceInput(input);
                    actualInput = target.Sha256;
                }
                else
                {
                    actualInput = FileFingerprints.Capture(input).Sha256;
                }
                FileFingerprints.EnsureUnchanged(input,
                    new Aspose.Cli.Sdk.Contracts.FileFingerprint { Sha256 = expectedInput.Sha256!.ToLowerInvariant() },
                    new Aspose.Cli.Sdk.Contracts.FileFingerprint { Sha256 = actualInput!.ToLowerInvariant() });
            }
            plan.ResourceBudgets?.Deadline.ThrowIfExpired("publication-prepare");
            EnsureTargetUnchanged(entry, target);
            if (entry.RequestedBackup is { } backup && entry.RequestedBackupOriginal is { } expectedBackup)
            {
                FilePublicationSnapshot current = FilePublicationSnapshot.Capture(backup);
                if (!expectedBackup.VersionEquals(current)) { throw PublicationErrors.OutputConflict(backup, expectedBackup, current); }
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
        entry.Original.Metadata?.Apply(backup);
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
        entry.Original.Metadata?.Apply(requestedBackup);
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
        byte[]? originalAccess = entry.Original.Exists
            ? FilePublicationInheritance.TryCaptureAccess(entry.Target)
            : null;
        (FilePublicationSnapshot published, FilePublicationSnapshot? displaced) = FilePublicationAtomicSwap.Publish(
            entry.Staged,
            entry.Target,
            entry.Overwrite,
            entry.Original,
            entry.Original,
            entry.Displaced,
            entry.StagedSnapshot);
        // The swap verified both files, so their snapshots are recorded without reading them again.
        entry.PublishedSnapshot = published;
        if (displaced is not null) { entry.DisplacedSnapshot = displaced; }
        plan.Faults.Hit(new PublicationFaultPoint(PublicationFaultKind.Access, entry.Index, entry.Target));
        // The replacement was staged in the private transaction directory: a new file takes the
        // folder's rules, a replaced file keeps the rules its owner had set.
        if (entry.Original.Exists) { FilePublicationInheritance.TryApplyAccess(entry.Target, originalAccess); }
        else { FilePublicationInheritance.TryReset(entry.Target); }
        EnsureStillPublished(entry, published);
        plan.ResourceBudgets?.RefreshAdmissionAfterPublication(entry.Target);

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
            throw PublicationErrors.OutputConflict(
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
            throw PublicationErrors.OutputConflict(
                entry.Target,
                entry.Original,
                FilePublicationSnapshot.Capture(entry.Target));
        }

        entry.DisplacedSnapshot = displaced;
        entry.PublishedSnapshot = FilePublicationSnapshot.Missing;
        entry.State = PublicationEntryState.Published;
    }

    /// <summary>
    /// Restoring access rules changes neither content nor identity, so the file the swap verified
    /// is still the target when it has the same identity and length; anything else replaced it.
    /// </summary>
    private static void EnsureStillPublished(PublicationJournalEntry entry, FilePublicationSnapshot published)
    {
        bool same;
        try
        {
            using SafeFileHandle handle = File.OpenHandle(
                entry.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            same = FilePublicationOwnedDelete.TryGetIdentity(handle) == published.PhysicalIdentity
                && RandomAccess.GetLength(handle) == published.Length;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            same = false;
        }
        if (!same)
        {
            // Recovery then treats what took the target's place as this publication's to roll back.
            entry.PublishedSnapshot = FilePublicationSnapshot.Capture(entry.Target);
            throw new IOException(
                $"Published target '{entry.Target}' did not match its staged content.");
        }
    }

    /// <summary>Reads an in-place input once for both checks, with the errors an input read reports.</summary>
    private static FilePublicationSnapshot CaptureInPlaceInput(string input)
    {
        try
        {
            FilePublicationSnapshot snapshot = FilePublicationSnapshot.Capture(input);
            return snapshot.Exists ? snapshot : throw CliErrors.FileNotFound(input);
        }
        catch (UnauthorizedAccessException)
        {
            throw CliErrors.FileAccessDenied(input);
        }
        catch (IOException)
        {
            throw File.Exists(input)
                ? CliErrors.FileLocked(input)
                : CliErrors.FileNotFound(input);
        }
    }

    private static void EnsureTargetUnchanged(PublicationJournalEntry entry, FilePublicationSnapshot? captured)
    {
        OutputPathValidator.EnsureParentUnchanged(
            entry.Target,
            entry.TargetParentIdentity);
        FilePublicationSnapshot actual =
            captured ?? FilePublicationSnapshot.Capture(entry.Target);
        if (!entry.Original.VersionEquals(actual))
        {
            throw PublicationErrors.OutputConflict(
                entry.Target,
                entry.Original,
                actual);
        }
    }
}
