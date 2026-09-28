using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Restores failed or abandoned publication transactions with evidence.</summary>
internal sealed class AtomicPublicationRecovery(AtomicPublicationPlan plan)
{
    private const int MaximumTransactionDirectories = 32;
    private static readonly TimeSpan MaximumRecoveryTime = TimeSpan.FromSeconds(2);
    public PublicationRecoveryReport RollBack()
    {
        using OperationDeadline cleanup = OperationDeadline.Start(TimeSpan.FromSeconds(30));
        plan.Journal.State = PublicationTransactionState.RollingBack;
        plan.TryPersist(cleanup);
        // Restoring an entry is idempotent and decided by evidence on disk, so an interrupted
        // rollback is repeated from this record rather than from a record per entry.
        var items = new List<PublicationRecoveryItem>();
        foreach (PublicationJournalEntry entry in plan.Journal.Entries
                     .OrderByDescending(static item => item.Index))
        {
            items.Add(RestoreOrReport(entry));
        }

        items.Reverse();
        bool complete = items.All(static item =>
            item.Status is "restored" or "unchanged"
            && item.ContentVerified
            && item.MetadataVerified);
        plan.Journal.State = complete
            ? PublicationTransactionState.RolledBack
            : PublicationTransactionState.Partial;
        if (!plan.TryPersist(cleanup))
        {
            plan.Journal.State = PublicationTransactionState.Partial;
            complete = false;
        }
        return new PublicationRecoveryReport(complete, items);
    }

    public static int RecoverPending(string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        string root = Path.GetFullPath(targetDirectory);
        if (!Directory.Exists(root))
        {
            return 0;
        }

        return RecoverPendingUnderLease(root, lease: null);
    }

    /// <summary>
    /// Recovers abandoned transactions in one directory. Without <paramref name="scope"/>
    /// every transaction there is recovered and any it cannot recover blocks the caller.
    /// With a scope, the directory is an ancestor of a publication: only transactions whose
    /// journal names a path overlapping the scope are recovered, because unrelated ones are
    /// recovered by publications into their own directory.
    /// </summary>
    internal static int RecoverPendingUnderLease(
        string targetDirectory,
        PublicationDirectoryLease? lease, OperationDeadline? invocationDeadline = null, string? scope = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        lease?.EnsureDirectoryUnchanged();
        string root = Path.GetFullPath(targetDirectory);
        if (!Directory.Exists(root))
        {
            return 0;
        }

        long deadline = Stopwatch.GetTimestamp()
            + (long)(MaximumRecoveryTime.TotalSeconds * Stopwatch.Frequency);
        string[] directories = EnumerateTransactions(root, deadline);
        if (scope is not null)
        {
            directories = Array.FindAll(directories, directory => Concerns(directory, scope, invocationDeadline));
        }
        if (directories.Length > MaximumTransactionDirectories)
        {
            throw CliErrors.OutputUnwritable(
                root,
                "the abandoned publication directory budget was exceeded",
                phase: "recovery");
        }

        int recovered = 0;
        foreach (string directory in directories)
        {
            if (Stopwatch.GetTimestamp() > deadline)
            {
                throw CliErrors.OutputUnwritable(
                    root,
                    "the abandoned publication recovery time budget was exceeded",
                    phase: "recovery");
            }
            try
            {
                invocationDeadline?.ThrowIfExpired("publication-recovery");
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (TryRecover(root, directory, lease, invocationDeadline)) { recovered++; }
                        break;
                    }
                    catch (PublicationLeaseExpansionException) when (lease is null && attempt < 2) { }
                }
            }
            catch (Exception exception) when (
                exception is DirectoryNotFoundException or FileNotFoundException
                && !Directory.Exists(directory))
            {
                // A concurrent owner or recovery completed after directory discovery.
            }
            catch (InvalidDataException exception)
            {
                throw CliErrors.OutputUnwritable(
                    root,
                    "an abandoned publication journal failed validation",
                    exception,
                    phase: "recovery");
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException)
            {
                throw CliErrors.OutputUnwritable(
                    root,
                    "an abandoned publication could not be recovered",
                    exception,
                    phase: "recovery");
            }
        }
        return recovered;
    }

    internal static int RecoverPendingHierarchy(string targetDirectory, OperationDeadline? deadline)
    {
        string target = Path.GetFullPath(targetDirectory);
        int recovered = 0;
        for (string? current = target; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current))
            {
                recovered += RecoverPendingUnderLease(current, null, deadline, current == target ? null : target);
            }
        }
        return recovered;
    }

    internal static int RecoverPendingHierarchyUnderLease(
        string targetDirectory,
        PublicationDirectoryLease lease)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        ArgumentNullException.ThrowIfNull(lease);
        var hierarchy = new List<string>();
        string target = Path.GetFullPath(targetDirectory);
        string? current = target;
        while (current is not null)
        {
            hierarchy.Add(current);
            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }
            current = parent;
        }

        int recovered = 0;
        foreach (string directory in hierarchy.AsEnumerable().Reverse())
        {
            if (Directory.Exists(directory))
            {
                recovered += RecoverPendingUnderLease(directory, lease, scope: directory == target ? null : target);
            }
        }
        return recovered;
    }

    private PublicationRecoveryItem RestoreOrReport(
        PublicationJournalEntry entry)
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
        PublicationJournalEntry entry,
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
    /// the target. A staged file that is still the admitted one, with no displaced file and a
    /// target that does not hold the staged content, proves the target was never touched by
    /// this transaction. The content check keeps a copying move that was interrupted before
    /// it removed its source from passing for an untouched target.
    /// </summary>
    private static bool SwapNeverStarted(PublicationJournalEntry entry, FilePublicationSnapshot current) =>
        (entry.Displaced is null || FilePublicationOwnedDelete.TryGetAttributesNoFollow(entry.Displaced) is null)
        && File.Exists(entry.Staged)
        && entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged))
        && !entry.StagedSnapshot.ContentEquals(current);

    private static FilePublicationSnapshot RecognizeInterruptedPublication(
        PublicationJournalEntry entry,
        FilePublicationSnapshot current)
    {
        bool displacedOriginal = entry.Original.Exists
            && entry.Displaced is not null
            && entry.Original.VersionEquals(
                FilePublicationSnapshot.Capture(entry.Displaced));
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
        PublicationJournalEntry entry,
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
            retainedDisplacedPath: entry.Staged,
            expectedStage: entry.Original);
    }

    private static bool TryRecover(string root, string directory, PublicationDirectoryLease? suppliedLease, OperationDeadline? deadline)
    {
        ValidateTransactionDirectory(root, directory);
        string journalPath = Path.Combine(
            directory,
            AtomicPublicationPlan.JournalName);
        if (!File.Exists(journalPath))
        {
            if (IsLiveCreation(directory)) { return false; }
            using PublicationDirectoryLease? orphanLease = suppliedLease is null
                ? PublicationDirectoryLease.Acquire(root, [root], deadline) : null;
            if (!(suppliedLease ?? orphanLease!).CoversDirectories([root])) { throw new PublicationLeaseExpansionException(); }
            if (!Directory.Exists(directory) || File.Exists(journalPath) || IsLiveCreation(directory)) { return false; }
            return CleanOrphanJournalTemporaries(directory);
        }
        PrivateUserStorage.ValidateFile(journalPath);
        if (!TryReadJournal(journalPath, out PublicationJournal? journal, deadline))
        {
            return false;
        }

        if (IsOwnerAlive(journal) && journal.State is not (PublicationTransactionState.Committed or PublicationTransactionState.RolledBack)) { return false; }
        if (journal.DirectoryOutput is not null)
        { return NewDirectoryPublication.Recover(root, directory, journal, suppliedLease, deadline); }
        ValidateJournal(root, AtomicPublicationPlan.Open(directory, journal));
        using PublicationDirectoryLease? ownedLease = suppliedLease is null
            ? PublicationDirectoryLease.Acquire(root, AtomicPublicationPlan.ResourceDirectories(journal), deadline) : null;
        PublicationDirectoryLease lease = suppliedLease ?? ownedLease!;
        if (!File.Exists(journalPath)) { return false; }
        journal = PublicationJournal.Read(journalPath, deadline: deadline);
        if (!lease.CoversDirectories(AtomicPublicationPlan.ResourceDirectories(journal))) { throw new PublicationLeaseExpansionException(); }
        if (IsOwnerAlive(journal) && journal.State is not (PublicationTransactionState.Committed or PublicationTransactionState.RolledBack)) { return false; }
        var recoveryPlan = AtomicPublicationPlan.Open(directory, journal);
        ValidateJournal(root, recoveryPlan);
        if (journal.State == PublicationTransactionState.Partial)
        {
            throw CliErrors.OutputPublicationFailure(
                new IOException(
                    $"Abandoned publication '{directory}' has an incomplete prior recovery."),
                new PublicationRecoveryReport(
                    RecoveryComplete: false,
                    journal.Entries
                        .OrderBy(static entry => entry.Index)
                        .Select(static entry => new PublicationRecoveryItem(
                            entry.Target,
                            entry.Original.Exists,
                            entry.State is PublicationEntryState.Publishing
                                or PublicationEntryState.Published,
                            "unknown",
                            ContentVerified: false,
                            MetadataVerified: false,
                            Failure: "prior-recovery-incomplete"))
                        .ToArray()));
        }
        if (recoveryPlan.IsTerminal)
        {
            recoveryPlan.CleanUp(throwOnFailure: true);
            return false;
        }
        if (IsOwnerAlive(journal))
        {
            return false;
        }

        var recovery = new AtomicPublicationRecovery(recoveryPlan);
        foreach (PublicationJournalEntry entry in journal.Entries)
        {
            if (entry.Backup is not null
                && entry.BackupSnapshot is null
                && File.Exists(entry.Backup)
                && entry.Original.ContentMatches(entry.Backup))
            {
                entry.BackupSnapshot = FilePublicationSnapshot.Capture(entry.Backup);
            }
            if (entry.Displaced is not null
                && entry.DisplacedSnapshot is null
                && File.Exists(entry.Displaced)
                && entry.Original.VersionEquals(
                    FilePublicationSnapshot.Capture(entry.Displaced)))
            {
                entry.DisplacedSnapshot =
                    FilePublicationSnapshot.Capture(entry.Displaced);
            }
        }
        PublicationRecoveryReport report = recovery.RollBack();
        if (!report.RecoveryComplete)
        {
            throw CliErrors.OutputPublicationFailure(
                new IOException(
                    $"Abandoned publication '{directory}' requires recovery."),
                report);
        }

        recoveryPlan.CleanUp(throwOnFailure: true);
        return true;
    }

    private static bool IsLiveCreation(string directory)
    {
        string[] parts = Path.GetFileName(directory).Split('-');
        if (parts.Length != 5 || parts[0] != ".aspose" || parts[1] != "publication"
            || !int.TryParse(parts[2], out int pid) || pid <= 0
            || !long.TryParse(parts[3], out long ticks) || ticks <= 0
            || !Guid.TryParseExact(parts[4], "N", out _)) { return false; }
        try
        {
            using Process process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime().Ticks == ticks;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }
    }

    private static void ValidateTransactionDirectory(
        string root,
        string directory)
    {
        string fullRoot = Path.GetFullPath(root);
        string fullDirectory = Path.GetFullPath(directory);
        string parent = Path.GetDirectoryName(fullDirectory)
            ?? throw new InvalidDataException(
                $"Publication transaction '{directory}' has no parent directory.");
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(fullRoot, parent, comparison)
            || File.GetAttributes(fullDirectory).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException(
                $"Publication transaction '{directory}' is outside its recovery root or is a reparse point.");
        }
        PrivateUserStorage.ValidateDirectory(fullDirectory);
    }

    private static void ValidateJournal(
        string root,
        AtomicPublicationPlan recoveryPlan)
    {
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var targets = new HashSet<string>(comparer);
        var indexes = new HashSet<int>();
        string transaction = Path.GetFullPath(recoveryPlan.StagingDirectory);
        string backupDirectory = Path.Combine(transaction, "backups");
        foreach (PublicationJournalEntry? entry in recoveryPlan.Journal.Entries)
        {
            if (entry is null
                || string.IsNullOrWhiteSpace(entry.Target)
                || string.IsNullOrWhiteSpace(entry.Staged)
                || !entry.Original.IsStructurallyValid()
                || !entry.StagedSnapshot.IsStructurallyValid()
                || entry.DeleteTarget && !entry.Original.Exists
                || entry.DeleteTarget && entry.Displaced is null
                || entry.DisplacedSnapshot is not null
                    && !entry.DisplacedSnapshot.IsStructurallyValid()
                || entry.PublishedSnapshot is not null
                    && !entry.PublishedSnapshot.IsStructurallyValid()
                || entry.PublishedSnapshot is not null
                    && entry.DeleteTarget == entry.PublishedSnapshot.Exists
                || OperatingSystem.IsWindows()
                    && entry.TargetParentIdentity is null
                || entry.RequestedBackup is not null
                    && OperatingSystem.IsWindows()
                    && entry.RequestedBackupParentIdentity is null)
            {
                throw new InvalidDataException(
                    $"Publication journal '{recoveryPlan.JournalPath}' contains an incomplete entry.");
            }
            string target = Path.GetFullPath(entry.Target);
            string targetParent = Path.GetDirectoryName(target)
                ?? throw new InvalidDataException(
                    $"Publication target '{entry.Target}' has no parent directory.");
            string staged = Path.GetFullPath(entry.Staged);
            string expectedStaged = AtomicPublicationPlan.StagedPath(
                transaction,
                target,
                entry.Index);
            string? expectedBackup = entry.Backup is null
                ? null
                : Path.Combine(
                    backupDirectory,
                    $"{entry.Index + 1:000000}.backup");
            string? expectedDisplaced = entry.Displaced is null
                ? null
                : Path.Combine(
                    backupDirectory,
                    $"{entry.Index + 1:000000}.displaced");
            if (!indexes.Add(entry.Index)
                || entry.Index < 0
                || !targets.Add(target)
                || !IsWithinRoot(root, targetParent)
                || !comparer.Equals(staged, expectedStaged)
                || !Enum.IsDefined(entry.State)
                || (entry.State == PublicationEntryState.Published
                    && entry.PublishedSnapshot is null))
            {
                throw new InvalidDataException(
                    $"Publication journal '{recoveryPlan.JournalPath}' contains an invalid entry.");
            }
            if (entry.Backup is not null
                && !comparer.Equals(
                    Path.GetFullPath(entry.Backup),
                    expectedBackup))
            {
                throw new InvalidDataException(
                    $"Publication backup '{entry.Backup}' is outside its transaction directory.");
            }
            if (entry.Displaced is not null
                && !comparer.Equals(
                    Path.GetFullPath(entry.Displaced),
                    expectedDisplaced))
            {
                throw new InvalidDataException(
                    $"Displaced publication target '{entry.Displaced}' is outside its transaction directory.");
            }
            OutputPathValidator.EnsureSafeFile(target);
            OutputPathValidator.EnsureParentUnchanged(
                target,
                entry.TargetParentIdentity);
            if (entry.RequestedBackup is not null)
            {
                string requestedBackup = Path.GetFullPath(entry.RequestedBackup);
                if (!IsWithinRoot(root, Path.GetDirectoryName(requestedBackup)!))
                {
                    throw new InvalidDataException(
                        $"Requested backup '{requestedBackup}' is outside its transaction root.");
                }
                OutputPathValidator.EnsureSafeFile(requestedBackup);
                OutputPathValidator.EnsureParentUnchanged(
                    requestedBackup,
                    entry.RequestedBackupParentIdentity);
            }
            ValidateStagedFileIfPresent(entry, staged);
            if (entry.Backup is not null)
            {
                ValidatePrivateFileIfPresent(entry.Backup);
            }
            if (entry.Displaced is not null)
            {
                ValidateDisplacedFileIfPresent(entry);
            }
        }
        if (!Enum.IsDefined(recoveryPlan.Journal.State))
        {
            throw new InvalidDataException(
                $"Publication journal '{recoveryPlan.JournalPath}' has an invalid state.");
        }
    }

    private static string[] EnumerateTransactions(string root, long deadline)
    {
        var directories = new List<string>(MaximumTransactionDirectories + 1);
        foreach (string directory in Directory.EnumerateDirectories(
                     root,
                     ".aspose-publication-*"))
        {
            if (Stopwatch.GetTimestamp() > deadline)
            {
                throw CliErrors.OutputUnwritable(
                    root,
                    "the abandoned publication recovery time budget was exceeded",
                    phase: "recovery");
            }
            if (IsPrivateToCurrentUser(directory) && HasJournalOrTemporary(directory))
            {
                directories.Add(directory);
                if (directories.Count > MaximumTransactionDirectories)
                {
                    break;
                }
            }
        }
        directories.Sort(StringComparer.Ordinal);
        return [.. directories];
    }

    /// <summary>
    /// Every transaction this code creates is private to its user. Anything else under the
    /// transaction name was created by another principal: it is neither recoverable nor
    /// allowed to block this user's publications.
    /// </summary>
    private static bool IsPrivateToCurrentUser(string directory)
    {
        try
        {
            PrivateUserStorage.ValidateDirectory(directory);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Returns whether a readable journal names a path overlapping <paramref name="scope"/>.</summary>
    private static bool Concerns(string directory, string scope, OperationDeadline? deadline)
    {
        string journalPath = Path.Combine(directory, AtomicPublicationPlan.JournalName);
        PublicationJournal journal;
        try
        {
            if (!File.Exists(journalPath)) { return false; }
            journal = PublicationJournal.Read(journalPath, deadline: deadline);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException
                or InvalidDataException or System.Text.Json.JsonException)
        {
            return false;
        }

        IEnumerable<string?> paths = journal.Entries
            .SelectMany(static entry => new[] { entry.Target, entry.RequestedBackup })
            .Append(journal.DirectoryOutput?.Target);
        return paths.Any(path => path is not null && (IsWithinRoot(scope, path) || IsWithinRoot(path, scope)));
    }

    private static bool IsWithinRoot(string root, string path)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(path);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(fullRoot, fullPath, comparison)
            || fullPath.StartsWith(
                fullRoot + Path.DirectorySeparatorChar,
                comparison);
    }

    private static void ValidatePrivateFileIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            throw new InvalidDataException(
                $"Publication file path '{path}' is occupied by a directory.");
        }
        if (File.Exists(path))
        {
            PrivateUserStorage.ValidateFile(path);
        }
    }

    private static void ValidateStagedFileIfPresent(
        PublicationJournalEntry entry,
        string path)
    {
        OutputPathValidator.EnsureSafeFile(path);
        if (File.Exists(path)
            && !entry.StagedSnapshot.VersionEquals(
                FilePublicationSnapshot.Capture(path)))
        {
            throw new InvalidDataException(
                $"Publication staged file changed after admission: '{path}'.");
        }
    }

    private static void ValidateDisplacedFileIfPresent(
        PublicationJournalEntry entry)
    {
        string path = entry.Displaced!;
        OutputPathValidator.EnsureSafeFile(path);
        if (File.Exists(path)
            && !entry.Original.VersionEquals(
                FilePublicationSnapshot.Capture(path)))
        {
            throw new InvalidDataException(
                $"Displaced publication target changed after admission: '{path}'.");
        }
    }

    private static bool HasJournalOrTemporary(string directory)
    {
        try
        {
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) { return false; }
            if (File.Exists(Path.Combine(directory, AtomicPublicationPlan.JournalName))) { return true; }
            // A private candidate set without its first durable journal never reached publication.
            // Only an otherwise empty journal-temporary directory is eligible for orphan cleanup.
            bool hasTemporary = false;
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (Directory.Exists(path) || !PublicationJournal.IsTemporaryPath(path)) { return false; }
                hasTemporary = true;
            }
            return hasTemporary;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CleanOrphanJournalTemporaries(string directory)
    {
        string[] entries = Directory.EnumerateFileSystemEntries(
            directory,
            "*",
            SearchOption.TopDirectoryOnly).ToArray();
        if (entries.Any(path => Directory.Exists(path)
                || !PublicationJournal.IsTemporaryPath(path)))
        {
            return false;
        }
        foreach (string path in entries)
        {
            PrivateUserStorage.ValidateFile(path);
            FilePublicationSnapshot snapshot =
                FilePublicationSnapshot.Capture(path);
            if (!FilePublicationOwnedDelete.TryDelete(path, snapshot))
            {
                throw new IOException(
                    $"Orphan journal temporary changed during cleanup: '{path}'.");
            }
        }
        Directory.Delete(directory, recursive: false);
        return true;
    }

    private static bool TryReadJournal(
        string path,
        [NotNullWhen(true)]
        out PublicationJournal? journal,
        OperationDeadline? deadline = null)
    {
        journal = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            // A journal whose lock a live process holds belongs to a transaction in use, not an
            // abandoned one; discovery passes it rather than wait for a busy owner.
            journal = PublicationJournal.ReadUnlessBusy(path, deadline);
            return journal is not null;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException && !File.Exists(path))
        {
            // A terminal transaction may finish cleanup between discovery and the read.
            return false;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or System.Text.Json.JsonException)
        {
            throw new InvalidDataException(
                $"Publication journal '{path}' could not be validated.",
                exception);
        }
    }

    private static bool MetadataMatches(PublicationJournalEntry entry) =>
        !entry.Original.Exists
        || entry.Original.Metadata is null
        || entry.Original.Metadata.Matches(entry.Target);

    private static PublicationRecoveryItem RecoveryItem(
        PublicationJournalEntry entry,
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

    private static bool IsOwnerAlive(PublicationJournal journal)
    {
        try
        {
            using Process process =
                Process.GetProcessById(journal.OwnerProcessId);
            return journal.OwnerProcessStartUtcTicks == 0
                || process.StartTime.ToUniversalTime().Ticks
                    == journal.OwnerProcessStartUtcTicks;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // A reused PID now belongs to a process this user may not inspect; like
            // IsLiveCreation, treat the owner as alive rather than roll back its work.
            return true;
        }
    }

}
