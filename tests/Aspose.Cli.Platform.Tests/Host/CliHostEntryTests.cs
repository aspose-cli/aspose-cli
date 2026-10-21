using System.Runtime.InteropServices;
using Aspose.Cli.Host;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>Runs alone: one case changes the process working directory.</summary>
[CollectionDefinition("Process working directory", DisableParallelization = true)]
public sealed class ProcessWorkingDirectoryCollection;

[Collection("Process working directory")]
public sealed class CliHostEntryTests
{
    [Fact]
    public void InteractiveDesktopWithoutArguments_OpensApp()
    {
        Assert.Equal(
            ["app", "open"],
            Normalize([], interactiveDesktop: true));
    }

    [Fact]
    public void InteractiveDesktopWithExistingFile_OpensFileInApp()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.Equal(
                ["app", "open", path],
                Normalize([path], interactiveDesktop: true));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RedirectedInvocation_DoesNotRewriteExistingFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            string[] args = [path];
            Assert.Same(
                args,
                Normalize(args, interactiveDesktop: false));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("missing-file.xlsx")]
    [InlineData("app")]
    public void InteractiveDesktop_DoesNotRewriteNonFiles(string argument)
    {
        string[] args = [argument];
        Assert.Same(
            args,
            Normalize(args, interactiveDesktop: true));
    }

    [Theory]
    [InlineData("doctor")]
    [InlineData("capabilities")]
    [InlineData("--version")]
    public void InteractiveDesktop_KeepsACommandThatIsAlsoAnExistingFile(string command)
    {
        using var temp = new TempDirectory();
        string previous = Directory.GetCurrentDirectory();
        File.WriteAllText(temp.File(command), "not a document");
        Directory.SetCurrentDirectory(temp.Path);
        try
        {
            string[] args = [command];
            Assert.Same(args, Normalize(args, interactiveDesktop: true));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    /// <summary>
    /// A run takes the current directory out of the DLL search path before anything else, so a
    /// DLL planted where the CLI is started is never loaded in place of one the system lacks.
    /// </summary>
    [Fact]
    public void Run_TakesTheCurrentDirectoryOutOfTheDllSearchPath()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        // Two copies of a system DLL under names found nowhere else, so only the current
        // directory can supply them.
        string before = "aspose-cwd-probe-" + Guid.NewGuid().ToString("N") + ".dll";
        string after = "aspose-cwd-probe-" + Guid.NewGuid().ToString("N") + ".dll";
        foreach (string name in new[] { before, after })
        {
            File.Copy(Path.Combine(Environment.SystemDirectory, "version.dll"), temp.File(name));
        }
        string previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(temp.Path);
        TextWriter error = Console.Error;
        try
        {
            // The default search, as a process starts with it, reaches the current directory.
            Assert.True(SetDllDirectoryW(null));
            Assert.True(Loads(before), "The probe does not reach the current directory.");

            // The catalog factory fails, so the run ends right after its process set-up.
            Console.SetError(TextWriter.Null);
            CliHost.Run([], static () => throw new InvalidOperationException("probe"));

            Assert.False(Loads(after), "The run left the current directory in the DLL search path.");
        }
        finally
        {
            Console.SetError(error);
            Directory.SetCurrentDirectory(previous);
        }

        static bool Loads(string name)
        {
            IntPtr module = LoadLibraryW(name);
            if (module == IntPtr.Zero) { return false; }
            FreeLibrary(module);
            return true;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectoryW(string? lpPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hLibModule);

    private static string[] Normalize(string[] args, bool interactiveDesktop) =>
        CliHost.NormalizeInteractiveArguments(args, interactiveDesktop, ActualCommandTree.Parser);
}
