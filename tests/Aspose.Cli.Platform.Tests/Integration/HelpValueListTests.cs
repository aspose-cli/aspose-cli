using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Help lists an option's values once: a description that names each of them gets no second list.</summary>
public sealed class HelpValueListTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("cells convert", "--to <to>", false)]
    [InlineData("words search", "--scope <scope>", false)]
    [InlineData("pdf convert", "--to <to>", true)]
    public void AnOptionsValues_AreListedOnce(string command, string label, bool listed)
    {
        CliResult help = _workspace.Run([.. command.Split(' '), "--help"]);

        Assert.True(help.ExitCode == 0, help.StdErr);
        string line = help.StdOut.ReplaceLineEndings("\n").Split('\n').Single(line => line.TrimStart().StartsWith(label, StringComparison.Ordinal));
        Assert.Equal(listed, line.Contains("Values: ", StringComparison.Ordinal));
    }
}
