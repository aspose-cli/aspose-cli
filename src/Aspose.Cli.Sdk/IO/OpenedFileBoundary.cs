using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

internal readonly record struct FilePhysicalIdentity(
    uint VolumeSerialNumber,
    ulong FileIndex);

/// <summary>Queries the actual object opened by the Windows file system.</summary>
internal static class OpenedFileBoundary
{
    internal readonly record struct Information(
        FilePhysicalIdentity Identity, FileAttributes Attributes, uint Links);

    internal static SafeFileHandle OpenNoFollow(string path, bool directory) =>
        CreateFile(
            @"\\?\" + path,
            directory ? 0x80u : 0x80000000u,
            1, // Share reads only: neither replacement nor mutation is allowed.
            IntPtr.Zero, 3,
            0x00200000u | (directory ? 0x02000000u : 0),
            IntPtr.Zero);

    internal static Information? GetInformation(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows() || handle.IsInvalid
            || !GetFileInformationByHandle(handle, out ByHandleFileInformation info))
        {
            return null;
        }
        return new Information(
            new FilePhysicalIdentity(info.VolumeSerialNumber,
                ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow),
            (FileAttributes)info.FileAttributes, info.NumberOfLinks);
    }

    internal static bool IsRegularSingleLinkFile(SafeFileHandle handle) =>
        GetInformation(handle) is { Links: 1 } info
        && (info.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0
        && GetFileType(handle) == 1;

    internal static bool HasPath(SafeFileHandle handle, string expected)
    {
        var buffer = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity)
        {
            return false;
        }
        string value = buffer.ToString();
        return value.StartsWith(@"\\?\", StringComparison.Ordinal)
            && string.Equals(value[4..].TrimEnd('\\'), expected.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string path, uint access, uint share, IntPtr security, uint disposition,
        uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);

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
