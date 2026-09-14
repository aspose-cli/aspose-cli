using System.Diagnostics;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Owns one immutable publication location and its durable journal.</summary>
internal sealed class AtomicPublicationPlan
{
    internal const string JournalName = "publication-journal.v1.json";

    private readonly IPublicationFaultInjector _faults;
    private PublicationDirectoryLease? _lease;
    internal HashSet<string> CreatedStagingDirectories { get; } = [];

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

    internal ResourceBudgetLedger? ResourceBudgets { get; set; }

    public string StagedPath(string target, int index) => StagedPath(StagingDirectory, target, index);

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
        return Path.Combine(transactionDirectory, $"output-{index + 1:000000}", Path.GetFileName(target));
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

            foreach (string directory in Journal.Entries.Select(entry => Path.GetDirectoryName(entry.Staged)!).Concat(CreatedStagingDirectories).Distinct())
            {
                DeleteEmptyDirectory(directory);
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

    internal void EnsureNoUnknownArtifacts()
    {
        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var allowedFiles = new HashSet<string>(comparer) { Path.GetFullPath(JournalPath) };
        var allowedDirectories = new HashSet<string>(comparer) { Path.Combine(StagingDirectory, "backups") };
        foreach (PublicationJournalEntry entry in Journal.Entries)
        {
            allowedDirectories.Add(Path.GetDirectoryName(entry.Staged)!);
            foreach (string? file in new[] { entry.Staged, entry.Backup, entry.Displaced })
            {
                if (file is not null) { allowedFiles.Add(Path.GetFullPath(file)); }
            }
        }
        allowedDirectories.UnionWith(CreatedStagingDirectories);
        var pending = new Stack<string>();
        pending.Push(StagingDirectory);
        while (pending.TryPop(out string? directory))
        {
            foreach (FileSystemInfo item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Publication preserved linked artifact '{item.FullName}'.");
                }
                if (item is DirectoryInfo)
                {
                    if (!allowedDirectories.Contains(item.FullName))
                    {
                        throw new IOException($"Publication preserved unknown directory '{item.FullName}'.");
                    }
                    pending.Push(item.FullName);
                }
                else if (!allowedFiles.Contains(item.FullName))
                {
                    throw new IOException($"Publication preserved unknown file '{item.FullName}'.");
                }
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
