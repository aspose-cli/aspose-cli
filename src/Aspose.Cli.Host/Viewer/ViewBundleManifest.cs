using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using System.Security.Cryptography;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Validated immutable inventory of one renderer-owned version directory.
/// Only listed regular files may be served.
/// </summary>
internal sealed class ViewBundleManifest
{
    internal const int MaximumPathSegments = 16;

    private readonly IReadOnlyDictionary<string, ArtifactRecord> _files;
    private readonly VerifiedFileBoundary _boundary;

    private ViewBundleManifest(
        string root,
        VerifiedFileBoundary boundary,
        string entryFileName,
        IReadOnlyDictionary<string, ArtifactRecord> files)
    {
        Root = root;
        _boundary = boundary;
        EntryFileName = entryFileName;
        _files = files;
    }

    public string Root { get; }

    public string EntryFileName { get; }

    public static ViewBundleManifest Validate(
        string directory,
        string entryFileName,
        LocalServiceResourceLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(limits);
        string root = Path.GetFullPath(directory);
        if (!Directory.Exists(root)
            || (File.GetAttributes(root)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The preview artifact root must be a real directory.");
        }
        var boundary = new VerifiedFileBoundary(root);
        string entry = NormalizeRelative(entryFileName);
        var files = new Dictionary<string, ArtifactRecord>(
            StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        long total = 0;
        int directoryCount = 0;

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            foreach (FileSystemInfo item in
                new DirectoryInfo(current).EnumerateFileSystemInfos())
            {
                if ((item.Attributes
                        & FileAttributes.ReparsePoint) != 0
                    || item.LinkTarget is not null)
                {
                    throw new UnauthorizedAccessException(
                        "Preview artifacts cannot contain links or reparse points.");
                }

                if (item is DirectoryInfo child)
                {
                    _ = NormalizeRelative(
                        Path.GetRelativePath(root, child.FullName));
                    directoryCount++;
                    if (directoryCount > limits.MaximumSnapshotFiles)
                    {
                        throw CliErrors.PreviewBudgetExceeded(
                            "directory count",
                            directoryCount,
                            limits.MaximumSnapshotFiles);
                    }
                    pending.Push(child.FullName);
                    continue;
                }

                if (item is not FileInfo file)
                {
                    throw new InvalidDataException(
                        "Preview artifacts must be regular files or directories.");
                }

                string relative = NormalizeRelative(
                    Path.GetRelativePath(root, file.FullName));
                if (files.ContainsKey(relative))
                {
                    throw new InvalidDataException(
                        "Preview artifacts contain a case-folding path collision.");
                }
                if (files.Count >= limits.MaximumSnapshotFiles)
                {
                    throw CliErrors.PreviewBudgetExceeded(
                        "file count",
                        files.Count + 1,
                        limits.MaximumSnapshotFiles);
                }

                ArtifactRecord record = ReadRecord(
                    boundary,
                    file,
                    relative,
                    limits,
                    total);
                files.Add(relative, record);
                total = checked(total + record.Length);
            }
        }

        if (!files.ContainsKey(entry))
        {
            throw new InvalidDataException(
                "The preview entry file is not a regular file in the artifact manifest.");
        }

        return new ViewBundleManifest(
            root,
            boundary,
            entry,
            files);
    }

    public bool Contains(string relative)
    {
        try
        {
            return _files.ContainsKey(
                NormalizeRelative(relative));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidDataException)
        {
            return false;
        }
    }

    public VerifiedReadLease? TryOpenRead(string relative)
    {
        string normalized;
        try
        {
            normalized = NormalizeRelative(relative);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidDataException)
        {
            return null;
        }

        if (!_files.TryGetValue(
                normalized,
                out ArtifactRecord? expected)
            || expected is null)
        {
            return null;
        }

        string full = Path.GetFullPath(
            Path.Combine(
                Root,
                normalized.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
        if (!full.StartsWith(
                Root + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            VerifiedReadLease? stream = _boundary.TryOpenRead(full);
            if (stream is null)
            {
                return null;
            }
            try
            {
                if (stream.Length != expected.Length)
                {
                    return null;
                }
                byte[] actualHash = SHA256.HashData(stream);
                if (!actualHash.AsSpan().SequenceEqual(expected.Sha256))
                {
                    return null;
                }
                stream.Position = 0;
                VerifiedReadLease result = stream;
                stream = null;
                return result;
            }
            finally
            {
                stream?.Dispose();
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException
                or System.Security.SecurityException)
        {
            return null;
        }
    }

    internal static string NormalizeRelative(string relative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relative);
        if (relative.IndexOf('\0') >= 0
            || Path.IsPathRooted(relative)
            || relative.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Preview artifact paths must be canonical relative paths.");
        }

        string normalized = relative
            .Replace('\\', '/')
            .Trim('/');
        string[] segments = normalized.Split('/');
        if (segments.Length == 0
            || segments.Length > MaximumPathSegments
            || segments.Any(static segment =>
                segment.Length == 0
                || segment is "." or ".."
                || segment.EndsWith(' ')
                || segment.EndsWith('.')
                || segment.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0
                || IsReservedWindowsName(segment)))
        {
            throw new InvalidDataException(
                "Preview artifact paths contain an unsafe segment.");
        }

        return string.Join('/', segments);
    }

    private static ArtifactRecord ReadRecord(
        VerifiedFileBoundary boundary,
        FileInfo file,
        string relative,
        LocalServiceResourceLimits limits,
        long acceptedBytes)
    {
        using VerifiedReadLease stream = boundary.TryOpenRead(file.FullName)
            ?? throw new UnauthorizedAccessException(
                $"Preview artifact '{relative}' is not an owned regular file.");

        long length = stream.Length;
        if (length > limits.MaximumSnapshotFileBytes)
        {
            throw CliErrors.PreviewBudgetExceeded(
                "file size",
                length,
                limits.MaximumSnapshotFileBytes);
        }
        long total = checked(acceptedBytes + length);
        if (total > limits.MaximumSnapshotBytes)
        {
            throw CliErrors.PreviewBudgetExceeded(
                "total bytes",
                total,
                limits.MaximumSnapshotBytes);
        }
        byte[] hash = SHA256.HashData(stream);
        if (stream.Length != length)
        {
            throw new IOException(
                $"Preview artifact '{relative}' changed during validation.");
        }

        return new ArtifactRecord(length, hash);
    }

    private static bool IsReservedWindowsName(string segment)
    {
        string stem = segment.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4
                && (stem.StartsWith(
                        "COM",
                        StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith(
                        "LPT",
                        StringComparison.OrdinalIgnoreCase))
                && stem[3] is >= '1' and <= '9');
    }

    private sealed record ArtifactRecord(long Length, byte[] Sha256);
}
