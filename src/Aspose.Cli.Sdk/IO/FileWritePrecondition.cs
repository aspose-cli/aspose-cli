using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Binds a file's admitted bytes to a later atomic publication. Product edit
/// engines capture this before loading the document and pass it to the writer,
/// which rechecks the source immediately before publishing the staged output.
/// </summary>
public sealed class FileWritePrecondition
{
    private readonly FilePublicationSnapshot _snapshot;

    private FileWritePrecondition(string path, FilePublicationSnapshot snapshot)
    {
        Path = path;
        _snapshot = snapshot;
        Fingerprint = new FileFingerprint
        {
            Sha256 = snapshot.Sha256!.ToLowerInvariant(),
        };
    }

    /// <summary>The absolute source path captured for this edit.</summary>
    public string Path { get; }

    /// <summary>The captured source SHA-256 fingerprint.</summary>
    public FileFingerprint Fingerprint { get; }

    /// <summary>Captures an existing source before a product engine loads it.</summary>
    public static FileWritePrecondition Capture(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = System.IO.Path.GetFullPath(path);
        try
        {
            FilePublicationSnapshot snapshot = FilePublicationSnapshot.Capture(fullPath);
            if (!snapshot.Exists)
            {
                throw CliErrors.FileNotFound(fullPath);
            }

            return new FileWritePrecondition(fullPath, snapshot);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw CliErrors.FileNotFound(fullPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw CliErrors.FileAccessDenied(fullPath);
        }
        catch (IOException)
        {
            throw File.Exists(fullPath)
                ? CliErrors.FileLocked(fullPath)
                : CliErrors.FileNotFound(fullPath);
        }
    }

    internal FilePublicationSnapshot Snapshot => _snapshot;

    internal static FileWritePrecondition FromSnapshot(string path, FilePublicationSnapshot snapshot) => new(path, snapshot);

    internal bool Targets(string targetPath) =>
        string.Equals(
            Path,
            System.IO.Path.GetFullPath(targetPath),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    internal void EnsureUnchanged()
    {
        FileFingerprint actual = FileFingerprints.Capture(Path);
        FileFingerprints.EnsureUnchanged(Path, Fingerprint, actual);
    }
}
