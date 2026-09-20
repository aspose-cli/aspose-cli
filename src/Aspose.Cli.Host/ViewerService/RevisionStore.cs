using System.Globalization;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Owns rendered version children inside a session storage root: one
/// <c>v{revision}</c> subdirectory per render. Pruning keeps a one-version
/// grace window so responses still streaming the previous version do not
/// lose files mid-read. Final root cleanup belongs to
/// <see cref="ViewerStorage"/>.
/// </summary>
internal sealed class RevisionStore : IDisposable
{
    private readonly string _root;

    public RevisionStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
        PrivateUserStorage.EnsureDirectory(_root);
    }

    public string CreateVersionDirectory(int revision)
    {
        string path = Path.Combine(
            _root,
            string.Create(
                CultureInfo.InvariantCulture,
                $"v{revision}"));
        PrivateUserStorage.EnsureDirectory(path);
        return path;
    }

    /// <summary>
    /// Deletes every version older than <paramref name="keepFromRevision"/>.
    /// An in-flight read may defer deletion until a later prune or disposal.
    /// Unknown children are always preserved.
    /// </summary>
    public void Prune(int keepFromRevision)
    {
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(
                _root,
                "v*"))
            {
                if (!TryParseRevision(
                        Path.GetFileName(directory),
                        out int revision)
                    || revision >= keepFromRevision)
                {
                    continue;
                }

                if (!OwnedDirectory.CanDelete(
                        _root,
                        directory,
                        static name => TryParseRevision(name, out _))
                    || !LocalFileCleanup.DeleteDirectory(directory))
                {
                    // A later prune or the session owner retries after active
                    // response streams have closed. A directory whose
                    // ownership cannot be proved is intentionally preserved.
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The session root vanished or is temporarily unavailable.
        }
    }

    public void Dispose() => Prune(int.MaxValue);

    internal static bool TryParseRevision(
        string directoryName,
        out int revision)
    {
        revision = 0;
        return directoryName.Length > 1
            && directoryName[0] == 'v'
            && int.TryParse(
                directoryName.AsSpan(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out revision);
    }
}
