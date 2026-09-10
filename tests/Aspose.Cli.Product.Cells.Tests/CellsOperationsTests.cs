using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cli.Generated;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Representative persisted workbook operations against the real engine.</summary>
public sealed class CellsOperationsTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsOperationsTests(CellsFixture fixture) => _fixture = fixture;

    private EditResult Apply(string path, string operations, string output) =>
        _fixture.Engine.ApplyOps(
            path,
            OpsParser.Parse(operations),
            new EditRequest
            {
                OutputPath = _fixture.Temp.File(output),
                Overwrite = true,
            });

    [Theory]
    [InlineData("{\"unexpected\":true,\"ops\":[{\"op\":\"recalculate\"}]}")]
    [InlineData("{\"ops\":[{\"op\":\"recalculate\",\"unexpected\":true}]}")]
    [InlineData("{\"ops\":[{\"op\":\"set_values\",\"range\":\"A1\",\"values\":[[1]],\"values\":[[2]]}]}")]
    public void Parser_RejectsUnknownAndDuplicateFields(string json)
    {
        CliException error = Assert.Throws<CliException>(() => OpsParser.Parse(json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void Runner_ReportsSuccessfulAndFailedAttemptedTargets()
    {
        OpsBatch batch = OpsParser.Parse(
            "{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Data\",\"range\":\"A1\",\"values\":[[1]]},{\"op\":\"clear_range\",\"sheet\":\"Missing\",\"range\":\"A1:B1\"}]}");
        IReadOnlyList<BoundedOperationOutcome> outcomes = OpsBatchRunner.Run(
            batch,
            op => op.Sheet == "Missing"
                ? throw new EngineOpException("missing", new InvalidOperationException("missing"))
                : 1,
            continueOnError: true);

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], outcomes.Select(static item => item.Status));
        Assert.Equal(["Data!A1"], outcomes[0].Targets);
        Assert.Equal(["Missing!A1:B1"], outcomes[1].Targets);
    }

    [Fact]
    public void ConditionalFormats_AddAndClearPersist()
    {
        string source = _fixture.CreateSalesWorkbook("conditional.xlsx");
        EditResult result = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1", "values": [[10],[20],[30]] },
              { "op": "add_conditional_format", "sheet": "Data", "range": "A1:A3",
                "rule": { "kind": "cellValue", "operator": "greaterThan", "value1": "15" },
                "style": { "bg": "#FFC7CE" } },
              { "op": "add_conditional_format", "sheet": "Data", "range": "B1:B3",
                "rule": { "kind": "colorScale", "minColor": "#FFFFFF", "maxColor": "#FF0000" } }
            ] }
            """,
            "conditional.out.xlsx");
        Assert.Equal(["Data!A1"], result.Applied[0].Targets);
        Assert.Equal(["Data!A1:A3"], result.Applied[1].Targets);

        using (var reopened = new Workbook(result.Output!.Path))
        {
            Assert.Equal(2, reopened.Worksheets["Data"].ConditionalFormattings.Count);
        }

        string cleared = Apply(
            result.Output.Path,
            """{ "ops": [ { "op": "clear_conditional_formats", "sheet": "Data", "range": "A1:A3" } ] }""",
            "conditional.cleared.xlsx").Output!.Path;
        using var final = new Workbook(cleared);
        Assert.Single(final.Worksheets["Data"].ConditionalFormattings);
    }

    [Theory]
    [InlineData("""{ "op": "create_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:B3", "at": "A1", "values": [{ "field": "Ghost" }] }""")]
    [InlineData("""{ "op": "create_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:B3", "at": "A1", "rows": ["Ghost"], "values": [{ "field": "Amount" }] }""")]
    public void PivotWithUnknownField_IsAnActionableOperationsError(string pivotOperation)
    {
        string created = _fixture.Engine.CreateWorkbook(new NewWorkbookRequest
        {
            OutputPath = _fixture.Temp.File("pivot.xlsx"),
            SheetNames = ["Data", "Pivot"],
            Overwrite = true,
        }).Output.Path;
        string seeded = Apply(
            created,
            """{ "ops": [ { "op": "set_values", "sheet": "Data", "range": "A1", "values": [["Region","Amount"],["East",10],["West",20]] } ] }""",
            "pivot.seeded.xlsx").Output!.Path;

        CliException error = Assert.Throws<CliException>(() => Apply(
            seeded,
            $$"""{ "ops": [ {{pivotOperation}} ] }""",
            "pivot.invalid.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("Ghost", error.Message, StringComparison.Ordinal);
        Assert.Contains("Region", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BorderMutation_PreservesUnrelatedStyleAfterSaveAndReopen()
    {
        string source = _fixture.CreateSalesWorkbook("borders.xlsx");
        string styled = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A5", "values": [[1234.5,99],[7,8]] },
              { "op": "format_range", "sheet": "Data", "range": "A5:B6",
                "style": { "bg": "#FFF2CC", "font": "Verdana", "size": 12, "bold": true, "numberFormat": "0.00" } }
            ] }
            """,
            "borders.styled.xlsx").Output!.Path;
        string bordered = Apply(
            styled,
            """{ "ops": [ { "op": "set_borders", "sheet": "Data", "range": "A5:B6", "edges": ["all"] } ] }""",
            "borders.out.xlsx").Output!.Path;

        using var workbook = new Workbook(bordered);
        Style style = workbook.Worksheets["Data"].Cells["A5"].GetStyle();
        Assert.Equal("Verdana", style.Font.Name);
        Assert.True(style.Font.IsBold);
        Assert.Equal("0.00", style.Custom);
        Assert.Equal(BackgroundType.Solid, style.Pattern);
        Assert.Equal(CellBorderType.Thin, style.Borders[BorderType.TopBorder].LineStyle);
        Assert.Equal(CellBorderType.Thin, style.Borders[BorderType.RightBorder].LineStyle);
    }

    [Fact]
    public void SheetProtection_UsesAnEnvironmentSecretWithoutSerializingIt()
    {
        const string secret = "test-sheet-secret";
        Environment.SetEnvironmentVariable("ASPOSE_CLI_TEST_SHEET_PASSWORD", secret);
        try
        {
            string source = _fixture.CreateSalesWorkbook("protected.xlsx");
            EditResult result = Apply(
                source,
                """{ "ops": [ { "op": "protect_sheet", "sheet": "Data", "passwordEnv": "ASPOSE_CLI_TEST_SHEET_PASSWORD", "allow": ["sort"] } ] }""",
                "protected.out.xlsx");

            using var workbook = new Workbook(result.Output!.Path);
            Assert.True(workbook.Worksheets["Data"].Protection.AllowSorting);
            Assert.DoesNotContain(
                secret,
                ProductJsonContext.Definition.Serialize(result),
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPOSE_CLI_TEST_SHEET_PASSWORD", null);
        }
    }

    [Fact]
    public void ChartCosmetics_PersistThroughSaveAndReopen()
    {
        string source = _fixture.CreateSalesWorkbook("chart.xlsx");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "create_chart", "sheet": "Data", "type": "column", "dataRange": "A1:C3", "at": "E2:L18",
                "title": "Quarterly", "legend": { "visible": true, "position": "right" },
                "axisTitles": { "category": "Region", "value": "Sales (USD)" },
                "seriesColors": ["#C00000", "#00B050"],
                "dataLabels": { "visible": true, "format": "#,##0" } }
            ] }
            """,
            "chart.out.xlsx").Output!.Path;

        using var workbook = new Workbook(output);
        Chart chart = workbook.Worksheets["Data"].Charts[0];
        Assert.Equal(LegendPositionType.Right, chart.Legend.Position);
        Assert.Equal("Region", chart.CategoryAxis.Title.Text);
        Assert.Equal("Sales (USD)", chart.ValueAxis.Title.Text);
        Assert.Equal((0xC0, 0x00, 0x00),
            (chart.NSeries[0].Area.ForegroundColor.R,
             chart.NSeries[0].Area.ForegroundColor.G,
             chart.NSeries[0].Area.ForegroundColor.B));
        Assert.Equal("#,##0", chart.NSeries[0].DataLabels.NumberFormat);
    }

    [Fact]
    public void FormulaConditionalFormat_PreservesTheCellsNumberFormat()
    {
        string source = _fixture.CreateSalesWorkbook("formula-format.xlsx");
        EditResult result = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Invoice","Amount","Status"],["INV-1",120.5,"OVERDUE"],["INV-2",80,"PAID"]] },
              { "op": "format_range", "sheet": "Data", "range": "B2:B3", "style": { "numberFormat": "#,##0.00" } },
              { "op": "add_conditional_format", "sheet": "Data", "range": "A2:C3",
                "rule": { "kind": "formula", "value1": "$C2=\"OVERDUE\"" },
                "style": { "bg": "#FFC7CE" } }
            ] }
            """,
            "formula-format.out.xlsx");

        using (var workbook = new Workbook(result.Output!.Path))
        {
            FormatCondition condition = workbook.Worksheets["Data"].ConditionalFormattings[0][0];
            Assert.Equal("=$C2=\"OVERDUE\"", condition.Formula1);
            Assert.Equal("=$C3=\"OVERDUE\"", condition.GetFormula1(2, 0));
        }

        WorkbookReadResult read = _fixture.Engine.Read(
            result.Output.Path,
            new ReadRequest
            {
                SheetName = "Data",
                Range = global::Aspose.Cli.Product.Cells.Addressing.A1.ParseRange("B2:B2").Range,
                Scope = ReadScope.Full,
                MaxCells = 10,
            });
        string styleId = Assert.IsType<string>(read.Sheet.Cells![0][0].StyleId);
        Assert.Equal("#,##0.00", read.Styles![styleId].NumberFormat);
    }

    [Fact]
    public void Sparkline_FansOutOneSeriesPerDataRow()
    {
        string source = _fixture.CreateSalesWorkbook("sparkline.xlsx");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Second", "range": "B2",
                "values": [[1,-2,3,4],[5,6,-7,8],[9,10,11,-12]] },
              { "op": "add_sparkline", "sheet": "Second", "dataRange": "B2:E4", "location": "F2:F4",
                "type": "winloss", "color": "#1F4E79" }
            ] }
            """,
            "sparkline.out.xlsx").Output!.Path;

        using var workbook = new Workbook(output);
        SparklineGroup group = workbook.Worksheets["Second"].SparklineGroups[0];
        Assert.Equal(SparklineType.Stacked, group.Type);
        Assert.Equal(3, group.Sparklines.Count);
        Assert.Equal("Second!B2:E2", group.Sparklines[0].DataRange);
        Assert.Equal("Second!B4:E4", group.Sparklines[2].DataRange);
    }
}
