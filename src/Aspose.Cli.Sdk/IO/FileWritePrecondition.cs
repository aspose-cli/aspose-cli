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

    /// <summary>
    /// Admits the source a product engine has just loaded: describes it as it is now, refuses it
    /// when it changed while the engine loaded it, and then when it does not have the
    /// fingerprint <c>--if-match</c> names, or that value is not a SHA-256.
    /// </summary>
    /// <param name="format">The format id the product detected.</param>
    /// <param name="protection">How the loaded document is protected.</param>
    /// <param name="ifMatch">The value of <c>--if-match</c>, or null when it was not given.</param>
    /// <returns>The <c>input</c> object the edit result reports.</returns>
    public InputInfo Admit(string format, InputProtection protection, string? ifMatch)
    {
        ArgumentNullException.ThrowIfNull(protection);
        InputInfo input = FileObjects.Input(Path, format, protection.Encrypted);
        FileFingerprints.EnsureUnchanged(Path, Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(Path, ifMatch, input.Fingerprint!);
        return input;
    }

    /// <summary>Refuses a private copy of the source whose bytes are not the admitted ones.</summary>
    /// <param name="copyPath">The copy, such as a verification baseline.</param>
    public void EnsureCopyMatches(string copyPath) =>
        FileFingerprints.EnsureUnchanged(Path, Fingerprint, FileFingerprints.Capture(copyPath));

    internal FilePublicationSnapshot Snapshot => _snapshot;

    internal static FileWritePrecondition FromSnapshot(string path, FilePublicationSnapshot snapshot) => new(path, snapshot);

    internal bool Targets(string targetPath) => FilePaths.Same(Path, System.IO.Path.GetFullPath(targetPath));

    internal void EnsureUnchanged()
    {
        FileFingerprint actual = FileFingerprints.Capture(Path);
        FileFingerprints.EnsureUnchanged(Path, Fingerprint, actual);
    }
}
