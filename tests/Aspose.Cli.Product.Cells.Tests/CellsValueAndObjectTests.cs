using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Tables;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Values and objects keep exactly what the operation declared.</summary>
public sealed class CellsValueAndObjectTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsValueAndObjectTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void FractionalFontSizes_PersistAndReadBack()
    {
        string source = _fixture.CreateSalesWorkbook("font-size.xlsx");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "set_default_font", "font": "Calibri", "size": 10.5 },
              { "op": "format_range", "sheet": "Data", "range": "A1", "style": { "size": 11.5 } }
            ] }
            """,
            "font-size.out.xlsx");

        using (var workbook = new Workbook(output))
        {
            Assert.Equal(10.5, workbook.DefaultStyle.Font.DoubleSize);
            Assert.Equal(11.5, workbook.Worksheets["Data"].Cells["A1"].GetStyle().Font.DoubleSize);
        }

        WorkbookReadResult read = CellsRead.Run(_fixture.Session, new ReadRequest
        {
            Input = output,
            SheetName = "Data",
            Range = A1.ParseRange("A1").Range,
            Scope = ReadScope.Full,
            MaxCells = 1,
        });
        string styleId = Assert.IsType<string>(read.Sheet.Cells![0][0].StyleId);
        Assert.Equal(11.5, read.Styles![styleId].Size);
    }

    [Theory]
    [InlineData("""["North, East", "West"]""")]
    [InlineData("""["Say \"hi\"", "West"]""")]
    [InlineData("""["", "West"]""")]
    public void ListValidation_RejectsItemsExcelCannotStoreLiterally(string items) =>
        AssertInvalid($$"""{ "op": "set_validation", "range": "A1:A9", "type": "list", "listItems": {{items}} }""");

    [Fact]
    public void ListValidation_RejectsAListLongerThanExcelAllows()
    {
        string items = string.Join(",", Enumerable.Range(0, 26).Select(static index => $"\"Item{index:000}xxxx\""));
        CliException error = AssertInvalid($$"""{ "op": "set_validation", "range": "A1:A9", "type": "list", "listItems": [{{items}}] }""");
        Assert.Contains("255", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListValidation_StoresTheItemsAsOneLiteral()
    {
        string source = _fixture.CreateSalesWorkbook("list.xlsx");
        string output = Apply(
            source,
            """{ "ops": [ { "op": "set_validation", "sheet": "Data", "range": "D2:D9", "type": "list", "listItems": ["Low", "Medium", "High"] } ] }""",
            "list.out.xlsx");

        using (var workbook = new Workbook(output))
        {
            Validation validation = workbook.Worksheets["Data"].Validations[0];
            Assert.Equal(ValidationType.List, validation.Type);
            Assert.Equal("Low,Medium,High", validation.Formula1);
        }

        // Excel's own form: one quoted literal, with no quotes inside the items.
        using ZipArchive package = ZipFile.OpenRead(output);
        using Stream sheet = package.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XElement formula = XDocument.Load(sheet).Descendants().Single(static node => node.Name.LocalName == "formula1");
        Assert.Equal("\"Low,Medium,High\"", formula.Value);
    }

    [Fact]
    public void UpdateChart_LeavesAnExistingAutomaticValueAxisAlone()
    {
        string source = _fixture.CreateSalesWorkbook("axis-auto.xlsx");
        using (var workbook = new Workbook(source))
        {
            Worksheet data = workbook.Worksheets["Data"];
            data.Cells["E1"].PutValue("Item");
            data.Cells["F1"].PutValue("Amount");
            for (int row = 1; row <= 3; row++)
            {
                data.Cells[row, 4].PutValue("Item" + row);
                data.Cells[row, 5].PutValue(95 + (5 * row));
            }

            int index = data.Charts.Add(ChartType.Column, 6, 0, 20, 6);
            data.Charts[index].SetChartDataRange("E1:F4", true);
            workbook.Save(source);
        }

        string output = Apply(
            source,
            """{ "ops": [ { "op": "update_chart", "sheet": "Data", "index": 0, "title": "Revised", "dataRange": "E1:F4" } ] }""",
            "axis-auto.out.xlsx");

        Assert.Null(ReadValueAxisMinimum(output));
    }

    [Theory]
    [InlineData("TableStyleMedium2", TableStyleType.TableStyleMedium2)]
    [InlineData("tablestylelight9", TableStyleType.TableStyleLight9)]
    public void CreateTable_AppliesABuiltInStyle(string style, TableStyleType expected)
    {
        string source = _fixture.CreateSalesWorkbook($"table-{style}.xlsx");
        string output = Apply(
            source,
            $$"""{ "ops": [ { "op": "create_table", "sheet": "Data", "range": "A1:C2", "name": "Sales", "style": "{{style}}", "totalsRow": true } ] }""",
            $"table-{style}.out.xlsx");

        using var workbook = new Workbook(output);
        ListObject table = workbook.Worksheets["Data"].ListObjects[0];
        Assert.Equal(expected, table.TableStyleType);
        Assert.Equal("Sales", table.DisplayName);
        Assert.True(table.ShowTotals);
        Assert.Equal((0, 0, 2, 2), (table.StartRow, table.StartColumn, table.EndRow, table.EndColumn));
    }

    [Fact]
    public void CreateTable_AppliesACustomStyleTheWorkbookDefines()
    {
        string source = _fixture.CreateSalesWorkbook("table-custom.xlsx");
        using (var workbook = new Workbook(source))
        {
            workbook.Worksheets.TableStyles.AddTableStyle("HouseStyle");
            workbook.Save(source);
        }

        string output = Apply(
            source,
            """{ "ops": [ { "op": "create_table", "sheet": "Data", "range": "A1:C2", "style": "HouseStyle" } ] }""",
            "table-custom.out.xlsx");

        using var reopened = new Workbook(output);
        Assert.Equal("HouseStyle", reopened.Worksheets["Data"].ListObjects[0].TableStyleName);
    }

    [Theory]
    [InlineData("NoSuchStyle")]
    [InlineData("5")]
    [InlineData("Custom")]
    public void CreateTable_ReportsAStyleItCannotApplyBeforeCreatingTheTable(string style)
    {
        string source = _fixture.CreateSalesWorkbook("table-bad.xlsx");
        string output = _fixture.Temp.File("table-bad.out.xlsx");
        File.Delete(output);

        CliException error = Assert.Throws<CliException>(() => Apply(
            source,
            $$"""{ "ops": [ { "op": "create_table", "sheet": "Data", "range": "A1:C2", "style": "{{style}}" } ] }""",
            "table-bad.out.xlsx"));

        Assert.Equal(ErrorCodes.StyleNotFound, error.Code);
        Assert.Equal(style, error.Details!["requested"]!.GetValue<string>());
        Assert.Contains("TableStyleMedium2", error.Details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("T1")]
    [InlineData("xfd1048576")]
    [InlineData("R1C1")]
    [InlineData("rc")]
    [InlineData("C")]
    [InlineData("Sales Data")]
    [InlineData("1Sales")]
    [InlineData("Sales-2")]
    public void CreateTable_RejectsANameExcelRefusesBeforeAnyWorkbookOpens(string name)
    {
        CliException error = AssertInvalid(
            $$"""{ "op": "create_table", "sheet": "Data", "range": "A1:C2", "name": "{{name}}" }""");

        Assert.Equal("create_table", error.Details!["op"]!.GetValue<string>());
        Assert.Contains(name, error.Details["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("XFE1")]
    [InlineData("A0")]
    [InlineData("_2024")]
    [InlineData("\\\\Totals")]
    [InlineData("Sales.Q1?")]
    [InlineData("Übersicht")]
    public void CreateTable_AcceptsANameExcelAllows(string name) =>
        Assert.Single(Parse(
            $$"""{ "ops": [ { "op": "create_table", "sheet": "Data", "range": "A1:C2", "name": "{{name}}" } ] }""").Ops);

    [Theory]
    [InlineData("""{ "op": "create_table", "sheet": "Data", "range": "A1:C2", "name": "Sales" }""")]
    [InlineData("""{ "op": "define_name", "name": "Sales", "refersTo": "=Data!$A$1" }""")]
    public void CreateTable_RejectsANameTheWorkbookAlreadyUses(string first)
    {
        string source = _fixture.CreateSalesWorkbook("table-taken.xlsx");
        string output = _fixture.Temp.File("table-taken.out.xlsx");
        File.Delete(output);

        CliException error = Assert.Throws<CliException>(() => Apply(
            source,
            $$"""{ "ops": [ {{first}}, { "op": "create_table", "sheet": "Data", "range": "E1:F2", "name": "sales" } ] }""",
            "table-taken.out.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.False(File.Exists(output));
    }

    private static CliException AssertInvalid(string operation)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{ "ops": [ {{operation}} ] }"""));
        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        return error;
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

    private static CellsOpsBatch Parse(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);

    private string Apply(string path, string operations, string output) =>
        CellsEdit.Run(_fixture.Session,
            new EditRequest { Input = path, Batch = Parse(operations), Output = TestOutput.At(_fixture.Temp.File(output), overwrite: true) }).Output!.Path;
}
