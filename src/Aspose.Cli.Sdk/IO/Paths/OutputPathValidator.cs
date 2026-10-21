using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO.Paths;

/// <summary>Validates local output paths without following filesystem links.</summary>
internal static class OutputPathValidator
{
    public static string NormalizeFile(
        string path,
        string? baseDirectory = null,
        string phase = "path")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = baseDirectory is null
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, baseDirectory);
        EnsureSafeFile(full, phase);
        return full;
    }

    /// <summary>
    /// Whether two paths name one file: equal after full resolution (ignoring case on
    /// Windows), or both existing and one physical file reached through another spelling,
    /// such as a short 8.3 name or a hard link.
    /// </summary>
    public static bool IsSameFile(string first, string second)
    {
        string left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
        string right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
        if (FilePaths.Same(left, right))
        {
            return true;
        }

        if (!File.Exists(left) || !File.Exists(right))
        {
            return false;
        }

        using SafeFileHandle leftHandle = FilePublicationOwnedDelete.OpenIdentityHandle(left);
        using SafeFileHandle rightHandle = FilePublicationOwnedDelete.OpenIdentityHandle(right);
        return FilePublicationOwnedDelete.TryGetIdentity(leftHandle) is { } identity
            && identity == FilePublicationOwnedDelete.TryGetIdentity(rightHandle);
    }

    public static void EnsureSafeFile(string path, string phase = "path")
    {
        string full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            throw Unsafe(
                full,
                "the file path is occupied by a directory",
                phase);
        }
        EnsureSafeComponents(full, phase);
    }

    public static void EnsureSafeDirectory(string path)
    {
        string full = Path.GetFullPath(path);
        if (File.Exists(full))
        {
            throw Unsafe(full, "the directory path is occupied by a file");
        }
        EnsureSafeComponents(full);
    }

    public static FilePhysicalIdentity? CaptureParentIdentity(string path)
    {
        string parent = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw Unsafe(path, "the path has no parent directory");
        EnsureSafeDirectory(parent);
        FilePhysicalIdentity? identity =
            FilePublicationOwnedDelete.TryGetDirectoryIdentity(parent);
        if (identity is null)
        {
            throw Unsafe(path, "the output directory identity could not be verified");
        }
        return identity;
    }

    public static void EnsureParentUnchanged(
        string path,
        FilePhysicalIdentity? expected)
    {
        FilePhysicalIdentity? current = CaptureParentIdentity(path);
        if (current != expected)
        {
            throw Unsafe(path, "the output directory changed during publication");
        }
    }

    private static void EnsureSafeComponents(
        string path,
        string phase = "path")
    {
        string full = Path.GetFullPath(path);
        if (FilePaths.IsRemoteOrDevice(full))
        {
            throw Unsafe(full, "UNC and device paths are not accepted", phase);
        }

        string root = Path.GetPathRoot(full)
            ?? throw Unsafe(full, "the path has no filesystem root", phase);
        string relative = full[root.Length..];
        string[] segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(IsUnsafeSegment))
        {
            throw Unsafe(
                full,
                "the path contains a reserved or unsafe segment",
                phase);
        }

        string? current = full;
        while (current is not null)
        {
            FileAttributes? attributes =
                FilePublicationOwnedDelete.TryGetAttributesNoFollow(current);
            if (attributes?.HasFlag(FileAttributes.ReparsePoint) == true)
            {
                throw Unsafe(
                    full,
                    "the path contains a symbolic link or reparse point",
                    phase);
            }
            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }
            current = parent;
        }
    }

    /// <summary>
    /// Whether one path segment is no plain file name: empty, <c>.</c> or <c>..</c>, ending in a
    /// space or a dot, holding a character no file name takes, or a device name such as
    /// <c>NUL</c> or <c>COM1</c>, with or without extensions.
    /// </summary>
    internal static bool IsUnsafeSegment(string segment)
    {
        if (segment.Length == 0
            || segment is "." or ".."
            || segment.EndsWith(' ')
            || segment.EndsWith('.')
            || segment.Contains(':')
            || segment.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0
            || segment.Any(static character => char.IsControl(character))
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return true;
        }

        int extension = segment.IndexOf('.');
        string device = (extension < 0 ? segment : segment[..extension]).TrimEnd(' ').ToUpperInvariant();
        return device is "CON" or "PRN" or "AUX" or "NUL"
            || (device.Length == 4
                && (device.StartsWith("COM", StringComparison.Ordinal)
                    || device.StartsWith("LPT", StringComparison.Ordinal))
                && device[3] is >= '1' and <= '9' or '\u00B9' or '\u00B2' or '\u00B3');
    }

    private static CliException Unsafe(
        string path,
        string reason,
        string phase = "path") =>
        CliErrors.OutputUnwritable(
            Path.GetFullPath(path),
            reason,
            phase: phase);
}
