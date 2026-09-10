using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>
/// Owns the bounded worker manifest format and validates every path before it
/// crosses the worker-to-parent process boundary.
/// </summary>
internal static class WorkerManifestStore
{
    internal const int MaximumEntries = 256;
    internal const int MaximumDirectories = 256;
    private const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static WorkerOutputManifest ReadAndValidate(string manifestPath)
    {
        string fullManifest = Path.GetFullPath(manifestPath);
        try
        {
            (string root, _) = ValidateManifestPath(
                fullManifest,
                requireManifest: true);
            using var stream = new FileStream(
                fullManifest,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length < 1 || stream.Length > MaximumBytes)
            {
                throw new InvalidDataException(
                    "Worker output manifest violates its byte budget.");
            }

            WorkerOutputManifest manifest =
                JsonSerializer.Deserialize<WorkerOutputManifest>(stream, JsonOptions)
                ?? throw new InvalidDataException(
                    "Worker output manifest is empty.");
            ValidateManifest(root, manifest);
            return manifest;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or IOException
                or UnauthorizedAccessException
                or ArgumentException
                or JsonException
                or CliException)
        {
            throw CliErrors.OutputUnwritable(
                fullManifest,
                "the worker output manifest failed validation",
                exception,
                phase: "worker-manifest");
        }
    }

    internal static void Write(
        string path,
        WorkerOutputManifest manifest)
    {
        ValidateManifestPath(path, requireManifest: false);
        if (manifest.Entries.Count > MaximumEntries
            || manifest.Directories.Count > MaximumDirectories)
        {
            throw CliErrors.OutputUnwritable(
                path,
                "the worker output manifest entry budget was exceeded",
                phase: "worker-manifest");
        }
        string contents = JsonSerializer.Serialize(manifest, JsonOptions);
        if (Encoding.UTF8.GetByteCount(contents) > MaximumBytes)
        {
            throw CliErrors.OutputUnwritable(
                path,
                "the worker output manifest byte budget was exceeded",
                phase: "worker-manifest");
        }
        PrivateUserStorage.WriteAllText(path, contents);
        FilePublicationDurabilityAdapter.FlushFile(path);
    }

    internal static (string Root, string Manifest) ValidateSessionPaths(
        string? rootPath,
        string? manifestPath,
        bool requireManifest)
    {
        string root = Path.GetFullPath(
            rootPath ?? throw new InvalidOperationException(
                "Worker output root is unavailable."));
        (string validatedRoot, string manifest) = ValidateManifestPath(
            manifestPath ?? throw new InvalidOperationException(
                "Worker output manifest path is unavailable."),
            requireManifest);
        if (!PathComparer.Equals(root, validatedRoot))
        {
            throw new InvalidDataException(
                "Worker output root and manifest directory do not match.");
        }
        return (validatedRoot, manifest);
    }

    private static void ValidateManifest(
        string root,
        WorkerOutputManifest manifest)
    {
        if (manifest.Version != 1
            || manifest.Entries is null
            || manifest.Directories is null
            || manifest.Entries.Count > MaximumEntries
            || manifest.Directories.Count > MaximumDirectories)
        {
            throw new InvalidDataException(
                "Worker output manifest violates its bounded contract.");
        }

        var paths = new HashSet<string>(PathComparer);
        var directoryPaths = new HashSet<string>(PathComparer);
        foreach (WorkerOutputEntry? entry in manifest.Entries)
        {
            ValidateEntry(root, entry, paths);
        }
        foreach (WorkerDirectoryEntry? directory in manifest.Directories)
        {
            ValidateDirectory(directory, paths, directoryPaths);
        }
        foreach (WorkerOutputEntry entry in manifest.Entries)
        {
            RequireDeclaredParents(entry.Target, directoryPaths);
            if (entry.BackupPath is not null)
            {
                RequireDeclaredParents(entry.BackupPath, directoryPaths);
            }
        }
        foreach (WorkerDirectoryEntry directory in manifest.Directories)
        {
            RequireDeclaredParents(directory.Target, directoryPaths);
        }
    }

    private static void ValidateDirectory(
        WorkerDirectoryEntry? directory,
        ISet<string> paths,
        ISet<string> directoryPaths)
    {
        if (directory is null)
        {
            throw new InvalidDataException(
                "Worker output manifest contains an empty directory entry.");
        }
        string target = ValidateCanonicalPath(directory.Target);
        if (!paths.Add(target) || File.Exists(target))
        {
            throw new InvalidDataException(
                $"Worker output directory conflicts with another path: '{target}'.");
        }
        ExtractionPathValidator.EnsureNoLinks(target);
        if (directory.Existed && directory.CreatedIdentity is not null)
        {
            throw new InvalidDataException(
                $"Worker output directory has unexpected creation ownership: '{target}'.");
        }
        if (directory.CreatedIdentity is { } created
            && FilePublicationOwnedDelete.TryGetDirectoryIdentity(target) != created)
        {
            throw new InvalidDataException(
                $"Worker output directory changed after creation: '{target}'.");
        }
        directoryPaths.Add(target);
    }

    private static void RequireDeclaredParents(
        string path,
        IReadOnlySet<string> directories)
    {
        string? current = Path.GetDirectoryName(path);
        while (current is not null && !Directory.Exists(current))
        {
            if (!directories.Contains(current))
            {
                throw new InvalidDataException(
                    $"Worker output path has an undeclared parent directory: '{current}'.");
            }
            current = Path.GetDirectoryName(current);
        }
    }

    private static void ValidateEntry(
        string root,
        WorkerOutputEntry? entry,
        ISet<string> paths)
    {
        if (entry is null
            || !Enum.IsDefined(entry.State)
            || !entry.Original.IsStructurallyValid()
            || !entry.StagedSnapshot.IsStructurallyValid()
            || entry.Original.Exists && !entry.Overwrite
            || entry.DeleteTarget && entry.BackupPath is not null
            || entry.PublishedSnapshot is not null
                && !entry.PublishedSnapshot.IsStructurallyValid())
        {
            throw new InvalidDataException(
                "Worker output manifest contains an incomplete file entry.");
        }

        string target = ValidateCanonicalPath(entry.Target);
        string staged = ValidatePrivateFile(root, entry.Staged, "output.stage");
        if (!paths.Add(target)
            || Directory.Exists(target)
            || !entry.StagedSnapshot.VersionEquals(
                FilePublicationSnapshot.Capture(staged))
            || entry.StagedSnapshot.Metadata is not null
                && !entry.StagedSnapshot.Metadata.Matches(staged))
        {
            throw new InvalidDataException(
                $"Worker output '{target}' failed staging validation.");
        }
        ExtractionPathValidator.EnsureNoLinks(target);
        ValidatePublicationState(entry);
        ValidateOriginalBackup(root, entry);
        ValidateRequestedBackup(entry, paths);
    }

    private static void ValidatePublicationState(WorkerOutputEntry entry)
    {
        bool valid = entry.State switch
        {
            WorkerPublicationState.Staged or WorkerPublicationState.Restored =>
                entry.PublishedSnapshot is null,
            WorkerPublicationState.Publishing =>
                entry.PublishedSnapshot is null
                || entry.DeleteTarget && !entry.PublishedSnapshot.Exists,
            WorkerPublicationState.Published =>
                entry.PublishedSnapshot is not null
                && (entry.DeleteTarget
                    ? !entry.PublishedSnapshot.Exists
                    : entry.PublishedSnapshot.Exists),
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidDataException(
                $"Worker output '{entry.Target}' has an invalid publication state.");
        }
    }

    private static void ValidateOriginalBackup(
        string root,
        WorkerOutputEntry entry)
    {
        if (!entry.Original.Exists)
        {
            if (entry.OriginalBackup is not null
                || entry.OriginalBackupSnapshot is not null)
            {
                throw new InvalidDataException(
                    $"Worker output '{entry.Target}' has an unexpected original backup.");
            }
            return;
        }
        if (entry.OriginalBackup is null
            || entry.OriginalBackupSnapshot is null)
        {
            throw new InvalidDataException(
                $"Worker output '{entry.Target}' has no original backup.");
        }

        string backup = ValidatePrivateFile(
            root,
            entry.OriginalBackup,
            "original.backup");
        if (!entry.OriginalBackupSnapshot.IsStructurallyValid())
        {
            throw new InvalidDataException(
                $"Worker output '{entry.Target}' has an invalid original backup snapshot.");
        }
        FilePublicationSnapshot actual = FilePublicationSnapshot.Capture(backup);
        if (!entry.OriginalBackupSnapshot.VersionEquals(actual)
            || !entry.Original.ContentEquals(actual))
        {
            throw new InvalidDataException(
                $"Worker output '{entry.Target}' has an invalid original backup.");
        }
    }

    private static void ValidateRequestedBackup(
        WorkerOutputEntry entry,
        ISet<string> paths)
    {
        if (entry.BackupPath is null)
        {
            if (entry.BackupOriginal is not null)
            {
                throw new InvalidDataException(
                    $"Worker output '{entry.Target}' has an unexpected backup snapshot.");
            }
            return;
        }

        string backup = ValidateCanonicalPath(entry.BackupPath);
        if (!entry.Original.Exists
            || entry.BackupOriginal is null
            || !entry.BackupOriginal.IsStructurallyValid()
            || !paths.Add(backup)
            || Directory.Exists(backup))
        {
            throw new InvalidDataException(
                $"Worker output '{entry.Target}' has an invalid backup path.");
        }
        ExtractionPathValidator.EnsureNoLinks(backup);
    }

    private static (string Root, string Manifest) ValidateManifestPath(
        string manifestPath,
        bool requireManifest)
    {
        string manifest = Path.GetFullPath(manifestPath);
        string root = Path.GetDirectoryName(manifest)
            ?? throw new InvalidDataException(
                "Worker output manifest has no parent directory.");
        string category = Path.Combine(
            PrivateUserStorage.TemporaryRoot(),
            "worker");
        PrivateUserStorage.ValidateDirectory(category);
        if (!PathComparer.Equals(Path.GetDirectoryName(root), category)
            || !Guid.TryParseExact(Path.GetFileName(root), "N", out _)
            || !PathComparer.Equals(
                Path.GetFileName(manifest),
                "output-manifest.v1.json"))
        {
            throw new InvalidDataException(
                "Worker output manifest is outside the private worker root.");
        }

        PrivateUserStorage.ValidateDirectory(root);
        if (requireManifest)
        {
            PrivateUserStorage.ValidateFile(manifest);
        }
        return (root, manifest);
    }

    private static string ValidateCanonicalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException(
                "Worker output paths must be absolute.");
        }
        string full = Path.GetFullPath(path);
        if (!PathComparer.Equals(path, full))
        {
            throw new InvalidDataException(
                $"Worker output path is not canonical: '{path}'.");
        }
        return full;
    }

    private static string ValidatePrivateFile(
        string root,
        string path,
        string expectedName)
    {
        string full = ValidateCanonicalPath(path);
        string parent = Path.GetDirectoryName(full)
            ?? throw new InvalidDataException(
                $"Worker private file has no parent: '{full}'.");
        if (!IsChild(root, parent)
            || !PathComparer.Equals(Path.GetDirectoryName(parent), root)
            || !PathComparer.Equals(Path.GetFileName(full), expectedName))
        {
            throw new InvalidDataException(
                $"Worker private file is outside its bounded layout: '{full}'.");
        }
        PrivateUserStorage.ValidateDirectory(parent);
        PrivateUserStorage.ValidateFile(full);
        return full;
    }

    private static bool IsChild(string root, string path) =>
        path.StartsWith(
            Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar,
            PathComparison);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
