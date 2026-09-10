using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>
/// Staging-only publication protocol used by a supervised CLI worker. The
/// worker never receives publication authority over final user targets.
/// </summary>
public static class WorkerOutputSession
{
    /// <summary>Private directory used for staged worker output.</summary>
    public const string RootEnvironmentVariable = "ASPOSE_CLI_WORKER_OUTPUT_ROOT";

    /// <summary>Manifest written by the worker for parent publication.</summary>
    public const string ManifestEnvironmentVariable = "ASPOSE_CLI_WORKER_OUTPUT_MANIFEST";

    /// <summary>Absolute monotonic deadline propagated to the worker.</summary>
    public const string DeadlineEnvironmentVariable = "ASPOSE_CLI_WORKER_DEADLINE_TICK";

    /// <summary>Original timeout budget propagated to the worker.</summary>
    public const string BudgetEnvironmentVariable = "ASPOSE_CLI_WORKER_BUDGET_MS";

    /// <summary>Marker that identifies a supervised worker process.</summary>
    public const string WorkerEnvironmentVariable = "ASPOSE_CLI_TIMEOUT_WORKER";

    private static readonly object Sync = new();
    private static readonly List<WorkerOutputEntry> Entries = [];
    private static readonly List<WorkerDirectoryEntry> Directories = [];
    private static int _nextId;

    /// <summary>Whether this process has a complete supervised output session.</summary>
    public static bool IsActive =>
        string.Equals(
            Environment.GetEnvironmentVariable(WorkerEnvironmentVariable),
            "1",
            StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(Root)
        && !string.IsNullOrWhiteSpace(ManifestPath);

    /// <summary>
    /// Resolves a final output to its private staged file while a worker is
    /// building the command result. Outside a worker the canonical path is
    /// returned unchanged.
    /// </summary>
    public static string ResolveReadPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (!IsActive)
        {
            return full;
        }

        lock (Sync)
        {
            WorkerOutputEntry? entry = Entries.LastOrDefault(
                candidate => PathComparer.Equals(candidate.Target, full));
            return entry is { DeleteTarget: false }
                ? entry.Staged
                : full;
        }
    }

    private static string? Root =>
        Environment.GetEnvironmentVariable(RootEnvironmentVariable);

    private static string? ManifestPath =>
        Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);

    internal static string CreatePrivateDirectory(string operation)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("No supervised worker output session is active.");
        }

        string safeOperation = string.Concat(
            operation.Select(static character =>
                char.IsAsciiLetterOrDigit(character) ? character : '-'));
        string directory = Path.Combine(
            ValidateWorkerPaths(requireManifest: false).Root,
            $"{Interlocked.Increment(ref _nextId):000000}-{safeOperation}");
        PrivateUserStorage.EnsureDirectory(directory);
        return directory;
    }

    internal static SafeWriteResult StageSingle(
        SafeFileWriter writer,
        string targetPath,
        bool overwrite,
        string? backupPath,
        FileWritePrecondition? inputPrecondition,
        Action<string> write,
        Action<string, Stream>? inspectProducedFile = null,
        Action<string>? verifyProducedFile = null)
    {
        string target = Path.GetFullPath(targetPath);
        FilePublicationSnapshot original = inputPrecondition is not null
            && inputPrecondition.Targets(target)
                ? inputPrecondition.Snapshot
                : FilePublicationSnapshot.Capture(target);
        if (!overwrite && original.Exists)
        {
            throw CliErrors.OutputExists(target);
        }

        string directory = CreatePrivateDirectory("single");
        string staged = Path.Combine(directory, "output.stage");
        using var temporary = OwnedTemporaryFile.Create(staged);
        write(staged);
        temporary.BindInspectAndVerify(
            inspectProducedFile,
            verifyProducedFile);

        temporary.FlushBound();
        FilePublicationSnapshot verifiedStage =
            temporary.CaptureBoundSnapshot();
        original.Metadata?.ApplyContentAttributes(staged);
        inputPrecondition?.EnsureUnchanged();
        RegisterFile(
            target,
            staged,
            overwrite,
            backupPath,
            original,
            verifiedStage);
        temporary.MarkPublished();
        long size = new FileInfo(staged).Length;
        writer.ConsumeOutput(size, "worker-output-stage");
        return new SafeWriteResult(
            size,
            PlannedBackup(backupPath, original));
    }

    internal static void RegisterFile(
        string targetPath,
        string sourcePath,
        bool overwrite,
        string? backupPath,
        FilePublicationSnapshot original,
        FilePublicationSnapshot? expectedSource = null)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("No supervised worker output session is active.");
        }

        string target = Path.GetFullPath(targetPath);
        string source = Path.GetFullPath(sourcePath);
        string root = ValidateWorkerPaths(requireManifest: false).Root;
        if (!IsChild(root, source))
        {
            throw new InvalidOperationException(
                "Worker output source is outside the private staging root.");
        }
        PrivateUserStorage.ProtectFile(source);
        ExtractionPathValidator.EnsureNoLinks(target);
        string? backup = backupPath is null
            ? null
            : Path.GetFullPath(backupPath);
        if (backup is not null)
        {
            ExtractionPathValidator.EnsureNoLinks(backup);
            if (PathComparer.Equals(backup, target)
                || Directory.Exists(backup))
            {
                throw new IOException(
                    $"Worker output backup path conflicts with its target: '{backup}'.");
            }
        }

        lock (Sync)
        {
            StringComparer comparer = PathComparer;
            if (Entries.Count >= WorkerManifestStore.MaximumEntries)
            {
                throw ManifestBudgetExceeded(
                    "the worker output entry budget was exceeded");
            }
            if (Entries.Any(entry => comparer.Equals(entry.Target, target)))
            {
                throw new InvalidOperationException(
                    $"Worker registered duplicate output target '{target}'.");
            }
            if (backup is not null
                && Entries.Any(entry =>
                    comparer.Equals(entry.Target, backup)
                    || entry.BackupPath is not null
                        && comparer.Equals(entry.BackupPath, backup)))
            {
                throw new InvalidOperationException(
                    $"Worker registered conflicting backup path '{backup}'.");
            }

            string retainedDirectory = CreatePrivateDirectory("retained");
            string retained = Path.Combine(retainedDirectory, "output.stage");
            File.Copy(source, retained, overwrite: false);
            FilePublicationDurabilityAdapter.FlushFile(retained);
            PrivateUserStorage.ProtectFile(retained);
            FilePublicationSnapshot staged = FilePublicationSnapshot.Capture(retained);
            if (expectedSource is not null
                && (!expectedSource.ContentEquals(staged)
                    || !expectedSource.VersionEquals(
                        FilePublicationSnapshot.Capture(source))))
            {
                throw new IOException(
                    $"Worker staged file '{source}' changed after verification.");
            }
            string? originalBackup = null;
            FilePublicationSnapshot? originalBackupSnapshot = null;
            if (original.Exists)
            {
                EnsureVersion(target, original);
                originalBackup = Path.Combine(retainedDirectory, "original.backup");
                File.Copy(target, originalBackup, overwrite: false);
                FilePublicationDurabilityAdapter.FlushFile(originalBackup);
                PrivateUserStorage.ProtectFile(originalBackup);
                originalBackupSnapshot =
                    FilePublicationSnapshot.Capture(originalBackup);
                if (!original.ContentEquals(originalBackupSnapshot))
                {
                    throw CliErrors.OutputConflict(
                        target,
                        original,
                        FilePublicationSnapshot.Capture(target));
                }
                EnsureVersion(target, original);
            }
            else
            {
                EnsureVersion(target, original);
            }

            FilePublicationSnapshot? backupOriginal = backup is null
                || !original.Exists
                    ? null
                    : FilePublicationSnapshot.Capture(backup);

            Entries.Add(new WorkerOutputEntry
            {
                Target = target,
                Staged = retained,
                Overwrite = overwrite,
                BackupPath = backup,
                BackupOriginal = backupOriginal,
                OriginalBackup = originalBackup,
                OriginalBackupSnapshot = originalBackupSnapshot,
                Original = original,
                StagedSnapshot = staged,
            });
            RegisterMissingParentDirectories(target);
            if (backup is not null && original.Exists)
            {
                RegisterMissingParentDirectories(backup);
            }
            WriteManifest();
        }
    }

    internal static void RegisterDirectory(string targetPath)
    {
        string target = Path.GetFullPath(targetPath);
        ExtractionPathValidator.EnsureNoLinks(target);
        if (File.Exists(target))
        {
            throw new IOException(
                $"Worker output directory path is occupied by a file: '{target}'.");
        }
        lock (Sync)
        {
            StringComparer comparer = PathComparer;
            if (!Directories.Any(entry => comparer.Equals(entry.Target, target)))
            {
                if (Directories.Count >= WorkerManifestStore.MaximumDirectories)
                {
                    throw ManifestBudgetExceeded(
                        "the worker output directory budget was exceeded");
                }
                Directories.Add(new WorkerDirectoryEntry
                {
                    Target = target,
                    Existed = Directory.Exists(target),
                });
                RegisterMissingParentDirectories(target);
                WriteManifest();
            }
        }
    }

    /// <summary>
    /// Deletes a target through the active recoverable worker protocol, or
    /// directly when the process is not supervised.
    /// </summary>
    public static bool Delete(string targetPath)
    {
        if (!IsActive)
        {
            return File.Exists(targetPath) && DeleteDirect(targetPath);
        }

        string target = Path.GetFullPath(targetPath);
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        if (!original.Exists)
        {
            return false;
        }

        string retainedDirectory = CreatePrivateDirectory("deleted");
        string retained = Path.Combine(retainedDirectory, "original.stage");
        File.Copy(target, retained, overwrite: false);
        FilePublicationDurabilityAdapter.FlushFile(retained);
        original.Metadata?.Apply(retained);
        RegisterFile(
            target,
            retained,
            overwrite: true,
            backupPath: null,
            original);
        MarkDeleted(target);
        return true;
    }

    /// <summary>Marks a registered worker target as intentionally deleted.</summary>
    public static void MarkDeleted(string targetPath)
    {
        string target = Path.GetFullPath(targetPath);
        lock (Sync)
        {
            StringComparer comparer = PathComparer;
            WorkerOutputEntry entry = Entries.FirstOrDefault(
                candidate => comparer.Equals(candidate.Target, target))
                ?? throw new InvalidOperationException(
                    $"Worker output target '{target}' is not registered.");
            entry.DeleteTarget = true;
            WriteManifest();
        }
    }

    /// <summary>Publishes and verifies every staged output in a worker manifest.</summary>
    public static IReadOnlyList<long> Publish(string manifestPath)
    {
        WorkerOutputManifest manifest =
            WorkerManifestStore.ReadAndValidate(manifestPath);
        return WorkerOutputPublisher.Publish(manifest, manifestPath);
    }

    /// <summary>
    /// Restores every target recorded by a worker manifest. Incomplete
    /// recovery is reported as the stable output-publication CLI error.
    /// </summary>
    public static void RestoreOrThrow(
        string manifestPath,
        Exception originalFailure)
    {
        ArgumentNullException.ThrowIfNull(originalFailure);
        WorkerOutputManifest manifest =
            WorkerManifestStore.ReadAndValidate(manifestPath);
        PublicationRecoveryReport recovery =
            WorkerOutputRecovery.Restore(manifest);
        if (!recovery.RecoveryComplete)
        {
            throw CliErrors.OutputPublicationFailure(
                originalFailure,
                recovery);
        }
    }

    private static void WriteManifest()
    {
        var manifest = new WorkerOutputManifest
        {
            Entries = [.. Entries],
            Directories = [.. Directories],
        };
        WorkerManifestStore.Write(ManifestPath!, manifest);
    }

    internal static void WriteManifest(
        string path,
        WorkerOutputManifest manifest)
        => WorkerManifestStore.Write(path, manifest);

    private static (string Root, string Manifest) ValidateWorkerPaths(
        bool requireManifest)
        => WorkerManifestStore.ValidateSessionPaths(
            Root,
            ManifestPath,
            requireManifest);

    private static bool IsChild(string root, string path) =>
        path.StartsWith(
            Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar,
            PathComparison);

    private static void EnsureVersion(
        string target,
        FilePublicationSnapshot expected)
    {
        FilePublicationSnapshot current = FilePublicationSnapshot.Capture(target);
        if (!expected.VersionEquals(current) || Directory.Exists(target))
        {
            throw CliErrors.OutputConflict(target, expected, current);
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static void RegisterMissingParentDirectories(string target)
    {
        StringComparer comparer = PathComparer;
        string? current = Path.GetDirectoryName(target);
        while (current is not null && !Directory.Exists(current))
        {
            if (!Directories.Any(entry => comparer.Equals(entry.Target, current)))
            {
                if (Directories.Count >= WorkerManifestStore.MaximumDirectories)
                {
                    throw ManifestBudgetExceeded(
                        "the worker output directory budget was exceeded");
                }
                Directories.Add(new WorkerDirectoryEntry
                {
                    Target = current,
                    Existed = false,
                });
            }

            current = Path.GetDirectoryName(current);
        }
    }

    private static bool DeleteDirect(string targetPath)
    {
        FilePublicationSnapshot target =
            FilePublicationSnapshot.Capture(targetPath);
        return target.Exists
            && FilePublicationOwnedDelete.TryDelete(targetPath, target);
    }

    private static SafeBackupResult? PlannedBackup(
        string? backupPath,
        FilePublicationSnapshot original)
    {
        if (backupPath is null || !original.Exists)
        {
            return null;
        }

        string path = Path.GetFullPath(backupPath);
        bool exists = File.Exists(path);
        return new SafeBackupResult(
            path,
            Created: !exists,
            exists ? new FileInfo(path).Length : original.Length);
    }

    private static CliException ManifestBudgetExceeded(string reason) =>
        CliErrors.OutputUnwritable(
            ManifestPath ?? Root ?? "worker-output-manifest",
            reason,
            phase: "worker-manifest");

}

internal sealed class WorkerOutputManifest
{
    public int Version { get; init; } = 1;

    public List<WorkerOutputEntry> Entries { get; init; } = [];

    public List<WorkerDirectoryEntry> Directories { get; init; } = [];
}

internal sealed class WorkerDirectoryEntry
{
    public required string Target { get; init; }

    public required bool Existed { get; init; }

    public FilePhysicalIdentity? CreatedIdentity { get; set; }
}

internal enum WorkerPublicationState
{
    Staged,
    Publishing,
    Published,
    Restored,
}

internal sealed class WorkerOutputEntry
{
    public required string Target { get; init; }

    public required string Staged { get; init; }

    public required bool Overwrite { get; init; }

    public string? BackupPath { get; init; }

    public FilePublicationSnapshot? BackupOriginal { get; init; }

    public string? OriginalBackup { get; init; }

    public FilePublicationSnapshot? OriginalBackupSnapshot { get; init; }

    public required FilePublicationSnapshot Original { get; init; }

    public required FilePublicationSnapshot StagedSnapshot { get; init; }

    public FilePublicationSnapshot? PublishedSnapshot { get; set; }

    public WorkerPublicationState State { get; set; }

    public bool DeleteTarget { get; set; }
}
