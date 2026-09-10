using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Validated immutable inventory of one renderer-owned version directory.
/// Only listed regular files may be served.
/// </summary>
internal sealed class PreviewArtifactManifest
{
    internal const int MaximumPathSegments = 16;

    private readonly IReadOnlyDictionary<string, ArtifactRecord> _files;

    private PreviewArtifactManifest(
        string root,
        string entryFileName,
        IReadOnlyDictionary<string, ArtifactRecord> files,
        long totalBytes)
    {
        Root = root;
        EntryFileName = entryFileName;
        _files = files;
        TotalBytes = totalBytes;
    }

    public string Root { get; }

    public string EntryFileName { get; }

    public int FileCount => _files.Count;

    public IReadOnlyList<string> Files => _files.Keys
        .Order(StringComparer.Ordinal)
        .ToArray();

    public long TotalBytes { get; }

    public long EntryLength => _files[EntryFileName].Length;

    public static PreviewArtifactManifest Validate(
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
                    root,
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

        return new PreviewArtifactManifest(
            root,
            entry,
            files,
            total);
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

    public FileStream? TryOpenRead(string relative)
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
            if (HasLinkedComponent(full))
            {
                return null;
            }

            var stream = new FileStream(
                full,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.Asynchronous
                        | FileOptions.SequentialScan,
                });
            if (stream.Length != expected.Length
                || !OpenedFileBoundary.IsRegularSingleLinkFile(
                    stream.SafeFileHandle)
                || !OpenedFileBoundary.IsInside(
                    Root,
                    stream.SafeFileHandle))
            {
                stream.Dispose();
                return null;
            }

            byte[] actualHash = SHA256.HashData(stream);
            if (!actualHash.AsSpan().SequenceEqual(expected.Sha256))
            {
                stream.Dispose();
                return null;
            }
            stream.Position = 0;

            return stream;
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
        string root,
        FileInfo file,
        string relative,
        LocalServiceResourceLimits limits,
        long acceptedBytes)
    {
        using var stream = new FileStream(
            file.FullName,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.SequentialScan,
            });
        if (!OpenedFileBoundary.IsRegularSingleLinkFile(stream.SafeFileHandle)
            || !OpenedFileBoundary.IsInside(root, stream.SafeFileHandle))
        {
            throw new UnauthorizedAccessException(
                $"Preview artifact '{relative}' is not an owned regular file.");
        }

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

    private bool HasLinkedComponent(string full)
    {
        if ((File.GetAttributes(Root)
                & FileAttributes.ReparsePoint) != 0
            || new DirectoryInfo(Root).LinkTarget is not null)
        {
            return true;
        }

        string relative = Path.GetRelativePath(Root, full);
        string current = Root;
        foreach (string segment in relative.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current)
                    & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
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

/// <summary>
/// Verifies the object behind an already-open handle. This closes the
/// check/open race: path components may be inspected before open for a useful
/// diagnostic, but the authorization decision is made from the opened handle.
/// </summary>
internal static class OpenedFileBoundary
{
    private const int FinalPathNameNormalized = 0;
    private const int AtEmptyPath = 0x1000;
    private const uint StatxType = 0x0000_0001;
    private const uint StatxNlink = 0x0000_0004;
    private const ushort UnixRegularFile = 0x8000;

    public static bool IsInside(
        string root,
        SafeFileHandle handle)
    {
        string? opened = ResolvePath(handle);
        if (opened is null)
        {
            return false;
        }

        string canonicalRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar);
        string canonicalOpened = Path.GetFullPath(opened);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return canonicalOpened.StartsWith(
            canonicalRoot + Path.DirectorySeparatorChar,
            comparison);
    }

    public static bool IsRegularSingleLinkFile(
        SafeFileHandle handle)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetFileInformationByHandle(
                    handle,
                    out ByHandleFileInformation info)
                && (info.FileAttributes
                    & (uint)FileAttributes.Directory) == 0
                && info.NumberOfLinks == 1;
        }

        if (OperatingSystem.IsLinux())
        {
            int descriptor = checked((int)handle.DangerousGetHandle());
            int result = LinuxStatx(
                descriptor,
                string.Empty,
                AtEmptyPath,
                StatxType | StatxNlink,
                out LinuxStatxBuffer info);
            return result == 0
                && (info.Mask & (StatxType | StatxNlink))
                    == (StatxType | StatxNlink)
                && (info.Mode & 0xF000) == UnixRegularFile
                && info.LinkCount == 1;
        }

        // Platforms without a reliable no-follow, link-count adapter fail
        // closed instead of serving an object that was only path-checked.
        return false;
    }

    private static string? ResolvePath(SafeFileHandle handle)
    {
        if (OperatingSystem.IsWindows())
        {
            var buffer = new StringBuilder(1024);
            uint length = GetFinalPathNameByHandle(
                handle,
                buffer,
                (uint)buffer.Capacity,
                FinalPathNameNormalized);
            if (length == 0 || length >= buffer.Capacity)
            {
                return null;
            }

            string value = buffer.ToString();
            if (value.StartsWith(
                    @"\\?\UNC\",
                    StringComparison.OrdinalIgnoreCase))
            {
                return @"\\" + value[8..];
            }

            return value.StartsWith(
                    @"\\?\",
                    StringComparison.OrdinalIgnoreCase)
                ? value[4..]
                : value;
        }

        if (OperatingSystem.IsLinux())
        {
            string link = $"/proc/self/fd/{handle.DangerousGetHandle()}";
            try
            {
                return new FileInfo(link)
                    .ResolveLinkTarget(returnFinalTarget: true)
                    ?.FullName;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder path,
        uint pathLength,
        int flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int LinuxStatx(
        int directoryFileDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        uint mask,
        out LinuxStatxBuffer buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(16)]
        public uint LinkCount;

        [FieldOffset(28)]
        public ushort Mode;
    }
}
