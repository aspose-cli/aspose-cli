using System.Diagnostics;

namespace Aspose.Cli.Host.App;

internal sealed record FilePickerResult(bool Available, string? Path);

/// <summary>Best-effort native document picker with a browser-upload fallback.</summary>
internal static class SystemFilePicker
{
    private static readonly TimeSpan PickerTimeout = TimeSpan.FromMinutes(3);

    public static FilePickerResult PickFile(
        IReadOnlyList<string> supportedExtensions)
    {
        ArgumentNullException.ThrowIfNull(supportedExtensions);
        string semicolonPatterns = string.Join(
            ';', supportedExtensions.Select(static extension => "*" + extension));
        string spacePatterns = semicolonPatterns.Replace(';', ' ');
        if (OperatingSystem.IsWindows())
        {
            string script =
                "Add-Type -AssemblyName System.Windows.Forms; "
                + "$d=[System.Windows.Forms.OpenFileDialog]::new(); "
                + "$d.Title='Open a document'; "
                + $"$d.Filter='Supported files|{semicolonPatterns}|All files|*.*'; "
                + "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Out.Write($d.FileName)}";
            return Run(
                "powershell.exe", ["-NoProfile", "-STA", "-Command", script], available: true);
        }

        if (OperatingSystem.IsMacOS())
        {
            const string script = "POSIX path of (choose file with prompt \"Open a document\")";
            return Run(
                "/usr/bin/osascript", ["-e", script], available: File.Exists("/usr/bin/osascript"));
        }

        string? zenity = FindOnPath("zenity");
        if (zenity is not null)
        {
            return Run(
                zenity,
                ["--file-selection", "--title=Open a document", $"--file-filter=Supported files | {spacePatterns}"],
                available: true);
        }

        string? kdialog = FindOnPath("kdialog");
        return kdialog is null
            ? new FilePickerResult(false, null)
            : Run(
                kdialog,
                [
                    "--getopenfilename",
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    $"Supported files ({spacePatterns})",
                ],
                available: true);
    }

    private static FilePickerResult Run(
        string executable,
        IReadOnlyList<string> arguments,
        bool available)
    {
        if (!available)
        {
            return new FilePickerResult(false, null);
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(info);
            if (process is null)
            {
                return new FilePickerResult(false, null);
            }

            if (!process.WaitForExit((int)PickerTimeout.TotalMilliseconds))
            {
                TryKill(process);
                return new FilePickerResult(true, null);
            }

            string path = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 && path.Length > 0
                ? new FilePickerResult(true, path)
                : new FilePickerResult(true, null);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new FilePickerResult(false, null);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static string? FindOnPath(string executable)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(
            Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(directory, executable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException)
            {
            }
        }

        return null;
    }
}
