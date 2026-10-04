using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The TEXT_TABLE_LAYOUT detector over row shapes, and <c>cells inspect</c> and
/// <c>cells convert</c> on CSV inputs through the built CLI in evaluation mode.
/// </summary>
public sealed class TextTableLayoutTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void Detect_FindsTheHeaderAfterATitleAndAQueryConditionRow()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((1, "销售明细报表"), (1, "查询条件: 2026-01"), (4, "日期"), (4, "北京"), (4, "上海")), []));

        Assert.Equal(TextTableFindingKind.Preamble, finding.Kind);
        Assert.Equal(2, finding.HeaderRow);
        Assert.Equal([0, 1], finding.Rows);
    }

    [Fact]
    public void Detect_ListsEmptyRowsInsideTheTableButNotAfterIt()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((3, "Region"), (3, "North"), (0, null), (3, "South"), (0, null), (3, "East")), []));

        Assert.Equal(TextTableFindingKind.BlankRows, finding.Kind);
        Assert.Equal([2, 4], finding.Rows);
        Assert.Equal(5, finding.LastRow);
    }

    [Theory]
    [InlineData("合计")]
    [InlineData("总计：")]
    [InlineData("小计")]
    [InlineData("总和")]
    [InlineData("Total")]
    [InlineData(" GRAND TOTAL: ")]
    [InlineData("Subtotal")]
    [InlineData("sum")]
    public void Detect_FindsATrailingTotalRowByItsLabel(string label)
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((3, "Region"), (3, "North"), (3, "South"), (2, label)), []));

        Assert.Equal(TextTableFindingKind.TotalRows, finding.Kind);
        Assert.Equal([3], finding.Rows);
        Assert.Equal([label], finding.Labels);
    }

    [Fact]
    public void Detect_FindsATotalRowInTheTailOfALongSheet()
    {
        TextRowShape[] head = [.. Enumerable.Range(0, 5).Select(static row => new TextRowShape(row, 3, row == 0 ? "Region" : "North"))];
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            head, [new TextRowShape(20_000, 3, "South"), new TextRowShape(20_001, 2, "Grand Total")]));

        Assert.Equal(TextTableFindingKind.TotalRows, finding.Kind);
        Assert.Equal([20_001], finding.Rows);
    }

    [Fact]
    public void Describe_GivesNoSumExampleForATotalRowWithoutNumbers()
    {
        Warning warning = Assert.Single(TextTableLayout.Describe(
            TextTableLayout.Detect(Shapes((3, "Region"), (3, "North"), (3, "South"), (1, "Total")), []), "Data", 2, capped: false));

        Assert.StartsWith("End data ranges at row 3, and leave", warning.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_ReportsNothingForAPlainTable()
    {
        Assert.Empty(TextTableLayout.Detect(
            Shapes((4, "Region"), (4, "North"), (3, "South"), (4, "Total Solutions Ltd"), (4, "East")), []));
    }

    [Fact]
    public void Detect_TakesAHeaderNarrowerThanItsDataRowsForTheHeader()
    {
        Assert.Empty(TextTableLayout.Detect(Shapes((2, "Name"), (5, "Ann"), (5, "Bob")), []));
    }

    [Fact]
    public void Detect_StartsTheTableAtANarrowHeaderAfterATitle()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((1, "Staff"), (2, "Name"), (5, "Ann"), (5, "Bob")), []));

        Assert.Equal(TextTableFindingKind.Preamble, finding.Kind);
        Assert.Equal(1, finding.HeaderRow);
        Assert.Equal([0], finding.Rows);
    }

    [Fact]
    public void Detect_ReportsNoPreambleForASingleColumn()
    {
        Assert.Empty(TextTableLayout.Detect(Shapes((1, "Name"), (1, "Ann"), (1, "Bob")), []));
    }

    [Fact]
    public void Inspect_WarnsAboutThePreambleTheEmptyRowAndTheTotalRow()
    {
        File.WriteAllText(_workspace.File("erp.csv"),
            "销售明细报表\n查询条件: 2026-01-01 至 2026-03-31\n日期,地区,产品,金额\n2026-01-05,北京,甲,100\n2026-01-06,上海,乙,200\n\n2026-02-01,广州,甲,300\n合计,,,600\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonNode[] warnings = LayoutWarnings(_workspace.Run("cells", "inspect", "erp.csv", "--output", "json").Json());

        Assert.Equal(3, warnings.Length);
        Assert.Contains("header is probably row 3; rows 1-2", Text(warnings[0], "message"), StringComparison.Ordinal);
        Assert.Contains("'cells query range <file> --sheet \"erp\" --range A3:D3'", Text(warnings[0], "hint"), StringComparison.Ordinal);
        Assert.Equal("1:2", Text(warnings[0], "location"));
        Assert.Contains("Row 6 is empty", Text(warnings[1], "message"), StringComparison.Ordinal);
        Assert.Equal("6:6", Text(warnings[1], "location"));
        Assert.Contains("Row 8 is a total row ('合计')", Text(warnings[2], "message"), StringComparison.Ordinal);
        // The example sums the column the total row holds a number in, not a text column.
        Assert.Contains("End data ranges at row 7, for example =SUM(D4:D7) for column D", Text(warnings[2], "hint"), StringComparison.Ordinal);
        Assert.Equal("8:8", Text(warnings[2], "location"));

        JsonNode converted = _workspace.Run(
            "cells", "convert", "erp.csv", "--to", "xlsx", "--out", "erp.xlsx", "--output", "json").Json();
        Assert.Equal(3, LayoutWarnings(converted).Length);
        // The hinted command works as written on the converted workbook too.
        JsonNode header = _workspace.Run(
            "cells", "query", "range", "erp.xlsx", "--sheet", "erp", "--range", "A3:D3", "--output", "json").Json();
        Assert.Equal("日期", header["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
    }

    [Fact]
    public void Inspect_ReportsNoLayoutWarningForAPlainCsv()
    {
        File.WriteAllText(_workspace.File("clean.csv"), "Region,Sales\nIT,5\nES,7\n");

        Assert.Empty(LayoutWarnings(_workspace.Run("cells", "inspect", "clean.csv", "--output", "json").Json()));
    }

    [Fact]
    public void Inspect_ReportsNoPreambleForAHeaderNarrowerThanItsDataRows()
    {
        File.WriteAllText(_workspace.File("unnamed.csv"), "Name,Amount\nAnn,5,a,b,c\nBob,7,d,e,f\n");

        Assert.Empty(LayoutWarnings(_workspace.Run("cells", "inspect", "unnamed.csv", "--output", "json").Json()));
    }

    public void Dispose() => _workspace.Dispose();

    private static TextRowShape[] Shapes(params (int Filled, string? FirstText)[] rows) =>
        [.. rows.Select(static (row, index) => new TextRowShape(index, row.Filled, row.FirstText))];

    private static JsonNode[] LayoutWarnings(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? [])
            .Where(static warning => Text(warning!, "code") == "TEXT_TABLE_LAYOUT")
            .Select(static warning => warning!)];

    private static string Text(JsonNode node, string name) => node[name]!.GetValue<string>();
}
