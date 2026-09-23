namespace Aspose.Cli.Host;

/// <summary>
/// Locates the inbox Windows PowerShell by absolute path. A bare "powershell.exe" would let
/// process creation run a same-named file from the current directory.
/// </summary>
internal static class WindowsPowerShell
{
    /// <summary>Returns the verified system executable, or null when it is unavailable.</summary>
    internal static string? TryResolve()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string systemDirectory = Path.GetFullPath(Environment.SystemDirectory);
        string executable = Path.GetFullPath(Path.Combine(
            systemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"));
        return executable.StartsWith(systemDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && File.Exists(executable)
            && (File.GetAttributes(executable) & FileAttributes.ReparsePoint) == 0
                ? executable
                : null;
    }
}
