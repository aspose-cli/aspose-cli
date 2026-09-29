using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// create_pivot captions through the built CLI. Every child process runs in evaluation mode,
/// so these cover the caption language and value labels where the licensed engine suite skips.
/// </summary>
public sealed class CellsPivotCaptionCliTests : IDisposable
{
    private const string ChineseData =
        """{"op":"set_values","sheet":"Data","range":"A1","values":[["地区","产品","不含税净额","数量"],["北京","甲",10.5,1],["上海","乙",20,2]]}""";

    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ChineseFields_GetExcelsChineseCaptionsThatARefreshKeeps()
    {
        CreateReport();
        Edit(ChineseData,
            """{"op":"create_pivot","sheet":"Pivot","sourceRange":"Data!A1:D3","at":"A1","rows":["地区"],"columns":["产品"],"values":[{"field":"不含税净额"},{"field":"数量","function":"count"}]}""");

        AssertChineseCaptions(Read("A1:E4"));

        Edit("""{"op":"set_values","sheet":"Data","range":"C2","values":[[30]]}""",
            """{"op":"refresh_pivot","sheet":"Pivot"}""");

        string[][] refreshed = Read("A1:E4");
        AssertChineseCaptions(refreshed);
        Assert.Equal("30", refreshed[2][2]);
    }

    [Fact]
    public void EnglishCaptions_StayEnglishForChineseFields()
    {
        CreateReport();
        Edit(ChineseData,
            """{"op":"create_pivot","sheet":"Pivot","sourceRange":"Data!A1:D3","at":"A1","rows":["地区"],"columns":["产品"],"values":[{"field":"不含税净额"},{"field":"数量","function":"count"}],"captions":"en"}""");

        string[][] cells = Read("A1:E4");
        Assert.Equal("Data", cells[1][1]);
        Assert.Equal("Grand Total", cells[1][4]);
        Assert.Equal("Sum of 不含税净额", cells[2][1]);
        Assert.Equal("Count of 数量", cells[3][1]);
    }

    [Fact]
    public void EnglishFields_KeepTheEngineCaptionsUnderAuto()
    {
        CreateReport();
        Edit("""{"op":"set_values","sheet":"Data","range":"A1","values":[["Region","Product","Amount","Units"],["North","A",10.5,1],["South","B",20,2]]}""",
            """{"op":"create_pivot","sheet":"Pivot","sourceRange":"Data!A1:D3","at":"A1","rows":["Region"],"columns":["Product"],"values":[{"field":"Amount"},{"field":"Units","function":"max"}]}""");

        string[][] cells = Read("A1:E4");
        Assert.Equal("Data", cells[1][1]);
        Assert.Equal("Grand Total", cells[1][4]);
        Assert.Equal("Sum of Amount", cells[2][1]);
        Assert.Equal("Max of Units", cells[3][1]);
    }

    [Fact]
    public void ALabel_WinsOverTheCaptionLanguage()
    {
        CreateReport();
        Edit(ChineseData,
            """{"op":"create_pivot","sheet":"Pivot","sourceRange":"Data!A1:D3","at":"A1","rows":["地区"],"columns":["产品"],"values":[{"field":"不含税净额","label":"净额合计"},{"field":"数量","function":"average","label":"Average units"},{"field":"不含税净额","function":"min"}]}""");

        string[][] cells = Read("A1:E5");
        Assert.Equal("总计", cells[1][4]);
        Assert.Equal("净额合计", cells[2][1]);
        Assert.Equal("Average units", cells[3][1]);
        Assert.Equal("最小值项:不含税净额", cells[4][1]);
    }

    [Theory]
    [InlineData("""[{"field":"数量","label":"地区"}]""", "auto", "a header of the source data")]
    [InlineData("""[{"field":"数量","label":"合计"},{"field":"不含税净额","label":"合计"}]""", "zh", "the label of values[0]")]
    [InlineData("""[{"field":"数量","label":"sum of 不含税净额"},{"field":"不含税净额"}]""", "en", "the caption of values[1]")]
    public void ALabelExcelWouldRefuse_IsRefused(string values, string captions, string owner)
    {
        CreateReport();
        Edit(ChineseData);

        CliResult refused = _workspace.Run(
            "cells", "edit", "report.xlsx", "--out", "report.out.xlsx", "--output", "json", "--ops",
            $$"""{"ops":[{"op":"create_pivot","sheet":"Pivot","sourceRange":"Data!A1:D3","at":"A1","rows":["地区"],"values":{{values}},"captions":"{{captions}}"}]}""");

        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(owner, error["details"]!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("report.out.xlsx")));
    }

    public void Dispose() => _workspace.Dispose();

    private static void AssertChineseCaptions(string[][] cells)
    {
        Assert.Equal("值", cells[1][1]);
        Assert.Equal("总计", cells[1][4]);
        Assert.Equal("求和项:不含税净额", cells[2][1]);
        Assert.Equal("计数项:数量", cells[3][1]);
    }

    private void CreateReport() =>
        _workspace.Run("cells", "create", "report.xlsx", "--sheets", "Data,Pivot").Succeeded();

    private void Edit(params string[] operations) =>
        _workspace.Run(
            "cells", "edit", "report.xlsx", "--in-place", "--output", "json", "--ops",
            $$"""{"ops":[{{string.Join(",", operations)}}]}""").Succeeded();

    /// <summary>The Pivot sheet's cells in the range as display strings, empty for an empty cell.</summary>
    private string[][] Read(string range) =>
        [.. _workspace.Run("cells", "query", "range", "report.xlsx", "--sheet", "Pivot", "--range", range, "--output", "json")
            .Json()["sheet"]!["cells"]!.AsArray()
            .Select(static row => row!.AsArray()
                .Select(static cell => cell!["v"] is { } value ? value.ToString() : string.Empty)
                .ToArray())];
}
