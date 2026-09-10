using System.Diagnostics;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Owns one immutable publication location and its durable journal.</summary>
internal sealed class AtomicPublicationPlan
{
    internal const string JournalName = "publication-journal.v1.json";

    private readonly IPublicationFaultInjector _faults;
    private PublicationDirectoryLease? _lease;

    private AtomicPublicationPlan(
        string stagingDirectory,
        PublicationJournal journal,
        IPublicationFaultInjector faults,
        bool workerStagingOnly,
        PublicationDirectoryLease? lease)
    {
        StagingDirectory = stagingDirectory;
        JournalPath = Path.Combine(stagingDirectory, JournalName);
        Journal = journal;
        _faults = faults;
        WorkerStagingOnly = workerStagingOnly;
        _lease = lease;
    }

    public string StagingDirectory { get; }

    public string JournalPath { get; }

    public PublicationJournal Journal { get; }

    public IPublicationFaultInjector Faults => _faults;

    public bool WorkerStagingOnly { get; }

    public PublicationDirectoryLease? Lease => _lease;

    public string StagedPath(string target, int index) =>
        WorkerStagingOnly
            ? Path.Combine(
                StagingDirectory,
                $"{index + 1:000000}.stage")
            : StagedPath(StagingDirectory, target, index);

    public string DisplacedPath(int index) =>
        Path.Combine(
            StagingDirectory,
            "backups",
            $"{index + 1:000000}.displaced");

    public bool IsTerminal =>
        Journal.State is PublicationTransactionState.Committed
            or PublicationTransactionState.RolledBack
            or PublicationTransactionState.Partial;

    public bool CanCleanUp =>
        Journal.State is PublicationTransactionState.Committed
            or PublicationTransactionState.RolledBack;

    public static AtomicPublicationPlan Create(
        string targetDirectory,
        string operation,
        IPublicationFaultInjector faults)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        ArgumentException.ThrowIfNullOrEmpty(operation);
        string root = Path.GetFullPath(targetDirectory);
        bool workerStagingOnly = WorkerOutputSession.IsActive;
        PublicationDirectoryLease? lease = null;
        try
        {
            if (!workerStagingOnly)
            {
                lease = PublicationDirectoryLease.Acquire(root);
                AtomicPublicationRecovery.RecoverPendingHierarchyUnderLease(
                    root,
                    lease);
            }
            string stagingDirectory = workerStagingOnly
                ? WorkerOutputSession.CreatePrivateDirectory(operation)
                : CreateLocalStagingDirectory(root);
            var journal = new PublicationJournal
            {
                Operation = operation,
                State = PublicationTransactionState.Created,
            };
            var plan = new AtomicPublicationPlan(
                stagingDirectory,
                journal,
                faults,
                workerStagingOnly,
                lease);
            plan.Persist();
            plan.Transition(PublicationTransactionState.Staging);
            lease = null;
            return plan;
        }
        finally
        {
            lease?.Dispose();
        }
    }

    public static AtomicPublicationPlan Open(
        string stagingDirectory,
        PublicationJournal journal) =>
        new(
            stagingDirectory,
            journal,
            NoPublicationFaultInjector.Instance,
            workerStagingOnly: false,
            lease: null);

    internal static string StagedPath(
        string transactionDirectory,
        string target,
        int index)
    {
        string transaction = Path.GetFileName(
            Path.GetFullPath(transactionDirectory)).TrimStart('.');
        return Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(target))
                ?? throw new IOException(
                    $"Publication target '{target}' has no parent directory."),
            $".{transaction}.{index + 1:000000}.stage");
    }

    public void ReleaseLease()
    {
        PublicationDirectoryLease? lease = Interlocked.Exchange(ref _lease, null);
        lease?.Dispose();
    }

    public void Transition(PublicationTransactionState state)
    {
        Journal.State = state;
        Persist();
    }

    public void Persist()
    {
        _faults.Hit(new PublicationFaultPoint(
            PublicationFaultKind.JournalWrite,
            -1,
            JournalPath));
        Journal.Write(JournalPath);
    }

    public void TryPersist()
    {
        try
        {
            Persist();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Publication journal update failed for '{0}' ({1}).",
                JournalPath,
                exception.GetType().Name);
        }
    }

    public void CleanUp(bool throwOnFailure = false)
    {
        try
        {
            _faults.Hit(new PublicationFaultPoint(
                PublicationFaultKind.Cleanup,
                -1,
                StagingDirectory));
            EnsureNoUnknownArtifacts();
            foreach (PublicationJournalEntry entry in Journal.Entries)
            {
                DeleteKnownFile(entry.Staged, entry.StagedSnapshot);
                if (entry.Backup is not null)
                {
                    DeleteKnownFile(entry.Backup, entry.BackupSnapshot);
                }
                if (entry.Displaced is not null)
                {
                    DeleteKnownFile(
                        entry.Displaced,
                        entry.DisplacedSnapshot ?? entry.Original);
                }
            }

            string backups = Path.Combine(StagingDirectory, "backups");
            DeleteEmptyDirectory(backups);
            if (File.Exists(JournalPath))
            {
                FilePublicationSnapshot journal =
                    FilePublicationSnapshot.Capture(JournalPath);
                DeleteKnownFile(JournalPath, journal);
            }
            DeleteEmptyDirectory(StagingDirectory);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Publication cleanup failed for transaction journal '{0}' ({1}).",
                JournalPath,
                exception.GetType().Name);
            if (throwOnFailure)
            {
                throw;
            }
        }
    }

    private void EnsureNoUnknownArtifacts()
    {
        DeleteJournalTemporaries();
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var allowedFiles = new HashSet<string>(comparer)
        {
            Path.GetFullPath(JournalPath),
        };
        foreach (PublicationJournalEntry entry in Journal.Entries)
        {
            allowedFiles.Add(Path.GetFullPath(entry.Staged));
            if (entry.Backup is not null)
            {
                allowedFiles.Add(Path.GetFullPath(entry.Backup));
            }
            if (entry.Displaced is not null)
            {
                allowedFiles.Add(Path.GetFullPath(entry.Displaced));
            }
        }

        string backups = Path.GetFullPath(
            Path.Combine(StagingDirectory, "backups"));
        foreach (string file in Directory.EnumerateFiles(
                     StagingDirectory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            string full = Path.GetFullPath(file);
            if (!allowedFiles.Contains(full))
            {
                throw new IOException(
                    $"Publication cleanup preserved unknown file '{full}'.");
            }
        }
        foreach (string directory in Directory.EnumerateDirectories(
                     StagingDirectory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            string full = Path.GetFullPath(directory);
            if (!comparer.Equals(full, backups)
                || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException(
                    $"Publication cleanup preserved unknown directory '{full}'.");
            }
        }
        if (!Directory.Exists(backups))
        {
            return;
        }
        if (Directory.EnumerateDirectories(
                backups,
                "*",
                SearchOption.TopDirectoryOnly).Any())
        {
            throw new IOException(
                $"Publication cleanup preserved an unknown backup directory below '{backups}'.");
        }
        foreach (string file in Directory.EnumerateFiles(
                     backups,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            string full = Path.GetFullPath(file);
            if (!allowedFiles.Contains(full))
            {
                throw new IOException(
                    $"Publication cleanup preserved unknown backup file '{full}'.");
            }
        }
    }

    private void DeleteJournalTemporaries()
    {
        foreach (string path in Directory.EnumerateFiles(
                     StagingDirectory,
                     ".publication-journal.v1.json.*.tmp",
                     SearchOption.TopDirectoryOnly))
        {
            if (!PublicationJournal.IsTemporaryPath(path))
            {
                throw new IOException(
                    $"Publication cleanup preserved unknown journal temporary '{path}'.");
            }
            PrivateUserStorage.ValidateFile(path);
            DeleteKnownFile(path, FilePublicationSnapshot.Capture(path));
        }
    }

    private static void DeleteKnownFile(
        string path,
        FilePublicationSnapshot? expected)
    {
        if (!File.Exists(path))
        {
            return;
        }
        if (expected is null
            || !FilePublicationOwnedDelete.TryDelete(path, expected))
        {
            throw new IOException(
                $"Publication cleanup preserved changed or unknown file '{path}'.");
        }
    }

    private static void DeleteEmptyDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: false);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static string CreateLocalStagingDirectory(string root)
    {
        Directory.CreateDirectory(root);
        string stagingDirectory = Path.Combine(
            root,
            $".aspose-publication-{Guid.NewGuid():N}");
        return PrivateUserStorage.EnsureDirectory(stagingDirectory);
    }
}
