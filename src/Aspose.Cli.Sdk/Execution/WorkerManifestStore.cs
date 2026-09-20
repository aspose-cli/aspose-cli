using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>Validates immutable worker plans before their data reaches the publisher.</summary>
internal static class WorkerManifestStore
{
    internal const int MaximumEntries = PublicationLimits.MaximumEntries;
    internal const int MaximumDirectories = PublicationLimits.MaximumDirectories;
    private const int MaximumBytes = PublicationLimits.MaximumMetadataBytes;
    internal static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static WorkerOutputManifest ReadAndValidate(string manifestPath)
    {
        try
        {
            (string root, string path) = ValidateSessionPaths(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, manifestPath, true);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is < 1 or > MaximumBytes) { throw new InvalidDataException("Worker manifest byte budget exceeded."); }
            WorkerOutputManifest manifest = JsonSerializer.Deserialize<WorkerOutputManifest>(stream, JsonOptions)
                ?? throw new InvalidDataException("The worker manifest is empty.");
            Validate(root, manifest);
            return manifest;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or CliException)
        {
            throw CliErrors.OutputUnwritable(manifestPath, "the worker output manifest failed validation", error, "worker-manifest");
        }
    }

    internal static void Write(string manifestPath, WorkerOutputManifest manifest)
    {
        _ = ValidateSessionPaths(Path.GetDirectoryName(manifestPath)!, manifestPath, false);
        string contents = CheckCapacity(manifest);
        PrivateUserStorage.WriteAllText(manifestPath, contents);
        FilePublicationDurabilityAdapter.FlushFile(manifestPath);
    }

    internal static string CheckCapacity(WorkerOutputManifest manifest)
    {
        if (manifest.Entries.Count > MaximumEntries || manifest.Directories.Count > MaximumDirectories)
        {
            throw new InvalidDataException("Worker manifest entry budget exceeded.");
        }
        string contents = JsonSerializer.Serialize(manifest, JsonOptions);
        if (Encoding.UTF8.GetByteCount(contents) > MaximumBytes) { throw new InvalidDataException("Worker manifest byte budget exceeded."); }
        return contents;
    }

    internal static (string Root, string Manifest) ValidateSessionPaths(string root, string manifestPath, bool requireManifest)
    {
        string fullRoot = Canonical(root);
        string path = Canonical(manifestPath);
        string category = Path.Combine(PrivateUserStorage.TemporaryRoot(), "worker");
        if (!PathComparer.Equals(Path.GetDirectoryName(fullRoot), category)
            || !Guid.TryParseExact(Path.GetFileName(fullRoot), "N", out _)
            || !PathComparer.Equals(Path.GetDirectoryName(path), fullRoot)
            || Path.GetFileName(path) != WorkerOutputSession.ManifestName)
        {
            throw new InvalidDataException("Worker manifest is outside its owned private root.");
        }
        PrivateUserStorage.ValidateDirectory(fullRoot);
        if (requireManifest) { PrivateUserStorage.ValidateFile(path); }
        return (fullRoot, path);
    }

    private static void Validate(string root, WorkerOutputManifest manifest)
    {
        if (manifest.Version != 4 || !manifest.Sealed || manifest.Entries is null || manifest.Directories is null
            || manifest.Entries.Count > MaximumEntries || manifest.Directories.Count > MaximumDirectories)
        {
            throw new InvalidDataException("The worker manifest violates its bounded contract.");
        }
        var paths = new HashSet<string>(PathComparer);
        foreach (WorkerOutputEntry entry in manifest.Entries)
        {
            if (entry is null || entry.Original is null || entry.StagedSnapshot is null
                || !entry.Original.IsStructurallyValid() || !entry.StagedSnapshot.IsStructurallyValid()
                || !entry.StagedSnapshot.Exists || entry.Original.Exists && !entry.Overwrite
                || entry.DeleteTarget && (!entry.Original.Exists || entry.BackupPath is not null))
            {
                throw new InvalidDataException("A worker entry has invalid snapshots or permissions.");
            }
            string target = Canonical(entry.Target);
            EnsureOutsideRoot(root, target);
            OutputPathValidator.EnsureSafeFile(target);
            if (!paths.Add(target)) { throw new InvalidDataException("Worker target paths overlap."); }
            string staged = Canonical(entry.Staged);
            string parent = Path.GetDirectoryName(staged)!;
            if (!PathComparer.Equals(Path.GetDirectoryName(parent), root) || Path.GetFileName(staged) != "output.stage")
            {
                throw new InvalidDataException("A staged file is outside the worker layout.");
            }
            PrivateUserStorage.ValidateDirectory(parent);
            PrivateUserStorage.ValidateFile(staged);
            if (!entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(staged))
                || !entry.StagedSnapshot.Metadata!.Matches(staged)) { throw new InvalidDataException("The staged file changed after handoff."); }
            if (entry.BackupPath is { } backup)
            {
                backup = Canonical(backup);
                EnsureOutsideRoot(root, backup);
                OutputPathValidator.EnsureSafeFile(backup, "backup");
                if (!paths.Add(backup) || entry.BackupOriginal is null || !entry.BackupOriginal.IsStructurallyValid())
                { throw new InvalidDataException("Worker backup paths or snapshots are invalid."); }
            }
            if ((entry.InputPath is null) != (entry.InputSnapshot is null)
                || entry.InputSnapshot is { } input && (!input.Exists || !input.IsStructurallyValid()))
            { throw new InvalidDataException("The input precondition is incomplete."); }
            if (entry.InputPath is not null) { _ = Canonical(entry.InputPath); }
        }
        var directories = new HashSet<string>(PathComparer);
        foreach (WorkerDirectoryEntry directory in manifest.Directories)
        {
            if (directory is null || !paths.Add(Canonical(directory.Target)) || !directories.Add(directory.Target)
                || !directory.Existed && directory.OriginalIdentity is not null)
            { throw new InvalidDataException("Worker directories overlap or have invalid ownership."); }
            EnsureOutsideRoot(root, directory.Target);
            OutputPathValidator.EnsureSafeDirectory(directory.Target);
        }
        foreach (string path in paths)
        {
            for (string? parent = Path.GetDirectoryName(path); parent is not null && !Directory.Exists(parent); parent = Path.GetDirectoryName(parent))
            {
                if (!directories.Contains(parent)) { throw new InvalidDataException("A worker output has an undeclared parent directory."); }
            }
        }
    }

    private static void EnsureOutsideRoot(string root, string target)
    {
        if (PathComparer.Equals(root, target) || target.StartsWith(root + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        { throw new InvalidDataException("Worker destinations cannot overlap their private transport storage."); }
    }

    private static string Canonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !PathComparer.Equals(path, Path.GetFullPath(path)))
        { throw new InvalidDataException("Worker paths must be canonical and absolute."); }
        return Path.GetFullPath(path);
    }
}
