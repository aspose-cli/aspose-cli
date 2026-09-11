using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Received private pipe handles must not escape into later descendants.</summary>
internal static class ProcessPipeHandles
{
    internal static void PreventInheritance(PipeStream pipe)
    {
        nint handle = pipe.SafePipeHandle.DangerousGetHandle();
        if (OperatingSystem.IsWindows())
        {
            if (!SetHandleInformation(handle, 1, 0)) { throw new Win32Exception(Marshal.GetLastPInvokeError()); }
        }
        else
        {
            int flags = Fcntl((int)handle, 1, 0);
            if (flags < 0 || Fcntl((int)handle, 2, flags | 1) < 0)
            {
                throw new IOException("The private pipe could not be confined to this process.");
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int Fcntl(int descriptor, int command, int value);
}
