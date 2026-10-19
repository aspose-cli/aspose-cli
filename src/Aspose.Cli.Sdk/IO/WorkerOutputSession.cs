namespace Aspose.Cli.Sdk.IO;

/// <summary>Explicit invocation-owned collection of files awaiting parent publication.</summary>
public sealed class WorkerOutputSession
{
    public const string RootEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_OUTPUT_ROOT";
    public const string ManifestEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_OUTPUT_MANIFEST";
    public const string DeadlineEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_DEADLINE_TICK";
    public const string BudgetEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_BUDGET_MS";
    public const string WorkerEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "TIMEOUT_WORKER";
    public const string ManifestName = "output-manifest.json";
    private readonly object _gate = new();
    private readonly List<PlannedOutput> _entries = [];
    private readonly Dictionary<string, WorkerDirectoryEntry> _directories = new(FilePaths.Comparer);
    private readonly string _root;
    private readonly string _manifestPath;
    private int _nextId;
    private bool _sealed;
    private NewDirectoryOutput? _directoryOutput;
    private string? _directoryTransactionParent;
    private AdmittedDirectoryPrevious? _directoryAdmittedPrevious;

    public WorkerOutputSession(string root, string manifestPath)
    {
        (_root, _manifestPath) = WorkerManifestStore.ValidateSessionPaths(root, manifestPath);
    }

    /// <summary>
    /// Creates a worker's private root, named for the process that owns it, so a later
    /// invocation can tell when that owner has exited (<see cref="RemoveAbandonedRoots"/>).
    /// </summary>
    public static string CreateRoot() => UserStorage.CreateTemporaryDirectory(
        WorkerManifestStore.RootCategory, $"{Environment.ProcessId}-{ProcessIdentity.CurrentStartUtcTicks}");

    /// <summary>
    /// Removes the worker roots whose owners exited without reclaiming them, such as a CLI
    /// killed with its worker, since they can hold decrypted copies. It lists only the worker
    /// category, leaves every root whose owner is alive or whose name is not one this CLI gives,
    /// follows no link, and keeps what it cannot delete for a later invocation.
    /// </summary>
    public static int RemoveAbandonedRoots()
    {
        int removed = 0;
        try
        {
            string category = Path.Combine(UserStorage.TemporaryRoot(), WorkerManifestStore.RootCategory);
            if (!Directory.Exists(category)) { return 0; }
            foreach (string root in Directory.EnumerateDirectories(category))
            {
                if (removed >= MaximumAbandonedRootsPerSweep) { break; }
                if (!WorkerManifestStore.TryParseRootOwner(Path.GetFileName(root), out int pid, out long ticks)
                    || AtomicPublicationRecovery.IsCreatorAlive(pid, ticks))
                { continue; }
                try
                {
                    if (UserStorage.TryDeleteTree(root)) { removed++; }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return removed;
    }

    // Bounds the work one invocation spends on another's debris; the rest waits for the next.
    private const int MaximumAbandonedRootsPerSweep = 16;

    /// <summary>Creates scratch storage reclaimed with this worker, including after forced termination.</summary>
    public string CreateDirectory(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (operation.Length > 128) { throw new ArgumentOutOfRangeException(nameof(operation)); }
        string name = string.Concat(operation.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));
        return Directory.CreateDirectory(Path.Combine(_root, $"{Interlocked.Increment(ref _nextId):000000}-{name}")).FullName;
    }

    internal void RegisterBatch(IReadOnlyList<PublicationJournalEntry> entries,
        IEnumerable<string> outputDirectories, OperationDeadline deadline)
    {
        lock (_gate)
        {
            EnsureMutable();
            if (_directoryOutput is not null) { throw new IOException("A new-directory output cannot be combined with file outputs."); }
            if (entries.Count > PublicationLimits.MaximumEntries - _entries.Count)
            { throw new IOException("The worker output entry budget was exceeded."); }
            var paths = new HashSet<string>(_entries.SelectMany(entry => entry.RequestedBackup is null
                ? new[] { entry.Target } : new[] { entry.Target, entry.RequestedBackup }), FilePaths.Comparer);
            var directories = new Dictionary<string, WorkerDirectoryEntry>(_directories, FilePaths.Comparer);
            foreach (string directory in outputDirectories) { AddDirectory(directories, directory); AddParents(directories, directory); }
            foreach (PublicationJournalEntry entry in entries)
            {
                if (!paths.Add(entry.Target) || entry.RequestedBackup is { } backup && !paths.Add(backup))
                { throw new IOException($"Duplicate worker output '{entry.Target}'."); }
                AddParents(directories, entry.Target);
                if (entry.RequestedBackup is not null) { AddParents(directories, entry.RequestedBackup); }
            }
            var additions = new List<PlannedOutput>(entries.Count);
            var moved = new List<(string Path, FilePublicationSnapshot Snapshot)>(entries.Count);
            var created = new List<string>(entries.Count);
            bool accepted = false;
            try
            {
                foreach (PublicationJournalEntry entry in entries)
                {
                    deadline.ThrowIfExpired("worker-handoff");
                    string directory = CreateDirectory("retained");
                    created.Add(directory);
                    string path = Path.Combine(directory, "output.stage");
                    // The staged file and the handoff share this root, so the file moves instead of
                    // being copied; keeping its physical identity proves it is the file staging verified.
                    AtomicFileRename.Move(entry.Staged, path, overwrite: false, deadline);
                    moved.Add((path, entry.StagedSnapshot));
                    if (!entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(path)))
                    { throw new IOException("The produced output changed before handoff."); }
                    additions.Add(entry.StagedAt(path));
                }
                PlannedOutput[] next = [.. _entries, .. additions];
                WorkerManifestStore.CheckCapacity(new WorkerOutputManifest
                { Entries = next, Directories = directories.Values.ToArray(), Sealed = true });
                deadline.ThrowIfExpired("worker-handoff-accept");
                _entries.AddRange(additions);
                _directories.Clear();
                foreach ((string path, WorkerDirectoryEntry value) in directories) { _directories.Add(path, value); }
                accepted = true;
            }
            finally
            {
                if (!accepted) { DiscardHandoff(moved, created); }
            }
        }
    }

    // A refused batch leaves no output in the handoff; a file that is no longer the one staged is
    // left for the removal of this root.
    private static void DiscardHandoff(List<(string Path, FilePublicationSnapshot Snapshot)> moved, List<string> created)
    {
        foreach ((string path, FilePublicationSnapshot snapshot) in moved)
        {
            try { _ = FilePublicationOwnedDelete.TryDelete(path, snapshot); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        foreach (string directory in created)
        {
            try { Directory.Delete(directory, recursive: false); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    internal void RegisterDirectory(NewDirectoryOutput output, string? transactionParent, AdmittedDirectoryPrevious? admittedPrevious,
        OperationDeadline deadline)
    {
        lock (_gate)
        {
            EnsureMutable();
            if (_directoryOutput is not null || _entries.Count != 0 || _directories.Count != 0)
            { throw new IOException("A new-directory output must be the invocation's sole output set."); }
            NewDirectoryPublication.ValidateDescriptor(output);
            if (!FilePaths.Same(Path.GetDirectoryName(Path.GetDirectoryName(output.Staged)!), _root)
                || Path.GetFileName(output.Staged) != "directory")
            { throw new IOException("The directory candidate is outside its worker's storage."); }
            deadline.ThrowIfExpired("directory-handoff");
            WorkerManifestStore.CheckCapacity(new WorkerOutputManifest
            {
                DirectoryOutput = output, DirectoryTransactionParent = transactionParent,
                DirectoryAdmittedPrevious = admittedPrevious, Sealed = true,
            });
            _directoryOutput = output;
            _directoryTransactionParent = transactionParent;
            _directoryAdmittedPrevious = admittedPrevious;
        }
    }
    private static void AddDirectory(Dictionary<string, WorkerDirectoryEntry> directories, string target)
    {
        string path = Path.GetFullPath(target);
        OutputPathValidator.EnsureSafeDirectory(path);
        if (directories.ContainsKey(path)) { return; }
        if (directories.Count >= PublicationLimits.MaximumDirectories)
        { throw new IOException("The worker directory budget was exceeded."); }
        bool exists = Directory.Exists(path);
        directories.Add(path, new WorkerDirectoryEntry(path, exists,
            exists ? FilePublicationOwnedDelete.TryGetDirectoryIdentity(path) : null));
    }

    private static void AddParents(Dictionary<string, WorkerDirectoryEntry> directories, string target)
    {
        for (string? parent = Path.GetDirectoryName(target); parent is not null && !Directory.Exists(parent); parent = Path.GetDirectoryName(parent))
        { AddDirectory(directories, parent); }
    }

    /// <summary>Publishes the handoff only after the host has produced a normal command result.</summary>
    public void SealForPublication()
    {
        lock (_gate)
        {
            if (_sealed) { return; }
            WorkerManifestStore.Write(_manifestPath, Snapshot() with { Sealed = true });
            _sealed = true;
        }
    }

    private WorkerOutputManifest Snapshot() => new()
    {
        Entries = _entries.ToArray(), Directories = _directories.Values.ToArray(), DirectoryOutput = _directoryOutput,
        DirectoryTransactionParent = _directoryTransactionParent, DirectoryAdmittedPrevious = _directoryAdmittedPrevious,
    };

    private void EnsureMutable()
    {
        if (_sealed) { throw new InvalidOperationException("The worker output set is sealed."); }
    }
}

/// <summary>
/// What a worker hands to its parent. The worker and its parent are one executable, so a
/// manifest of another version is refused rather than read.
/// </summary>
internal sealed record WorkerOutputManifest
{
    internal const int CurrentVersion = 8;

    public int Version { get; init; } = CurrentVersion;
    public bool Sealed { get; init; }
    public IReadOnlyList<PlannedOutput> Entries { get; init; } = [];
    public IReadOnlyList<WorkerDirectoryEntry> Directories { get; init; } = [];
    public NewDirectoryOutput? DirectoryOutput { get; init; }

    /// <summary>Where the parent places the transaction of <see cref="DirectoryOutput"/> when not in the target's parent.</summary>
    public string? DirectoryTransactionParent { get; init; }

    /// <summary>
    /// What the worker admitted at the target of <see cref="DirectoryOutput"/>; the parent
    /// publishes only while the target still holds it. Null when the worker admitted nothing.
    /// </summary>
    public AdmittedDirectoryPrevious? DirectoryAdmittedPrevious { get; init; }
}

internal sealed record WorkerDirectoryEntry(string Target, bool Existed, FilePhysicalIdentity? OriginalIdentity);
