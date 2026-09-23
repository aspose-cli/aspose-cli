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
            ["app"],
            Normalize([], interactiveDesktop: true));
    }

    [Fact]
    public void InteractiveDesktopWithExistingFile_OpensFileInApp()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.Equal(
                ["app", path],
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

    private static string[] Normalize(string[] args, bool interactiveDesktop) =>
        CliHost.NormalizeInteractiveArguments(args, interactiveDesktop, ActualCommandTree.Parser);
}
