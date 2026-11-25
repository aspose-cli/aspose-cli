using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>
/// Publishes every staged output in order and, when that fails, puts the targets back in the
/// reverse order.
/// </summary>
internal sealed class AtomicPublicationCommit(AtomicPublicationPlan plan)
{
    public IReadOnlyList<long> Execute()
    {
        if (plan.State != PublicationTransactionState.Staging)
        {
            throw new InvalidOperationException(
                $"Cannot commit a transaction in state {plan.State}.");
        }

        Prepare();
        RecordPublicationIntent();
        foreach (PublicationEntry entry in plan.Entries)
        {
            Publish(entry);
        }

        plan.ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
        plan.State = PublicationTransactionState.Committed;
        plan.ResourceBudgets.MarkOutputsCommitted();
        return plan.Entries
            .OrderBy(static entry => entry.Index)
            .Select(static entry => entry.Size)
            .ToArray();
    }

    /// <summary>
    /// Marks every target as possibly published before the first swap. The rollback tells a
    /// target that was never swapped from one that was by the staged and displaced files.
    /// </summary>
    private void RecordPublicationIntent()
    {
        foreach (PublicationEntry entry in plan.Entries)
        {
            entry.State = PublicationEntryState.Publishing;
        }
    }

    private void Prepare()
    {
        CreateBackupDirectory();
        foreach (PublicationEntry entry in plan.Entries)
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
            plan.ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
            EnsureTargetUnchanged(entry, target);

            // The swap keeps each replaced target at its displaced path, the only copy of the
            // original that the rollback and, after a partial rollback, the user restore from.
            entry.State = PublicationEntryState.Prepared;
        }
    }

    /// <summary>Creates the directory that receives the displaced targets.</summary>
    private void CreateBackupDirectory()
    {
        string backups = Path.Combine(plan.StagingDirectory, "backups");
        Directory.CreateDirectory(backups);
    }

    private void Publish(PublicationEntry entry)
    {
        plan.ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
        plan.Faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Publish,
            entry.Index,
            entry.Target));
        if (entry.DeleteTarget)
        {
            PublishDeletion(entry);
            return;
        }

        OutputPathValidator.EnsureParentUnchanged(
            entry.Target,
            entry.TargetParentIdentity);
        plan.ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
        // A secret was staged with current-user-only rules (CurrentUserFileAccess), which it keeps
        // wherever it is published. A replacement takes the replaced file's rules, so the file it
        // replaces is given them first.
        bool currentUserOnly = CurrentUserFileAccess.IsCurrentUserOnly(entry.Staged);
        if (currentUserOnly && entry.Original.Exists) { CurrentUserFileAccess.Restrict(entry.Target); }
        byte[]? originalAccess = entry.Original.Exists && !currentUserOnly
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
        if (!currentUserOnly)
        {
            if (entry.Original.Exists) { FilePublicationInheritance.TryApplyAccess(entry.Target, originalAccess); }
            else { FilePublicationInheritance.TryReset(entry.Target); }
        }
        EnsureStillPublished(entry, published);
        plan.ResourceBudgets.RefreshAdmissionAfterPublication(entry.Target);

        // Until the commit, the rollback recognizes the swap by evidence.
        entry.State = PublicationEntryState.Published;
    }

    private void PublishDeletion(PublicationEntry entry)
    {
        if (entry.Displaced is null)
        {
            throw new InvalidOperationException(
                $"Deletion transaction for '{entry.Target}' has no displaced path.");
        }
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
        plan.ResourceBudgets.Cancellation.ThrowIfCancellationRequested();
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
    private static void EnsureStillPublished(PublicationEntry entry, FilePublicationSnapshot published)
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
            // The rollback then treats what took the target's place as this publication's to roll back.
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

    private static void EnsureTargetUnchanged(PublicationEntry entry, FilePublicationSnapshot? captured)
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

    /// <summary>
    /// Puts every target back, last first, and reports what it did to each. A target that was
    /// not put back names where its original now is, when the publication moved it aside.
    /// </summary>
    public PublicationRecoveryReport RollBack()
    {
        var items = new List<PublicationRecoveryItem>();
        foreach (PublicationEntry entry in plan.Entries
                     .OrderByDescending(static item => item.Index))
        {
            items.Add(Located(RestoreOrReport(entry), entry));
        }

        items.Reverse();
        bool complete = items.All(static item =>
            item.Status is "restored" or "unchanged"
            && item.ContentVerified
            && item.MetadataVerified);
        plan.State = complete
            ? PublicationTransactionState.RolledBack
            : PublicationTransactionState.Partial;
        return new PublicationRecoveryReport(complete, items);
    }

    /// <summary>
    /// The item of a target that was not put back, with the path of its original when the swap
    /// moved the original aside and it is still there unchanged.
    /// </summary>
    internal static PublicationRecoveryItem Located(PublicationRecoveryItem item, PublicationEntry entry)
    {
        if (item.Status is "restored" or "unchanged" || entry.Displaced is null || !entry.Original.Exists)
        {
            return item;
        }
        try
        {
            return entry.Original.VersionEquals(FilePublicationSnapshot.Capture(entry.Displaced))
                ? item with { OriginalPath = entry.Displaced }
                : item;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return item;
        }
    }

    private PublicationRecoveryItem RestoreOrReport(
        PublicationEntry entry)
    {
        bool publicationAttempted =
            entry.State is not (PublicationEntryState.Staged or PublicationEntryState.Prepared
                or PublicationEntryState.Unchanged);
        try
        {
            return RestoreEntry(entry, publicationAttempted);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or CliException)
        {
            entry.State = PublicationEntryState.Unknown;
            return new PublicationRecoveryItem(
                entry.Target,
                entry.Original.Exists,
                publicationAttempted,
                "unknown",
                ContentVerified: false,
                MetadataVerified: false,
                Failure: exception is CliException cli
                    ? cli.Code.Name
                    : exception.GetType().Name);
        }
    }

    private PublicationRecoveryItem RestoreEntry(
        PublicationEntry entry,
        bool publicationAttempted)
    {
        FilePublicationSnapshot current =
            FilePublicationSnapshot.Capture(entry.Target);
        if (!publicationAttempted || SwapNeverStarted(entry, current))
        {
            entry.State = PublicationEntryState.Unchanged;
            return RecoveryItem(
                entry,
                published: false,
                "unchanged",
                contentVerified: true,
                metadataVerified: true);
        }

        if (entry.Original.VersionEquals(current))
        {
            if (!MetadataMatches(entry))
            {
                entry.Original.Metadata?.Apply(entry.Target);
            }
            bool metadataVerified = MetadataMatches(entry);
            entry.State = PublicationEntryState.Restored;
            return RecoveryItem(
                entry,
                published: true,
                "restored",
                contentVerified: true,
                metadataVerified,
                metadataVerified ? null : "metadata-mismatch");
        }

        FilePublicationSnapshot published = entry.PublishedSnapshot
            ?? RecognizeInterruptedPublication(entry, current);
        if (!published.VersionEquals(current))
        {
            throw new IOException(
                $"Target '{entry.Target}' has content that belongs to neither the original nor staged transaction.");
        }

        RestoreOriginal(entry, current);
        entry.Original.Metadata?.Apply(entry.Target);
        bool contentVerified = entry.Original.VersionEquals(FilePublicationSnapshot.Capture(entry.Target));
        bool restoredMetadata = MetadataMatches(entry);
        bool restored = contentVerified && restoredMetadata;
        entry.State = restored
            ? PublicationEntryState.Restored
            : PublicationEntryState.Unknown;
        return RecoveryItem(
            entry,
            publicationAttempted,
            restored ? "restored" : "unknown",
            contentVerified,
            restoredMetadata,
            restored ? null : "verification-failed");
    }

    /// <summary>
    /// Every swap first moves the target to its displaced path or moves the staged file onto
    /// the target. With no displaced file and the staged path still occupied, the target was
    /// never touched by this transaction when it is still the original file (as when the swap
    /// refused a replaced stage), or when the staged file is still the admitted one and the
    /// target does not hold its content. That content check keeps a copying move that was
    /// interrupted before it removed its source from passing for an untouched target.
    /// </summary>
    private static bool SwapNeverStarted(PublicationEntry entry, FilePublicationSnapshot current)
    {
        if ((entry.Displaced is not null && FilePublicationOwnedDelete.TryGetAttributesNoFollow(entry.Displaced) is not null)
            || !File.Exists(entry.Staged))
        {
            return false;
        }
        return entry.Original.VersionEquals(current)
            || (entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged))
                && !entry.StagedSnapshot.ContentEquals(current));
    }

    /// <summary>
    /// Whether a replacement moved the target to its displaced path and then could not put the
    /// staged file in its place, as ReplaceFile does when it fails with Win32 error 1177: the
    /// target is gone and the staged file is still the admitted one.
    /// </summary>
    private static bool ReplacementStoppedHalfway(PublicationEntry entry, FilePublicationSnapshot current) =>
        !entry.DeleteTarget
        && !current.Exists
        && entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged));

    private static FilePublicationSnapshot RecognizeInterruptedPublication(
        PublicationEntry entry,
        FilePublicationSnapshot current)
    {
        bool displacedOriginal = entry.Original.Exists
            && entry.Displaced is not null
            && entry.Original.VersionEquals(
                FilePublicationSnapshot.Capture(entry.Displaced));
        if (displacedOriginal && ReplacementStoppedHalfway(entry, current))
        {
            // Nothing was published, so no published snapshot is recorded.
            return current;
        }

        bool recognized = entry.DeleteTarget
            ? !current.Exists && displacedOriginal
            : entry.StagedSnapshot.VersionEquals(current)
                && (!entry.Original.Exists || displacedOriginal);
        if (!recognized)
        {
            throw new IOException(
                $"Target '{entry.Target}' has no durable publication evidence.");
        }

        entry.PublishedSnapshot = current;
        if (displacedOriginal)
        {
            entry.DisplacedSnapshot =
                FilePublicationSnapshot.Capture(entry.Displaced!);
        }
        return current;
    }

    private void RestoreOriginal(
        PublicationEntry entry,
        FilePublicationSnapshot publishedTarget)
    {
        plan.Faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Rollback,
            entry.Index,
            entry.Target));
        if (!entry.Original.Exists)
        {
            if (!FilePublicationOwnedDelete.TryDelete(
                    entry.Target,
                    publishedTarget))
            {
                throw new IOException(
                    $"Published target '{entry.Target}' changed and was not deleted during recovery.");
            }
            return;
        }

        if (entry.Displaced is null
            || !entry.Original.VersionEquals(
                FilePublicationSnapshot.Capture(entry.Displaced)))
        {
            throw new IOException(
                $"Verified displaced target is unavailable for '{entry.Target}'.");
        }

        plan.Faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.Metadata,
            entry.Index,
            entry.Target));
        if (!publishedTarget.Exists)
        {
            if (File.Exists(entry.Target) || Directory.Exists(entry.Target))
            {
                throw new IOException(
                    $"Deleted target '{entry.Target}' was recreated during recovery.");
            }
            File.Move(entry.Displaced, entry.Target, overwrite: false);
            return;
        }

        if (FilePublicationOwnedDelete.TryGetAttributesNoFollow(entry.Staged)
            is not null)
        {
            throw new IOException(
                $"Publication staging path '{entry.Staged}' is occupied during recovery.");
        }

        FilePublicationAtomicSwap.Publish(
            entry.Displaced,
            entry.Target,
            overwrite: true,
            expectedTarget: publishedTarget,
            metadataSource: entry.Original,
            displacedPath: entry.Staged,
            expectedStage: entry.Original);
    }

    private static bool MetadataMatches(PublicationEntry entry) =>
        !entry.Original.Exists
        || entry.Original.Metadata is null
        || entry.Original.Metadata.Matches(entry.Target);

    private static PublicationRecoveryItem RecoveryItem(
        PublicationEntry entry,
        bool published,
        string status,
        bool contentVerified,
        bool metadataVerified,
        string? failure = null) =>
        new(
            entry.Target,
            entry.Original.Exists,
            published,
            status,
            contentVerified,
            metadataVerified,
            failure);
}
