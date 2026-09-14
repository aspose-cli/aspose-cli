using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsOutputTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterPageConflictKeepsEarlierOutputsUnpublished(bool supervised)
    {
        using var fixture = new WordsFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateTwoSectionDocument();
        string output = workspace.File("page.png");
        File.WriteAllText(workspace.File("page.p2.png"), "existing second page");
        CliResult result = workspace.Run(["words", "render", input, "--pages", "1-2", "--out", output, "--dpi", "36", "--output", "json",
            .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
        Assert.Equal(5, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.False(File.Exists(workspace.File("page.p1.png")));
        Assert.Equal("existing second page", File.ReadAllText(workspace.File("page.p2.png")));
    }
}
