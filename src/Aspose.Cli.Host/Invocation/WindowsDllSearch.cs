using System.Runtime.InteropServices;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Takes the current directory out of the process's DLL search path. The native engine
/// libraries load from the installation directory, but what they depend on, such as the Visual
/// C++ runtime, is searched for by name; without this, a machine that lacks one would load a
/// file of that name from whatever directory the CLI was started in.
/// </summary>
internal static class WindowsDllSearch
{
    /// <summary>Best effort, once at process start, before any native library loads.</summary>
    internal static void ExcludeCurrentDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            // An empty string, unlike null, removes the current directory from the search.
            _ = SetDllDirectoryW(string.Empty);
        }
        catch (Exception exception) when (
            exception is DllNotFoundException
                or EntryPointNotFoundException)
        {
            // Every supported Windows version exports it; the guard keeps an unexpected host
            // from failing the command start.
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectoryW(string lpPathName);
}
