using System.Diagnostics;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Opens a local service page in the person's browser. It stays quiet where
/// nobody is watching — a script, a CI job, or a session that asked not to be
/// interrupted — and a browser that refuses to start is a notice, never a
/// failure of the command that produced the page.
/// </summary>
internal static class BrowserLauncher
{
    public static void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!IsInteractive())
        {
            return;
        }
        try
        {
            using Process? browser = OperatingSystem.IsWindows()
                ? Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })
                : Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", url);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"preview: open {url} manually ({exception.GetType().Name}).");
        }
    }

    private static bool IsInteractive() =>
        Environment.GetEnvironmentVariable("ASPOSE_CLI_NO_OPEN") != "1"
        && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"))
        && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
        && Environment.UserInteractive;
}
