using Aspose.Cli.Host;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class CliHostEntryTests
{
    [Fact]
    public void InteractiveDesktopWithoutArguments_OpensApp()
    {
        Assert.Equal(
            ["app"],
            CliHost.NormalizeInteractiveArguments([], interactiveDesktop: true));
    }

    [Fact]
    public void InteractiveDesktopWithExistingFile_OpensFileInApp()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.Equal(
                ["app", path],
                CliHost.NormalizeInteractiveArguments([path], interactiveDesktop: true));
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
                CliHost.NormalizeInteractiveArguments(args, interactiveDesktop: false));
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
            CliHost.NormalizeInteractiveArguments(args, interactiveDesktop: true));
    }
}
