using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

internal readonly record struct FilePhysicalIdentity(
    uint VolumeSerialNumber,
    ulong FileIndex);

/// <summary>Deletes only the physical file admitted by a verified snapshot.</summary>
internal static class FilePublicationOwnedDelete
{
    private const uint DeleteAccess = 0x00010000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const int FileDispositionInfoClass = 4;

    public static bool TryDelete(string path, FilePublicationSnapshot expected)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(expected);
        FileAttributes? attributes = TryGetAttributesNoFollow(path);
        if (attributes is null)
        {
            return true;
        }
        if (attributes.Value.HasFlag(FileAttributes.Directory)
            || attributes.Value.HasFlag(FileAttributes.ReparsePoint)
            || !File.Exists(path))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            if (!expected.ContentMatches(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        return TryDeleteWindows(path, expected);
    }

    public static bool TryDelete(string path, FilePhysicalIdentity expectedIdentity)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        FileAttributes? attributes = TryGetAttributesNoFollow(path);
        if (attributes is null)
        {
            return true;
        }
        if (attributes.Value.HasFlag(FileAttributes.Directory)
            || attributes.Value.HasFlag(FileAttributes.ReparsePoint)
            || !File.Exists(path))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using SafeFileHandle deleteHandle = OpenDeleteHandle(path);
        if (deleteHandle.IsInvalid
            || TryGetIdentity(deleteHandle) != expectedIdentity)
        {
            return false;
        }

        var disposition = new FileDispositionInfo { DeleteFile = true };
        return SetFileInformationByHandle(
            deleteHandle,
            FileDispositionInfoClass,
            ref disposition,
            Marshal.SizeOf<FileDispositionInfo>());
    }

    public static bool TryDeleteDirectory(
        string path,
        FilePhysicalIdentity? expectedIdentity)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        FileAttributes? attributes = TryGetAttributesNoFollow(path);
        if (attributes is null)
        {
            return true;
        }
        if (!attributes.Value.HasFlag(FileAttributes.Directory)
            || attributes.Value.HasFlag(FileAttributes.ReparsePoint)
            || !Directory.Exists(path))
        {
            return false;
        }
        if (!OperatingSystem.IsWindows())
        {
            Directory.Delete(path, recursive: false);
            return true;
        }
        if (expectedIdentity is null)
        {
            return false;
        }

        using SafeFileHandle handle = CreateFile(
            ToWin32Path(path),
            DeleteAccess | FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid
            || TryGetIdentity(handle) != expectedIdentity)
        {
            return false;
        }
        var disposition = new FileDispositionInfo { DeleteFile = true };
        return SetFileInformationByHandle(
            handle,
            FileDispositionInfoClass,
            ref disposition,
            Marshal.SizeOf<FileDispositionInfo>());
    }

    public static SafeFileHandle OpenIdentityHandle(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Physical file identity handles are available only on Windows.");
        }

        return CreateFile(
            ToWin32Path(path),
            desiredAccess: 0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint,
            IntPtr.Zero);
    }

    public static FilePhysicalIdentity? TryGetIdentity(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows()
            || handle.IsInvalid
            || !GetFileInformationByHandle(handle, out ByHandleFileInformation info))
        {
            return null;
        }

        return new FilePhysicalIdentity(
            info.VolumeSerialNumber,
            ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
    }

    public static FilePhysicalIdentity? TryGetDirectoryIdentity(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using SafeFileHandle handle = CreateFile(
            ToWin32Path(path),
            desiredAccess: 0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        return TryGetIdentity(handle);
    }

    public static FileAttributes? TryGetAttributesNoFollow(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!OperatingSystem.IsWindows())
        {
            return File.Exists(path) || Directory.Exists(path)
                ? File.GetAttributes(path)
                : null;
        }

        using SafeFileHandle handle = CreateFile(
            ToWin32Path(path),
            desiredAccess: 0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        return !handle.IsInvalid
            && GetFileInformationByHandle(
                handle,
                out ByHandleFileInformation information)
                    ? (FileAttributes)information.FileAttributes
                    : null;
    }

    private static bool TryDeleteWindows(
        string path,
        FilePublicationSnapshot expected)
    {
        if (expected.PhysicalIdentity is not { } expectedIdentity)
        {
            return false;
        }

        SafeFileHandle? readHandle = null;
        SafeFileHandle? deleteHandle = null;
        try
        {
            readHandle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                FileOptions.SequentialScan);
            deleteHandle = OpenDeleteHandle(path);
            if (deleteHandle.IsInvalid)
            {
                return false;
            }

            FilePhysicalIdentity? readIdentity = TryGetIdentity(readHandle);
            FilePhysicalIdentity? deleteIdentity = TryGetIdentity(deleteHandle);
            if (readIdentity is null
                || deleteIdentity is null
                || readIdentity.Value != deleteIdentity.Value
                || readIdentity.Value != expectedIdentity)
            {
                return false;
            }

            long length = RandomAccess.GetLength(readHandle);
            string hash;
            using (var stream = new FileStream(readHandle, FileAccess.Read))
            {
                readHandle = null;
                hash = Convert.ToHexString(SHA256.HashData(stream));
            }

            if (length != expected.Length
                || !string.Equals(hash, expected.Sha256, StringComparison.Ordinal))
            {
                return false;
            }

            var disposition = new FileDispositionInfo { DeleteFile = true };
            return SetFileInformationByHandle(
                deleteHandle,
                FileDispositionInfoClass,
                ref disposition,
                Marshal.SizeOf<FileDispositionInfo>());
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            readHandle?.Dispose();
            deleteHandle?.Dispose();
        }
    }

    private static SafeFileHandle OpenDeleteHandle(string path) =>
        CreateFile(
            ToWin32Path(path),
            DeleteAccess | FileReadAttributes,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint,
            IntPtr.Zero);

    private static string ToWin32Path(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.StartsWith("\\\\?\\", StringComparison.Ordinal))
        {
            return full;
        }
        return full.StartsWith("\\\\", StringComparison.Ordinal)
            ? "\\\\?\\UNC\\" + full[2..]
            : "\\\\?\\" + full;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int informationClass,
        ref FileDispositionInfo information,
        int bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
