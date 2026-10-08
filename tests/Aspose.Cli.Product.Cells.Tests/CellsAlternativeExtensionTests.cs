using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Delimited text is often named <c>.txt</c>: a csv or tsv output under that extension is written
/// in the format <c>--to</c> names, and a <c>.txt</c> input still imports as delimited text.
/// </summary>
public sealed class CellsAlternativeExtensionTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("csv", "alpha,3")]
    [InlineData("tsv", "alpha\t3")]
    public void Convert_ToDelimitedTextUnderATxtExtension_WritesTheNamedFormat(string format, string row)
    {
        File.WriteAllText(_workspace.File("rows.csv"), "name,qty\nalpha,3\n");

        CliResult converted = _workspace.Run("cells", "convert", "rows.csv", "--to", format, "--out", "data.txt", "--output", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode output = JsonNode.Parse(converted.StdOut)!["output"]!;
        Assert.Equal(format, output["format"]!.GetValue<string>());
        Assert.Equal(_workspace.File("data.txt"), output["path"]!.GetValue<string>());
        Assert.Contains(row, File.ReadAllLines(_workspace.File("data.txt")));
    }

    [Fact]
    public void Convert_OfATxtInput_ImportsItAsDelimitedText()
    {
        File.WriteAllText(_workspace.File("rows.txt"), "name;qty\nalpha;3\n");

        CliResult converted = _workspace.Run("cells", "convert", "rows.txt", "--to", "csv", "--out", "rows.csv", "--output", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Contains("alpha,3", File.ReadAllLines(_workspace.File("rows.csv")));
    }
}
