namespace Aspose.Cli.Sdk.IO;

/// <summary>Explicit invocation-owned collection of files awaiting parent publication.</summary>
public sealed class WorkerOutputSession
{
    public const string RootEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_OUTPUT_ROOT";
    public const string ManifestEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_OUTPUT_MANIFEST";
    public const string DeadlineEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_DEADLINE_TICK";
    public const string BudgetEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "WORKER_BUDGET_MS";
    public const string WorkerEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "TIMEOUT_WORKER";
    public const string ManifestName = "output-manifest.v5.json";
    private readonly object _gate = new();
    private readonly List<WorkerOutputEntry> _entries = [];
    private readonly Dictionary<string, WorkerDirectoryEntry> _directories = new(WorkerManifestStore.PathComparer);
    private readonly string _root;
    private readonly string _manifestPath;
    private int _nextId;
    private bool _sealed;
    private NewDirectoryOutput? _directoryOutput;

    public WorkerOutputSession(string root, string manifestPath)
    {
        (_root, _manifestPath) = WorkerManifestStore.ValidateSessionPaths(root, manifestPath);
    }

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
            var paths = new HashSet<string>(_entries.SelectMany(entry => entry.BackupPath is null
                ? new[] { entry.Target } : new[] { entry.Target, entry.BackupPath }), WorkerManifestStore.PathComparer);
            var directories = new Dictionary<string, WorkerDirectoryEntry>(_directories, WorkerManifestStore.PathComparer);
            foreach (string directory in outputDirectories) { AddDirectory(directories, directory); AddParents(directories, directory); }
            foreach (PublicationJournalEntry entry in entries)
            {
                if (!paths.Add(entry.Target) || entry.RequestedBackup is { } backup && !paths.Add(backup))
                { throw new IOException($"Duplicate worker output '{entry.Target}'."); }
                AddParents(directories, entry.Target);
                if (entry.RequestedBackup is not null) { AddParents(directories, entry.RequestedBackup); }
            }
            var additions = new List<WorkerOutputEntry>(entries.Count);
            var retained = new List<OwnedTemporaryFile>(entries.Count);
            bool accepted = false;
            try
            {
                foreach (PublicationJournalEntry entry in entries)
                {
                    deadline.ThrowIfExpired("worker-handoff");
                    string path = Path.Combine(CreateDirectory("retained"), "output.stage");
                    OwnedTemporaryFile temporary = OwnedTemporaryFile.Create(path);
                    retained.Add(temporary);
                    using (var source = new FileStream(entry.Staged, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var destination = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None))
                    {
                        if (!entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged)))
                        { throw new IOException("The produced output changed before handoff."); }
                        byte[] buffer = new byte[81920];
                        int count;
                        while ((count = source.Read(buffer)) != 0)
                        {
                            deadline.ThrowIfExpired("worker-handoff-copy");
                            destination.Write(buffer, 0, count);
                        }
                        destination.Flush(flushToDisk: true);
                    }
                    temporary.BindProducedFile();
                    FilePublicationSnapshot snapshot = temporary.CaptureBoundSnapshot();
                    if (!entry.StagedSnapshot.ContentEquals(snapshot))
                    { throw new IOException("Worker handoff changed the output content."); }
                    additions.Add(new WorkerOutputEntry
                    {
                        Target = entry.Target, Staged = path, StagedSnapshot = snapshot,
                        Original = entry.Original, Overwrite = entry.Overwrite, DeleteTarget = entry.DeleteTarget,
                        BackupPath = entry.RequestedBackup, BackupOriginal = entry.RequestedBackupOriginal,
                        InputPath = entry.InputPath, InputSnapshot = entry.InputSnapshot,
                        TargetParentIdentity = entry.TargetParentIdentity, BackupParentIdentity = entry.RequestedBackupParentIdentity,
                    });
                }
                WorkerOutputEntry[] next = [.. _entries, .. additions];
                WorkerManifestStore.CheckCapacity(new WorkerOutputManifest
                { Entries = next, Directories = directories.Values.ToArray(), Sealed = true });
                deadline.ThrowIfExpired("worker-handoff-accept");
                _entries.AddRange(additions);
                _directories.Clear();
                foreach ((string path, WorkerDirectoryEntry value) in directories) { _directories.Add(path, value); }
                foreach (OwnedTemporaryFile temporary in retained) { temporary.MarkPublished(); }
                accepted = true;
            }
            finally
            {
                foreach (OwnedTemporaryFile temporary in retained)
                {
                    temporary.Dispose();
                    if (!accepted)
                    {
                        try { Directory.Delete(Path.GetDirectoryName(temporary.Path)!, recursive: false); }
                        catch (IOException) { }
                    }
                }
            }
        }
    }

    internal void RegisterDirectory(NewDirectoryOutput output, OperationDeadline deadline)
    {
        lock (_gate)
        {
            EnsureMutable();
            if (_directoryOutput is not null || _entries.Count != 0 || _directories.Count != 0)
            { throw new IOException("A new-directory output must be the invocation's sole output set."); }
            NewDirectoryPublication.ValidateDescriptor(output);
            if (!WorkerManifestStore.PathComparer.Equals(Path.GetDirectoryName(Path.GetDirectoryName(output.Staged)!), _root)
                || Path.GetFileName(output.Staged) != "directory")
            { throw new IOException("The directory candidate is outside its worker's storage."); }
            deadline.ThrowIfExpired("directory-handoff");
            WorkerManifestStore.CheckCapacity(new WorkerOutputManifest { DirectoryOutput = output, Sealed = true });
            _directoryOutput = output;
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
    { Entries = _entries.ToArray(), Directories = _directories.Values.ToArray(), DirectoryOutput = _directoryOutput };

    private void EnsureMutable()
    {
        if (_sealed) { throw new InvalidOperationException("The worker output set is sealed."); }
    }
}

internal sealed record WorkerOutputManifest
{
    public int Version { get; init; } = 5;
    public bool Sealed { get; init; }
    public IReadOnlyList<WorkerOutputEntry> Entries { get; init; } = [];
    public IReadOnlyList<WorkerDirectoryEntry> Directories { get; init; } = [];
    public NewDirectoryOutput? DirectoryOutput { get; init; }
}

internal sealed record WorkerOutputEntry
{
    public required string Target { get; init; }
    public required string Staged { get; init; }
    public required FilePublicationSnapshot Original { get; init; }
    public required FilePublicationSnapshot StagedSnapshot { get; init; }
    public bool Overwrite { get; init; }
    public bool DeleteTarget { get; init; }
    public string? BackupPath { get; init; }
    public FilePublicationSnapshot? BackupOriginal { get; init; }
    public string? InputPath { get; init; }
    public FilePublicationSnapshot? InputSnapshot { get; init; }
    public FilePhysicalIdentity? TargetParentIdentity { get; init; }
    public FilePhysicalIdentity? BackupParentIdentity { get; init; }
}

internal sealed record WorkerDirectoryEntry(string Target, bool Existed, FilePhysicalIdentity? OriginalIdentity);
