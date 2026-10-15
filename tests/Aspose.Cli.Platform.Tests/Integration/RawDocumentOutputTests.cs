using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>A raw JSON Schema prints on one line with <c>--output-format compact</c>, as an envelope does.</summary>
public sealed class RawDocumentOutputTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Schema_WithCompactOutput_PrintsTheSameDocumentOnOneLine()
    {
        CliResult indented = _workspace.Run("schema", "v2/common/error", "--output-format", "json");
        CliResult compact = _workspace.Run("schema", "v2/common/error", "--output-format", "compact");

        Assert.True(compact.ExitCode == 0, compact.StdErr);
        Assert.Single(compact.StdOut.TrimEnd().ReplaceLineEndings("\n").Split('\n'));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(indented.StdOut), JsonNode.Parse(compact.StdOut)));
        Assert.True(indented.StdOut.TrimEnd().ReplaceLineEndings("\n").Split('\n').Length > 1);
    }
}
