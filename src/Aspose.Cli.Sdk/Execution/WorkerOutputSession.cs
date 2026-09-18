using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>Explicit invocation-owned collection of files awaiting parent publication.</summary>
public sealed class WorkerOutputSession
{
    public const string RootEnvironmentVariable = "ASPOSE_CLI_WORKER_OUTPUT_ROOT";
    public const string ManifestEnvironmentVariable = "ASPOSE_CLI_WORKER_OUTPUT_MANIFEST";
    public const string DeadlineEnvironmentVariable = "ASPOSE_CLI_WORKER_DEADLINE_TICK";
    public const string BudgetEnvironmentVariable = "ASPOSE_CLI_WORKER_BUDGET_MS";
    public const string WorkerEnvironmentVariable = "ASPOSE_CLI_TIMEOUT_WORKER";
    public const string ManifestName = "output-manifest.v3.json";
    private readonly object _gate = new();
    private readonly List<WorkerOutputEntry> _entries = [];
    private readonly Dictionary<string, WorkerDirectoryEntry> _directories = new(WorkerManifestStore.PathComparer);
    private readonly List<WorkerPreviewHint> _hints = [];
    private readonly string _root;
    private readonly string _manifestPath;
    private int _nextId;
    private bool _sealed;

    public WorkerOutputSession(string root, string manifestPath)
    {
        (_root, _manifestPath) = WorkerManifestStore.ValidateSessionPaths(root, manifestPath, requireManifest: false);
    }

    internal string CreatePrivateDirectory(string operation)
    {
        string name = string.Concat(operation.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));
        return PrivateUserStorage.EnsureDirectory(Path.Combine(_root, $"{Interlocked.Increment(ref _nextId):000000}-{name}"));
    }

    internal void RegisterBatch(IReadOnlyList<PublicationJournalEntry> entries,
        IEnumerable<string> outputDirectories, OperationDeadline deadline)
    {
        lock (_gate)
        {
            EnsureMutable();
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
                    string path = Path.Combine(CreatePrivateDirectory("retained"), "output.stage");
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
                { Entries = next, Directories = directories.Values.ToArray(), Hints = _hints.ToArray(), Sealed = true });
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

    public bool QueuePreviewHint(string filePath, IReadOnlyList<ProductPreviewPayload> targets)
    {
        lock (_gate)
        {
            if (_sealed || targets.Count == 0 || _hints.Count >= 256) { return false; }
            _hints.Add(new WorkerPreviewHint(Path.GetFullPath(filePath), targets.ToArray()));
            try { WorkerManifestStore.CheckCapacity(Snapshot()); return true; }
            catch { _hints.RemoveAt(_hints.Count - 1); return false; }
        }
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
    { Entries = _entries.ToArray(), Directories = _directories.Values.ToArray(), Hints = _hints.ToArray() };

    private void EnsureMutable()
    {
        if (_sealed) { throw new InvalidOperationException("The worker output set is sealed."); }
    }

    public static IReadOnlyList<long> Publish(string manifestPath, ResourceBudgetLedger budgets)
    {
        try { return WorkerOutputPublisher.Publish(WorkerManifestStore.ReadAndValidate(manifestPath), budgets); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw Aspose.Cli.Sdk.Errors.CliErrors.OutputUnwritable(manifestPath,
                "the worker output could not be published", error, "worker-publication");
        }
    }
}

internal sealed record WorkerOutputManifest
{
    public int Version { get; init; } = 3;
    public bool Sealed { get; init; }
    public IReadOnlyList<WorkerOutputEntry> Entries { get; init; } = [];
    public IReadOnlyList<WorkerDirectoryEntry> Directories { get; init; } = [];
    public IReadOnlyList<WorkerPreviewHint> Hints { get; init; } = [];
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
internal sealed record WorkerPreviewHint(string FilePath, IReadOnlyList<ProductPreviewPayload> Targets);
