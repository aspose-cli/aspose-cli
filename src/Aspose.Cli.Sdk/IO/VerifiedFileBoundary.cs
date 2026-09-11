using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// An immutable authorization boundary identified by its physical directory chain.
/// Unsupported platforms and unprovable boundaries deny reads.
/// </summary>
public sealed class VerifiedFileBoundary
{
    private readonly string _root;
    private readonly string _prefix;
    private readonly FilePhysicalIdentity[]? _ancestors;

    public VerifiedFileBoundary(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        _prefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var handles = new List<SafeFileHandle>();
        try
        {
            _ancestors = PinDirectories(_root, handles, expected: null);
        }
        catch (Exception exception) when (IsReadRefusal(exception))
        {
            _ancestors = null;
        }
        finally
        {
            Release(handles);
        }
    }

    /// <summary>
    /// Opens a regular, single-link file while pinning every ancestor against mutation
    /// and replacement. Authorization and all reads use the same file handle.
    /// </summary>
    public VerifiedReadLease? TryOpenRead(string path)
    {
        if (_ancestors is null)
        {
            return null;
        }
        var handles = new List<SafeFileHandle>();
        SafeFileHandle? file = null;
        try
        {
            ValidateLocalPath(path);
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(_prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            _ = PinDirectories(Path.GetDirectoryName(full)!, handles, _ancestors);
            file = OpenedFileBoundary.OpenNoFollow(full, directory: false);
            if (!OpenedFileBoundary.IsRegularSingleLinkFile(file)
                || !OpenedFileBoundary.HasPath(file, full))
            {
                return null;
            }
            var stream = new FileStream(file, FileAccess.Read, bufferSize: 1, isAsync: false);
            file = null;
            var lease = new VerifiedReadLease(stream, handles.ToArray());
            handles.Clear();
            return lease;
        }
        catch (Exception exception) when (IsReadRefusal(exception))
        {
            return null;
        }
        finally
        {
            file?.Dispose();
            Release(handles);
        }
    }

    private static FilePhysicalIdentity[] PinDirectories(
        string directory, List<SafeFileHandle> handles, FilePhysicalIdentity[]? expected)
    {
        ValidateLocalPath(directory);
        string drive = Path.GetPathRoot(directory)!;
        string[] segments = directory[drive.Length..].Split('\\',
            StringSplitOptions.RemoveEmptyEntries);
        var identities = new List<FilePhysicalIdentity>();
        string current = drive;
        for (int index = 0; index <= segments.Length; index++)
        {
            if (index > 0)
            {
                current = Path.Combine(current, segments[index - 1]);
            }
            SafeFileHandle handle = OpenedFileBoundary.OpenNoFollow(current, directory: true);
            handles.Add(handle);
            if (OpenedFileBoundary.GetInformation(handle) is not { } info
                || (info.Attributes & FileAttributes.Directory) == 0
                || (info.Attributes & FileAttributes.ReparsePoint) != 0
                || !OpenedFileBoundary.HasPath(handle, current)
                || (expected is not null && index < expected.Length
                    && info.Identity != expected[index]))
            {
                throw new UnauthorizedAccessException("The physical resource directory is not authorized.");
            }
            identities.Add(info.Identity);
        }
        if (expected is not null && identities.Count < expected.Length)
        {
            throw new UnauthorizedAccessException("The resource is outside the authorized directory.");
        }
        return identities.ToArray();
    }

    private static void ValidateLocalPath(string path)
    {
        // Validate before normalization: Win32 aliases must not become ordinary paths.
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':'
            || path[2] != '\\'
            || path[3..].Split('\\').Any(segment =>
                ExtractionPathValidator.IsUnsafeSegment(segment)))
        {
            if (path.Length == 3 && char.IsAsciiLetter(path[0])
                && path[1] == ':' && path[2] == '\\')
            {
                return;
            }
            throw new UnauthorizedAccessException("Only canonical local file paths are authorized.");
        }
    }

    private static bool IsReadRefusal(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or System.Security.SecurityException;

    private static void Release(IEnumerable<SafeFileHandle> handles)
    {
        foreach (SafeFileHandle handle in handles.Reverse())
        {
            handle.Dispose();
        }
    }
}
