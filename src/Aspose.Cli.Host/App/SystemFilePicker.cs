using System.Diagnostics;
using Aspose.Cli.Host.LocalServices;

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
        string patterns = string.Join(
            ';', supportedExtensions.Select(static extension => "*" + extension));
        string? powerShell = WindowsPowerShell.TryResolve();
        string script =
            "Add-Type -AssemblyName System.Windows.Forms; "
            + "$d=[System.Windows.Forms.OpenFileDialog]::new(); "
            + "$d.Title='Open a document'; "
            + $"$d.Filter='Supported files|{patterns}|All files|*.*'; "
            + "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Out.Write($d.FileName)}";
        return Run(
            powerShell ?? string.Empty, ["-NoProfile", "-STA", "-Command", script], available: powerShell is not null);
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

            using OwnedProcess picker = OwnedProcess.Start(info, killOnClose: true);
            Process process = picker.Process;
            // Both pipes are drained, so a chatty picker never blocks on a full buffer.
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)PickerTimeout.TotalMilliseconds))
            {
                picker.Stop();
                return new FilePickerResult(true, null);
            }

            string path = output.GetAwaiter().GetResult().Trim();
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
}
