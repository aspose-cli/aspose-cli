using System.Runtime.InteropServices;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Keeps the command-line process non-interactive when Windows or a native
/// dependency reports a fatal error. The caller still owns normal exception
/// reporting; this only prevents a GUI error dialog from replacing stderr and
/// the documented exit code.
/// </summary>
internal static class WindowsProcessErrorMode
{
    internal const uint SemFailCriticalErrors = 0x0001;
    internal const uint SemNoGpFaultErrorBox = 0x0002;
    private const uint SemNoOpenFileErrorBox = 0x8000;

    /// <summary>Adds the non-interactive flags without clearing existing mode bits.</summary>
    internal static uint AddNonInteractiveFlags(uint mode) =>
        mode | SemFailCriticalErrors | SemNoGpFaultErrorBox;

    /// <summary>Best-effort process-wide suppression of native Windows error UI.</summary>
    internal static void SuppressNativeErrorUi()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            uint current = GetErrorMode();
            _ = SetErrorMode(
                AddNonInteractiveFlags(current) | SemNoOpenFileErrorBox);
        }
        catch (Exception exception) when (
            exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException)
        {
            // The guard must never turn a command-start failure into a second
            // unhandled failure. Supported Windows versions export both APIs;
            // this fallback only applies to an unexpected host/runtime.
        }
    }

    internal static uint GetCurrentModeForTesting() =>
        OperatingSystem.IsWindows() ? GetErrorMode() : 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetErrorMode(uint mode);
}
