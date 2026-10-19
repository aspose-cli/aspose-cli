using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

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
    private const uint FileWriteAttributes = 0x00000100;
    private const int FileBasicInfoClass = 0;
    private const int FileDispositionInfoClass = 4;

    /// <summary>
    /// Deletes the file at <paramref name="path"/> when it is still the one
    /// <paramref name="expected"/> admitted. With <paramref name="clearReadOnly"/>, which only a
    /// publication passes for a tree its transaction owns, a read-only attribute of the admitted
    /// file is cleared first, through the same handle, so it does not keep the file.
    /// </summary>
    public static bool TryDelete(string path, FilePublicationSnapshot expected, bool clearReadOnly = false)
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

        return TryDeleteVerified(path, expected, clearReadOnly);
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
        return OpenedFileBoundary.GetInformation(handle)?.Identity;
    }

    public static FilePhysicalIdentity? TryGetDirectoryIdentity(string path)
    {
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
        using SafeFileHandle handle = CreateFile(
            ToWin32Path(path),
            desiredAccess: 0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        return OpenedFileBoundary.GetInformation(handle)?.Attributes;
    }

    private static bool TryDeleteVerified(
        string path,
        FilePublicationSnapshot expected,
        bool clearReadOnly)
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
            deleteHandle = OpenDeleteHandle(path, clearReadOnly ? FileWriteAttributes : 0);
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

            FileAttributes? attributes = clearReadOnly
                ? OpenedFileBoundary.GetInformation(deleteHandle)?.Attributes : null;
            bool cleared = attributes is { } readOnly && readOnly.HasFlag(FileAttributes.ReadOnly)
                && SetAttributes(deleteHandle, readOnly & ~FileAttributes.ReadOnly);
            var disposition = new FileDispositionInfo { DeleteFile = true };
            if (SetFileInformationByHandle(
                deleteHandle,
                FileDispositionInfoClass,
                ref disposition,
                Marshal.SizeOf<FileDispositionInfo>()))
            {
                return true;
            }
            // A file that stays keeps the attribute it had.
            if (cleared) { SetAttributes(deleteHandle, attributes!.Value); }
            return false;
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

    // Zero times leave the file's times unchanged; zero attributes would too, so none is Normal.
    private static bool SetAttributes(SafeFileHandle handle, FileAttributes attributes)
    {
        var info = new FileBasicInfo
        {
            FileAttributes = attributes == 0 ? (uint)FileAttributes.Normal : (uint)attributes,
        };
        return SetFileInformationByHandle(handle, FileBasicInfoClass, ref info, Marshal.SizeOf<FileBasicInfo>());
    }

    private static SafeFileHandle OpenDeleteHandle(string path, uint additionalAccess = 0) =>
        CreateFile(
            ToWin32Path(path),
            DeleteAccess | FileReadAttributes | additionalAccess,
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
        return FilePaths.IsRemoteOrDevice(full)
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
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int informationClass,
        ref FileDispositionInfo information,
        int bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int informationClass,
        ref FileBasicInfo information,
        int bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileBasicInfo
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }

}
