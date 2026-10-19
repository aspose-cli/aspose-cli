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
        if (!SetHandleInformation(handle, 1, 0)) { throw new Win32Exception(Marshal.GetLastPInvokeError()); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);
}
