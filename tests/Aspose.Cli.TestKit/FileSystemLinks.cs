using System.Diagnostics;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Creates the reparse points that path-safety tests must see refused.</summary>
public static class FileSystemLinks
{
    private const int PrivilegeNotHeld = unchecked((int)0x80070522);

    /// <summary>
    /// Links a directory to <paramref name="target"/>, which need not exist: a junction on
    /// Windows, which any account may create, and a symbolic link elsewhere.
    /// </summary>
    public static void CreateDirectoryLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in new[] { "/d", "/c", "mklink", "/J", link, target })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("cmd.exe could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        string error = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000), "Creating the junction timed out.");
        Assert.True(process.ExitCode == 0, $"mklink /J failed: {error}{output.Result}");
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, $"{link} is not a junction.");
    }

    /// <summary>
    /// Creates a file symbolic link, or skips the test where this account may not:
    /// Windows grants the privilege only in Developer Mode or to an elevated process.
    /// </summary>
    public static void CreateFileSymbolicLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (IOException exception) when (exception.HResult == PrivilegeNotHeld)
        {
            Assert.Skip("Creating a file symbolic link requires Windows Developer Mode or elevation.");
        }
    }
}
