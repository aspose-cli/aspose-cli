using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// <c>cells edit --bom</c> starts a csv or tsv output with a UTF-8 byte order mark and is refused
/// on any other output, as on <c>cells convert</c>. The commands run with the test license when
/// the run has one, and in evaluation mode otherwise.
/// </summary>
public sealed class CellsEditByteOrderMarkTests : IDisposable
{
    private static readonly byte[] Marked = [0xEF, 0xBB, 0xBF, 0xE4, 0xBA, 0xA4];

    private static readonly byte[] Unmarked = [0xE4, 0xBA, 0xA4];

    private readonly TempWorkspace _workspace = new();

    public CellsEditByteOrderMarkTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>A text output of an edit starts with the mark and then the first cell's text.</summary>
    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    public void Edit_WithBom_StartsTheTextOutputWithTheMark(string format)
    {
        Book();

        JsonNode edited = Run("cells", "edit", "book.xlsx", "--set", "Data!B1=2", "--out", $"out.{format}", "--bom");

        Assert.Equal(format, edited["output"]!["format"]!.GetValue<string>());
        Assert.Equal(Marked, Start($"out.{format}", Marked.Length));
    }

    /// <summary>An edit in place of a csv takes the mark too, and a read does not take it for text of the first cell.</summary>
    [Fact]
    public void Edit_WithBom_InPlaceOfACsv_AddsTheMarkAReadLeavesOut()
    {
        File.WriteAllText(_workspace.File("table.csv"), "交易,1\n", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Run("cells", "edit", "table.csv", "--in-place", "--set", "table!B1=2", "--bom");

        Assert.Equal(Marked, Start("table.csv", Marked.Length));
        JsonNode read = Run("cells", "read", "table.csv", "--range", "A1");
        Assert.Equal("交易", read["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
    }

    /// <summary>A verified edit reads the output back past the mark: the one change is the one the edit made.</summary>
    [Fact]
    public void VerifiedEdit_WithBom_ComparesTheCellsPastTheMark()
    {
        Book();

        JsonNode edited = Run("cells", "edit", "book.xlsx", "--set", "Data!B1=2", "--out", "out.csv", "--bom", "--verify");

        JsonNode verification = edited["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.Equal("B1", Assert.Single(verification["directChanges"]!.AsArray())!["cell"]!.GetValue<string>());
        Assert.DoesNotContain('﻿', edited.ToJsonString());
    }

    /// <summary>On a workbook output the edit refuses the option in the words of the convert, before it opens anything.</summary>
    [Theory]
    [InlineData("--out", "copy.xlsx")]
    [InlineData("--in-place")]
    [InlineData("--dry-run", "--in-place")]
    public void Edit_WithBom_RefusesAnOutputThatIsNotText(params string[] target)
    {
        Book();

        CliResult edit = _workspace.Run(["cells", "edit", "book.xlsx", "--set", "Data!B1=2", .. target, "--bom", "--output-format", "json"]);
        CliResult convert = _workspace.Run("cells", "convert", "book.xlsx", "--to", "xlsx", "--out", "converted.xlsx", "--bom", "--output-format", "json");

        Assert.Equal(2, edit.ExitCode);
        JsonNode refused = JsonNode.Parse(edit.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", refused["code"]!.GetValue<string>());
        Assert.Equal("--bom", refused["details"]!["option"]!.GetValue<string>());
        JsonNode convertRefused = JsonNode.Parse(convert.StdErr)!["error"]!;
        Assert.Equal(convertRefused["message"]!.GetValue<string>(), refused["message"]!.GetValue<string>());
        Assert.Equal(convertRefused["hint"]!.GetValue<string>(), refused["hint"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("copy.xlsx")));
    }

    /// <summary>Without the option a text output has no mark and is what the convert of the same sheet writes.</summary>
    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    public void Edit_WithoutBom_WritesTheTextAsBefore(string format)
    {
        Book();

        Run("cells", "edit", "book.xlsx", "--set", "Data!A1=交易", "--out", $"edited.{format}");
        Run("cells", "convert", "book.xlsx", "--to", format, "--out", $"converted.{format}");

        Assert.Equal(Unmarked, Start($"edited.{format}", Unmarked.Length));
        Assert.Equal(File.ReadAllBytes(_workspace.File($"converted.{format}")), File.ReadAllBytes(_workspace.File($"edited.{format}")));
    }

    private void Book()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "Data").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--set", "Data!A1=交易").ExitCode);
    }

    private byte[] Start(string file, int length) => File.ReadAllBytes(_workspace.File(file))[..length];

    private JsonNode Run(params string[] args)
    {
        CliResult result = _workspace.Run([.. args, "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.StdErr}{result.StdOut}");
        return JsonNode.Parse(result.StdOut)!;
    }
}
