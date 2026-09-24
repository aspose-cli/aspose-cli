using Aspose.Cli.Sdk.Errors;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

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
        if (string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows() || !File.Exists(left) || !File.Exists(right))
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
        if (OperatingSystem.IsWindows() && identity is null)
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
        if (OperatingSystem.IsWindows() && current != expected)
        {
            throw Unsafe(path, "the output directory changed during publication");
        }
    }

    private static void EnsureSafeComponents(
        string path,
        string phase = "path")
    {
        string full = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()
            && (full.StartsWith("\\\\", StringComparison.Ordinal)
                || full.StartsWith("\\\\?\\", StringComparison.Ordinal)
                || full.StartsWith("\\\\.\\", StringComparison.Ordinal)))
        {
            throw Unsafe(full, "UNC and device paths are not accepted", phase);
        }

        string root = Path.GetPathRoot(full)
            ?? throw Unsafe(full, "the path has no filesystem root", phase);
        string relative = full[root.Length..];
        string[] segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(ExtractionPathValidator.IsUnsafeSegment))
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

    private static CliException Unsafe(
        string path,
        string reason,
        string phase = "path") =>
        CliErrors.OutputUnwritable(
            Path.GetFullPath(path),
            reason,
            phase: phase);
}
