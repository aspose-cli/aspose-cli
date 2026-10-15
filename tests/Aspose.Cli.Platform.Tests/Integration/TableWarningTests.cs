using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Table output prints a warning's hint below it, as it prints an error's.</summary>
public sealed class TableWarningTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void AWarningWithAHint_PrintsTheHint()
    {
        Assert.Equal(0, _workspace.Run("words", "create", "a.docx", "--output-format", "json").ExitCode);

        CliResult result = _workspace.Run("words", "convert", "a.docx", "--to", "txt", "--out", "a.txt", "--output-format", "table");

        Assert.True(result.ExitCode == 0, result.StdErr);
        string[] lines = result.StdErr.ReplaceLineEndings("\n").Split('\n');
        int warning = Array.FindIndex(lines, static line => line.StartsWith("warning LOSSY_CONVERSION: ", StringComparison.Ordinal));
        Assert.True(warning >= 0, result.StdErr);
        Assert.StartsWith("  hint: ", lines[warning + 1], StringComparison.Ordinal);
    }
}
