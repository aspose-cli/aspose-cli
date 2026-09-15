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
    public const string ManifestName = "output-manifest.v2.json";
    private readonly object _gate = new();
    private readonly List<WorkerOutputEntry> _entries = [];
    private readonly Dictionary<string, WorkerDirectoryEntry> _directories = new(WorkerManifestStore.PathComparer);
    private readonly List<WorkerPreviewHint> _hints = [];
    private readonly string _root;
    private readonly string _manifestPath;
    private int _nextId;

    public WorkerOutputSession(string root, string manifestPath)
    {
        (_root, _manifestPath) = WorkerManifestStore.ValidateSessionPaths(root, manifestPath, requireManifest: false);
    }

    internal string CreatePrivateDirectory(string operation)
    {
        string name = string.Concat(operation.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'));
        return PrivateUserStorage.EnsureDirectory(Path.Combine(_root, $"{Interlocked.Increment(ref _nextId):000000}-{name}"));
    }

    internal void Register(PublicationJournalEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Count >= WorkerManifestStore.MaximumEntries) { throw new IOException("The worker output entry budget was exceeded."); }
            if (_entries.Any(existing => WorkerManifestStore.PathComparer.Equals(existing.Target, entry.Target)))
            {
                throw new IOException($"Duplicate worker output '{entry.Target}'.");
            }
            string retained = Path.Combine(CreatePrivateDirectory("retained"), "output.stage");
            using (var source = new FileStream(entry.Staged, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream target = PrivateUserStorage.CreateFile(retained))
            {
                if (!entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(entry.Staged)))
                {
                    throw new IOException("The produced output changed before handoff.");
                }
                source.CopyTo(target);
                target.Flush(flushToDisk: true);
            }
            FilePublicationSnapshot snapshot = FilePublicationSnapshot.Capture(retained);
            if (!entry.StagedSnapshot.ContentEquals(snapshot)) { throw new IOException("Worker handoff changed the output content."); }
            _entries.Add(new WorkerOutputEntry
            {
                Target = entry.Target, Staged = retained, StagedSnapshot = snapshot,
                Original = entry.Original, Overwrite = entry.Overwrite, DeleteTarget = entry.DeleteTarget,
                BackupPath = entry.RequestedBackup, BackupOriginal = entry.RequestedBackupOriginal,
                InputPath = entry.InputPath, InputSnapshot = entry.InputSnapshot,
                TargetParentIdentity = entry.TargetParentIdentity, BackupParentIdentity = entry.RequestedBackupParentIdentity,
            });
            RegisterParents(entry.Target);
            if (entry.RequestedBackup is not null) { RegisterParents(entry.RequestedBackup); }
            WriteManifest();
        }
    }

    internal void RegisterDirectory(string target)
    {
        lock (_gate)
        {
            string path = Path.GetFullPath(target);
            OutputPathValidator.EnsureSafeDirectory(path);
            if (!_directories.ContainsKey(path))
            {
                if (_directories.Count >= WorkerManifestStore.MaximumDirectories) { throw new IOException("The worker directory budget was exceeded."); }
                bool exists = Directory.Exists(path);
                _directories.Add(path, new WorkerDirectoryEntry(path, exists,
                    exists ? FilePublicationOwnedDelete.TryGetDirectoryIdentity(path) : null));
            }
            RegisterParents(path);
            WriteManifest();
        }
    }

    private void RegisterParents(string target)
    {
        for (string? parent = Path.GetDirectoryName(target); parent is not null && !Directory.Exists(parent); parent = Path.GetDirectoryName(parent))
        {
            if (_directories.Count >= WorkerManifestStore.MaximumDirectories) { throw new IOException("The worker directory budget was exceeded."); }
            _directories.TryAdd(parent, new WorkerDirectoryEntry(parent, false, null));
        }
    }

    public bool QueuePreviewHint(string filePath, IReadOnlyList<ProductPreviewPayload> targets)
    {
        lock (_gate)
        {
            if (targets.Count == 0 || _hints.Count >= 256) { return false; }
            _hints.Add(new WorkerPreviewHint(Path.GetFullPath(filePath), targets.ToArray()));
            try { WriteManifest(); return true; }
            catch { _hints.RemoveAt(_hints.Count - 1); return false; }
        }
    }

    private void WriteManifest() => WorkerManifestStore.Write(_manifestPath,
        new WorkerOutputManifest { Entries = _entries.ToArray(), Directories = _directories.Values.ToArray(), Hints = _hints.ToArray() });

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
    public int Version { get; init; } = 2;
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
