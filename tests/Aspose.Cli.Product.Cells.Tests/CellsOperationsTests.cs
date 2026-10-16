using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

    private CellsEditResult Apply(string path, string operations, string output,
        IReadOnlyDictionary<string, Secret>? secrets = null) =>
        CellsEdit.Run(_fixture.Session,
            new CellsEditRequest
            {
                Input = path,
                Batch = ParseOps(operations),
                Output = TestOutput.At(_fixture.Temp.File(output), overwrite: true),
                OpSecrets = secrets,
            });

    [Theory]
    [InlineData("{\"unexpected\":true,\"ops\":[{\"op\":\"clear_range\",\"range\":\"A1\"}]}")]
    [InlineData("{\"ops\":[{\"op\":\"clear_range\",\"range\":\"A1\",\"unexpected\":true}]}")]
    [InlineData("{\"ops\":[{\"op\":\"set_values\",\"range\":\"A1\",\"values\":[[1]],\"values\":[[2]]}]}")]
    public void Parser_RejectsUnknownAndDuplicateFields(string json)
    {
        CliException error = Assert.Throws<CliException>(() => ParseOps(json));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void BestEffort_ReportsSuccessfulAndFailedAttemptedTargets()
    {
        string source = _fixture.CreateSalesWorkbook("attempted.xlsx");
        CellsEditResult result = CellsEdit.Run(_fixture.Session,
            new CellsEditRequest
            {
                Input = source,
                Batch = ParseOps("{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Data\",\"range\":\"A1\",\"values\":[[1]]},{\"op\":\"clear_range\",\"sheet\":\"Missing\",\"range\":\"A1:B1\"}]}"),
                Output = TestOutput.At(_fixture.Temp.File("attempted.out.xlsx"), overwrite: true),
                Options = new EditCommandOptions { BestEffort = true },
            });

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], result.Applied.Select(static item => item.Status));
        Assert.Equal(["cell/Data!A1"], result.Applied[0].Targets);
        Assert.Equal(["cell/Missing!A1:B1"], result.Applied[1].Targets);
        Assert.Equal("SHEET_NOT_FOUND", result.Applied[1].Error!.Code);
    }

    [Fact]
    public void CellBudget_RejectsAWholeSheetFormulaBeforeWriting()
    {
        string source = _fixture.CreateSalesWorkbook("budget.xlsx");
        string output = _fixture.Temp.File("budget.out.xlsx");

        CliException error = Assert.Throws<CliException>(() => CellsEdit.Run(_fixture.Session,
            new CellsEditRequest { Input = source, Batch = ParseOps("{\"ops\":[{\"op\":\"set_formula\",\"range\":\"A1:XFD1048576\",\"formula\":\"=1\"}]}"), Output = TestOutput.At(output, overwrite: true) }));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ConditionalFormats_AddAndClearPersist()
    {
        string source = _fixture.CreateSalesWorkbook("conditional.xlsx");
        CellsEditResult result = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1", "values": [[10],[20],[30]] },
              { "op": "add_conditional_format", "sheet": "Data", "range": "A1:A3",
                "rule": { "kind": "cell-value", "operator": "greater-than", "value1": "15" },
                "matchStyle": { "fillColor": "#FFC7CE" } },
              { "op": "add_conditional_format", "sheet": "Data", "range": "B1:B3",
                "rule": { "kind": "color-scale", "minColor": "#FFFFFF", "maxColor": "#FF0000" } }
            ] }
            """,
            "conditional.out.xlsx");
        Assert.Equal(["cell/Data!A1", "cell/Data!A2", "cell/Data!A3"], result.Applied[0].Targets);
        Assert.Equal(["conditional-format/Data/1"], result.Applied[1].Targets);

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

    /// <summary>
    /// A cellValue rule matches text written as it is or quoted as an Excel string literal, as
    /// Excel's own dialog takes both.
    /// </summary>
    [Fact]
    public void CellValueConditionalFormat_MatchesPlainAndQuotedText()
    {
        string source = _fixture.CreateSalesWorkbook("conditional-text.xlsx");
        CellsEditResult result = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1", "values": [["关注", "关注", "a\"b"]] },
              { "op": "add_conditional_format", "sheet": "Data", "range": "A1",
                "rule": { "kind": "cell-value", "operator": "equal", "value1": "关注" }, "matchStyle": { "fillColor": "#FFEB9C" } },
              { "op": "add_conditional_format", "sheet": "Data", "range": "B1",
                "rule": { "kind": "cell-value", "operator": "equal", "value1": "\"关注\"" }, "matchStyle": { "fillColor": "#FFEB9C" } },
              { "op": "add_conditional_format", "sheet": "Data", "range": "C1",
                "rule": { "kind": "cell-value", "operator": "equal", "value1": "\"a\"\"b\"" }, "matchStyle": { "fillColor": "#FFEB9C" } }
            ] }
            """,
            "conditional-text.out.xlsx");

        using var workbook = new Workbook(result.Output!.Path);
        Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
        Assert.All(["A1", "B1", "C1"], name => Assert.NotNull(cells[name].GetConditionalFormattingResult()?.ConditionalStyle));
    }

    [Theory]
    [InlineData("""{ "op": "insert_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:B3", "at": "A1", "values": [{ "field": "Ghost" }] }""")]
    [InlineData("""{ "op": "insert_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:B3", "at": "A1", "rows": ["Ghost"], "values": [{ "field": "Amount" }] }""")]
    public void PivotWithUnknownField_IsAnActionableOperationsError(string pivotOperation)
    {
        string created = CellsCreate.Run(_fixture.Session, new CellsCreateRequest
        {
            Output = TestOutput.At(_fixture.Temp.File("pivot.xlsx"), overwrite: true),
            SheetNames = ["Data", "Pivot"],
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
                "style": { "fillColor": "#FFF2CC", "font": "Verdana", "fontSize": 12, "bold": true, "numberFormat": "0.00" } }
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
    public void SheetProtection_UsesAResolvedSecretWithoutSerializingIt()
    {
        const string secret = "test-sheet-secret";
        string source = _fixture.CreateSalesWorkbook("protected.xlsx");
        CellsEditResult result = Apply(
            source,
            """{ "ops": [ { "op": "protect_sheet", "sheet": "Data", "passwordEnv": "ASPOSE_CLI_TEST_SHEET_PASSWORD", "allow": ["sort"] } ] }""",
            "protected.out.xlsx",
            new Dictionary<string, Secret> { ["ASPOSE_CLI_TEST_SHEET_PASSWORD"] = new(secret) });

        using var workbook = new Workbook(result.Output!.Path);
        Assert.True(workbook.Worksheets["Data"].Protection.AllowSorting);
        Assert.True(workbook.Worksheets["Data"].IsProtected);
        string json = System.Text.Json.JsonSerializer.Serialize(
            result, result.GetType(), ProductJsonContext.Definition.LocalOptions);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }
    [Fact]
    public void SheetProtection_LeavesCellsFormattedUnlockedEditable()
    {
        string source = _fixture.CreateSalesWorkbook("input-cells.xlsx");
        CellsEditResult result = Apply(
            source,
            """
            { "ops": [
              { "op": "format_range", "sheet": "Data", "range": "B2:C2", "style": { "locked": false, "fillColor": "#FFF2CC" } },
              { "op": "protect_sheet", "sheet": "Data" }
            ] }
            """,
            "input-cells.out.xlsx");

        using (var workbook = new Workbook(result.Output!.Path))
        {
            Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
            Assert.True(workbook.Worksheets["Data"].IsProtected);
            Assert.False(cells["B2"].GetStyle().IsLocked);
            Assert.True(cells["B3"].GetStyle().IsLocked);
        }

        CellsReadResult read = CellsRead.Run(_fixture.Session,
            new CellsReadRequest
            {
                Input = result.Output.Path,
                SheetName = "Data",
                Range = global::Aspose.Cli.Product.Cells.Contracts.Addressing.A1.ParseRange("B2:B3").Range,
                Content = CellsReadContents.Full,
                MaxCells = 10,
            });
        Assert.False(read.Styles![Assert.IsType<string>(read.Sheet.Cells![0][0].StyleId)].Locked);
        Assert.Null(read.Sheet.Cells[1][0].StyleId);
    }

    [Fact]
    public void ChartCosmetics_PersistThroughSaveAndReopen()
    {
        string source = _fixture.CreateSalesWorkbook("chart.xlsx");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "insert_chart", "sheet": "Data", "kind": "column", "dataRange": "A1:C3", "at": "E2:L18",
                "title": "Quarterly", "legend": { "visible": true, "position": "right" },
                "axisTitles": { "category": "Region", "value": "Sales (USD)" },
                "seriesColors": ["#C00000", "#00B050"],
                "dataLabels": { "visible": true, "numberFormat": "#,##0" } }
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

    /// <summary>
    /// The default palette gives every series of a column chart and every slice of a pie its own
    /// color, past the six theme accents too.
    /// </summary>
    [Fact]
    public void CreateChart_DefaultPaletteKeepsSeriesAndSlicesDistinct()
    {
        string source = _fixture.CreateSalesWorkbook("palette.xlsx");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1:H4", "values": [
                ["Month","S1","S2","S3","S4","S5","S6","S7"],
                ["Jan",1,2,3,4,5,6,7],["Feb",2,3,4,5,6,7,8],["Mar",3,4,5,6,7,8,9]] },
              { "op": "set_values", "sheet": "Data", "range": "J1:K6", "values": [
                ["Region","Sales"],["East",5],["North",4],["South",3],["West",2],["Central",1]] },
              { "op": "insert_chart", "sheet": "Data", "kind": "column", "dataRange": "A1:H4", "at": "A8:H24" },
              { "op": "insert_chart", "sheet": "Data", "kind": "pie", "dataRange": "J1:K6", "at": "J8:P24" }
            ] }
            """,
            "palette.out.xlsx").Output!.Path;

        using var workbook = new Workbook(output);
        ChartCollection charts = workbook.Worksheets["Data"].Charts;
        Assert.Equal(7, charts[0].NSeries.Cast<Series>().Select(static series => series.Area.ForegroundColor.ToArgb()).Distinct().Count());
        Assert.Equal(5, charts[1].NSeries[0].Points.Cast<ChartPoint>().Select(static point => point.Area.ForegroundColor.ToArgb()).Distinct().Count());
    }

    [Fact]
    public void FormulaConditionalFormat_PreservesTheCellsNumberFormat()
    {
        string source = _fixture.CreateSalesWorkbook("formula-format.xlsx");
        CellsEditResult result = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Invoice","Amount","Status"],["INV-1",120.5,"OVERDUE"],["INV-2",80,"PAID"]] },
              { "op": "format_range", "sheet": "Data", "range": "B2:B3", "style": { "numberFormat": "#,##0.00" } },
              { "op": "add_conditional_format", "sheet": "Data", "range": "A2:C3",
                "rule": { "kind": "formula", "value1": "$C2=\"OVERDUE\"" },
                "matchStyle": { "fillColor": "#FFC7CE" } }
            ] }
            """,
            "formula-format.out.xlsx");

        using (var workbook = new Workbook(result.Output!.Path))
        {
            FormatCondition condition = workbook.Worksheets["Data"].ConditionalFormattings[0][0];
            Assert.Equal("=$C2=\"OVERDUE\"", condition.Formula1);
            Assert.Equal("=$C3=\"OVERDUE\"", condition.GetFormula1(2, 0));
        }

        CellsReadResult read = CellsRead.Run(_fixture.Session,
            new CellsReadRequest
            {
                Input = result.Output.Path,
                SheetName = "Data",
                Range = global::Aspose.Cli.Product.Cells.Contracts.Addressing.A1.ParseRange("B2:B2").Range,
                Content = CellsReadContents.Full,
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
              { "op": "add_sparkline", "sheet": "Second", "dataRange": "B2:E4", "at": "F2:F4",
                "kind": "winloss", "color": "#1F4E79" }
            ] }
            """,
            "sparkline.out.xlsx").Output!.Path;

        using var workbook = new Workbook(output);
        SparklineGroup group = workbook.Worksheets["Second"].SparklineGroups[0];
        Assert.Equal(SparklineType.WinLoss, group.Type);
        Assert.Equal(3, group.Sparklines.Count);
        Assert.Equal("Second!B2:E2", group.Sparklines[0].DataRange);
        Assert.Equal("Second!B4:E4", group.Sparklines[2].DataRange);
    }

    [Fact]
    public void PageSetup_StoresMarginsInTheInchesTheContractDeclares()
    {
        string source = _fixture.CreateSalesWorkbook("margins.xlsx");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "set_page_setup", "sheet": "Data",
                "margins": { "top": 72, "bottom": 90, "left": 36, "right": 36 },
                "headerMargin": 21.6, "footerMargin": 21.6 }
            ] }
            """,
            "margins.out.xlsx").Output!.Path;

        // OOXML stores <pageMargins> in inches, so this pins the contract unit
        // itself rather than the engine's own centimetre representation.
        Dictionary<string, double> stored = ReadPageMargins(output, "sheet1.xml");
        Assert.Equal(1d, stored["top"], 6);
        Assert.Equal(1.25d, stored["bottom"], 6);
        Assert.Equal(0.5d, stored["left"], 6);
        Assert.Equal(0.5d, stored["right"], 6);
        Assert.Equal(0.3d, stored["header"], 6);
        Assert.Equal(0.3d, stored["footer"], 6);
    }

    [Theory]
    [InlineData("column", 95, 100, 105, "0")]      // a length must start at zero
    [InlineData("bar", 95, 100, 105, "0")]
    [InlineData("area", 95, 100, 105, "0")]
    [InlineData("column", -10, 0, 15, null)]       // pinning zero would clip the negative
    [InlineData("line", 95, 100, 105, null)]       // a line encodes value by position
    public void Chart_DrawsALengthFromZeroWithoutHidingNegativeValues(
        string kind, double first, double second, double third, string? expectedMinimum)
    {
        string source = _fixture.CreateSalesWorkbook($"axis-{kind}-{first}.xlsx");
        string output = Apply(
            source,
            $$"""
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Item","Amount"],["Alpha",{{first}}],["Beta",{{second}}],["Gamma",{{third}}]] },
              { "op": "insert_chart", "sheet": "Data", "kind": "{{kind}}",
                "dataRange": "A1:B4", "at": "D2:K18", "title": "Amount" }
            ] }
            """,
            $"axis-{kind}-{first}.out.xlsx").Output!.Path;

        Assert.Equal(expectedMinimum, ReadValueAxisMinimum(output));
    }

    [Fact]
    public void UpdateChart_KeepsAValueAxisMinimumTheWorkbookAlreadyCarried()
    {
        string source = _fixture.CreateSalesWorkbook("axis-explicit.xlsx");
        string seeded = Apply(
            source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Item","Amount"],["Alpha",95],["Beta",100],["Gamma",105]] },
              { "op": "insert_chart", "sheet": "Data", "kind": "column",
                "dataRange": "A1:B4", "at": "D2:K18", "title": "Amount" }
            ] }
            """,
            "axis-explicit.seeded.xlsx").Output!.Path;
        using (var workbook = new Workbook(seeded))
        {
            Aspose.Cells.Charts.Axis axis = workbook.Worksheets["Data"].Charts[0].ValueAxis;
            axis.IsAutomaticMinValue = false;
            axis.MinValue = 50d;
            workbook.Save(seeded);
        }

        string output = Apply(
            seeded,
            """{ "ops": [ { "op": "set_chart", "sheet": "Data", "index": 1, "title": "Revised" } ] }""",
            "axis-explicit.out.xlsx").Output!.Path;

        Assert.Equal("50", ReadValueAxisMinimum(output));
    }

    [Fact]
    public void ResizeTable_TakesInTheRowsWrittenBelowIt()
    {
        string source = CreateTableWorkbook("resize-table.xlsx");
        CellsEditResult result = CellsEdit.Run(_fixture.Session,
            new CellsEditRequest
            {
                Input = source,
                Batch = ParseOps("""
                    { "ops": [
                      { "op": "set_print_area", "sheet": "Data", "range": "A1:D3" },
                      { "op": "set_values", "sheet": "Data", "range": "A4", "values": [["North", 5, 6], ["South", 7, 8]] },
                      { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D5" }
                    ] }
                    """),
                Output = TestOutput.At(_fixture.Temp.File("resize-table.out.xlsx"), overwrite: true),
                Verify = true,
            });

        Assert.True(result.Verification!.Ok);
        // The table's calculated Total column fills the rows it takes in, so those cells count too.
        Assert.Equal(["cell/Data!D4", "cell/Data!D5", "table/Sales"], result.Applied[2].Targets.Order(StringComparer.Ordinal));
        Assert.Equal(3, result.Applied[2].ItemsAffected);
        CellsInspectResult read = CellsInfo.Run(_fixture.Session,
            new CellsInspectRequest { Input = result.Output!.Path, Details = [CellsInspectDetails.Tables, CellsInspectDetails.Layout] });
        CellsTableInfo table = Assert.Single(read.Workbook.Tables!);
        Assert.Equal(("Sales", "A1:D5", "TableStyleMedium2"), (table.Name, table.Range, table.TableStyle));
        // As in Excel, a print area set as cell addresses stays where it was.
        Assert.Equal("A1:D3", Assert.Single(read.Workbook.Layouts!).PrintArea!.Range);
        using var reopened = new Workbook(result.Output!.Path);
        Cell total = reopened.Worksheets["Data"].Cells["D5"];
        Assert.Equal(("=[Q1]+[Q2]", 15d), (total.Formula, total.DoubleValue));
    }

    [Fact]
    public void ResizeTable_MovesTheTotalsRowToTheNewLastRow()
    {
        string source = CreateTableWorkbook("resize-table-totals.xlsx", totalsRow: true);
        CellsEditResult result = Apply(source,
            """{ "ops": [ { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D6" } ] }""",
            "resize-table-totals.out.xlsx");

        // The totals row leaves row 4, which takes the calculated column, for row 6.
        Assert.Equal(["cell/Data!A4", "cell/Data!A6", "cell/Data!D4", "cell/Data!D5", "cell/Data!D6", "table/Sales"],
            result.Applied[0].Targets.Order(StringComparer.Ordinal));
        Assert.Equal(6, result.Applied[0].ItemsAffected);
        using var workbook = new Workbook(result.Output!.Path);
        Aspose.Cells.Tables.ListObject table = workbook.Worksheets["Data"].ListObjects[0];
        Assert.Equal((5, true), (table.EndRow, table.ShowTotals));
        Assert.StartsWith("=SUBTOTAL(", workbook.Worksheets["Data"].Cells["D6"].Formula, StringComparison.Ordinal);
    }

    [Fact]
    public void ResizeTable_RefusesToShrinkATableWithATotalsRow()
    {
        string source = CreateTableWorkbook("resize-table-totals-shrink.xlsx", totalsRow: true);
        string output = _fixture.Temp.File("resize-table-totals-shrink.out.xlsx");

        CliException error = Assert.Throws<CliException>(() => Apply(source,
            """{ "ops": [ { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D3" } ] }""",
            "resize-table-totals-shrink.out.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("totals row", error.Message, StringComparison.Ordinal);
        Assert.Contains("A1:D5", error.Hint, StringComparison.Ordinal);
        Assert.Contains("cannot shrink a table with a totals row", error.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ResizeTable_RefusesToWriteTheTotalsRowOverValues()
    {
        string source = CreateTableWorkbook("resize-table-totals-over.xlsx", totalsRow: true);
        string output = _fixture.Temp.File("resize-table-totals-over.out.xlsx");

        // The totals row is row 4; rows 5 and 6 hold data the table should take in.
        CliException error = Assert.Throws<CliException>(() => Apply(source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A5", "values": [["North", 5, 6], ["South", 7, 8]] },
              { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D6" }
            ] }
            """,
            "resize-table-totals-over.out.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("row 6", error.Message, StringComparison.Ordinal);
        Assert.Contains("such as A1:D7", error.Hint, StringComparison.Ordinal);
        Assert.Contains("old row 4 becomes an empty data row", error.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ResizeTable_MovesTheTotalsRowOntoAnEmptyRowBelowNewData()
    {
        string source = CreateTableWorkbook("resize-table-totals-below.xlsx", totalsRow: true);
        string output = Apply(source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A5", "values": [["North", 5, 6], ["South", 7, 8]] },
              { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D7" }
            ] }
            """,
            "resize-table-totals-below.out.xlsx").Output!.Path;

        using var workbook = new Workbook(output);
        Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
        Assert.Equal((6, true), (workbook.Worksheets["Data"].ListObjects[0].EndRow, workbook.Worksheets["Data"].ListObjects[0].ShowTotals));
        Assert.Equal(("South", 7d, 8d), (cells["A6"].StringValue, cells["B6"].DoubleValue, cells["C6"].DoubleValue));
        Assert.StartsWith("=SUBTOTAL(", cells["D7"].Formula, StringComparison.Ordinal);
    }

    [Fact]
    public void ResizeTable_CountsTheRowsItLeavesOut()
    {
        string source = CreateTableWorkbook("resize-table-shrink.xlsx");
        CellsEditResult result = Apply(source,
            """{ "ops": [ { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D2" } ] }""",
            "resize-table-shrink.out.xlsx");

        // The engine rewrites the calculated formula of the row it leaves out to name the table.
        Assert.Equal(["cell/Data!D3", "table/Sales"], result.Applied[0].Targets.Order(StringComparer.Ordinal));
        Assert.Equal(2, result.Applied[0].ItemsAffected);
    }

    [Fact]
    public void ResizeTable_ToTheRangeItHasChangesNothing()
    {
        string source = CreateTableWorkbook("resize-table-same.xlsx");
        CellsEditResult result = Apply(source,
            """{ "ops": [ { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "A1:D3" } ] }""",
            "resize-table-same.out.xlsx");

        Assert.Equal(0, result.Applied[0].ItemsAffected);
        Assert.Contains(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Theory]
    [InlineData("B1:D5", false, "A1:D5")]
    [InlineData("A2:D5", false, "A1:D5")]
    [InlineData("A1:E5", false, "A1:D5")]
    [InlineData("A1:C3", false, "A1:D3")]
    [InlineData("A1:D1", false, "A1:D2")]
    [InlineData("B1:D2", true, "A1:D4")]
    public void ResizeTable_RefusesARangeThatChangesTheHeaderRowOrLeavesNoDataRow(string range, bool totalsRow, string suggested)
    {
        string name = $"resize-table-{range.Replace(':', '-')}-{totalsRow}";
        string source = CreateTableWorkbook($"{name}.xlsx", totalsRow);

        CliException error = Assert.Throws<CliException>(() => Apply(source,
            $$"""{ "ops": [ { "op": "resize_table", "sheet": "Data", "name": "Sales", "range": "{{range}}" } ] }""",
            $"{name}.out.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(totalsRow ? "the table covers A1:D4" : "the table covers A1:D3", error.Message, StringComparison.Ordinal);
        Assert.EndsWith($"such as {suggested}.", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ResizeTable_NamesTheTablesOfTheSheetWhenTheNameIsUnknown()
    {
        string source = CreateTableWorkbook("resize-table-missing.xlsx");

        CliException error = Assert.Throws<CliException>(() => Apply(source,
            """{ "ops": [ { "op": "resize_table", "sheet": "Data", "name": "Orders", "range": "A1:D5" } ] }""",
            "resize-table-missing.out.xlsx"));

        Assert.Equal("TABLE_NOT_FOUND", error.Code.Name);
        Assert.Contains("Sales", error.Message, StringComparison.Ordinal);
        Assert.Contains("'aspose-cli cells inspect <file> --detail tables'", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Data!A1:D3 as the styled table Sales, with a Total column that adds Q1 and Q2, and optionally a totals row below.</summary>
    private string CreateTableWorkbook(string fileName, bool totalsRow = false)
    {
        string path = _fixture.Temp.File(fileName);
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells.ImportTwoDimensionArray(new object[,] { { "Region", "Q1", "Q2", "Total" }, { "East", 1, 2, "" }, { "West", 3, 4, "" } }, 0, 0);
        Aspose.Cells.Tables.ListObject table = sheet.ListObjects[sheet.ListObjects.Add(0, 0, 2, 3, hasHeaders: true)];
        table.DisplayName = "Sales";
        table.TableStyleType = Aspose.Cells.Tables.TableStyleType.TableStyleMedium2;
        table.ListColumns[3].Formula = "=[Q1]+[Q2]";
        table.ShowTotals = totalsRow;
        workbook.Save(path);
        return path;
    }

    private static string? ReadValueAxisMinimum(string workbookPath)
    {
        using ZipArchive package = ZipFile.OpenRead(workbookPath);
        ZipArchiveEntry entry = Assert.Single(
            package.Entries,
            item => Regex.IsMatch(item.FullName, @"^xl/charts/chart\d+\.xml$"));
        using Stream content = entry.Open();
        return XDocument.Load(content).Descendants()
            .FirstOrDefault(static node => node.Name.LocalName == "min")
            ?.Attribute("val")?.Value;
    }

    private static Dictionary<string, double> ReadPageMargins(string workbookPath, string sheetEntry)
    {
        using ZipArchive package = ZipFile.OpenRead(workbookPath);
        ZipArchiveEntry entry = Assert.Single(
            package.Entries, item => item.FullName == $"xl/worksheets/{sheetEntry}");
        using Stream content = entry.Open();
        XElement margins = Assert.Single(
            XDocument.Load(content).Descendants(),
            static node => node.Name.LocalName == "pageMargins");
        return margins.Attributes().ToDictionary(
            static attribute => attribute.Name.LocalName,
            static attribute => double.Parse(
                attribute.Value, CultureInfo.InvariantCulture));
    }

    private static CellsOpsBatch ParseOps(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
