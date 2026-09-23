using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Pivot;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// References the engine receives are rebuilt from the parsed range and always quoted, so
/// sheet names with punctuation, digits or spaces reach charts, pivots, sparklines and
/// hyperlinks intact.
/// </summary>
public sealed class CellsSheetReferenceTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsSheetReferenceTests(CellsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("P&L")]
    [InlineData("Q1-Data")]
    [InlineData("2026")]
    [InlineData("My Sheet")]
    public void QualifiedReferences_ReachTheNamedSheet(string name)
    {
        string source = Seed(name);
        string output = Apply(
            source,
            $$"""
            { "ops": [
              { "op": "create_chart", "sheet": "Dash", "type": "column", "dataRange": "{{name}}!A1:B3", "at": "D2:J12" },
              { "op": "create_pivot", "sheet": "Dash", "sourceRange": "{{name}}!A1:B3", "at": "A1",
                "rows": ["Region"], "values": [{ "field": "Amount" }] },
              { "op": "add_sparkline", "sheet": "Dash", "dataRange": "{{name}}!B2:B3", "location": "A10:A11" },
              { "op": "set_hyperlink", "sheet": "Dash", "cell": "A14", "target": "{{name}}!A1" }
            ] }
            """,
            "qualified.out.xlsx");

        using var workbook = new Workbook(output);
        Worksheet dash = workbook.Worksheets["Dash"];
        string quoted = "'" + name + "'";
        Assert.Equal($"={quoted}!$B$2:$B$3", dash.Charts[0].NSeries[0].Values);
        Assert.Equal($"{quoted}!B2:B2", dash.SparklineGroups[0].Sparklines[0].DataRange);
        Assert.Equal($"{quoted}!A1", dash.Hyperlinks[0].Address);
        Assert.Equal($"{name}!A1", dash.Cells["A14"].StringValue);
        AssertPivotTotals(dash, 30);
    }

    [Theory]
    [InlineData("P&L")]
    [InlineData("2026")]
    public void UnqualifiedReferences_ResolveOnTheOpsSheet(string name)
    {
        string source = Seed(name);
        string output = Apply(
            source,
            $$"""
            { "ops": [
              { "op": "create_chart", "sheet": "{{name}}", "type": "line", "dataRange": "A1:B3", "at": "D2:J12" },
              { "op": "create_pivot", "sheet": "{{name}}", "sourceRange": "A1:B3", "at": "D20",
                "rows": ["Region"], "values": [{ "field": "Amount" }] },
              { "op": "add_sparkline", "sheet": "{{name}}", "dataRange": "B2:B3", "location": "C2:C3" }
            ] }
            """,
            "unqualified.out.xlsx");

        using var workbook = new Workbook(output);
        Worksheet sheet = workbook.Worksheets[name];
        Assert.Equal($"='{name}'!$B$2:$B$3", sheet.Charts[0].NSeries[0].Values);
        Assert.Equal($"'{name}'!B2:B2", sheet.SparklineGroups[0].Sparklines[0].DataRange);
        AssertPivotTotals(sheet, 30);
    }

    [Theory]
    [InlineData("""{ "op": "create_chart", "sheet": "Dash", "type": "column", "dataRange": "Nope!A1:B3", "at": "D2:J12" }""")]
    [InlineData("""{ "op": "create_pivot", "sheet": "Dash", "sourceRange": "Nope!A1:B3", "at": "A1", "values": [{ "field": "Amount" }] }""")]
    [InlineData("""{ "op": "add_sparkline", "sheet": "Dash", "dataRange": "Nope!B2:B3", "location": "A10:A11" }""")]
    [InlineData("""{ "op": "set_hyperlink", "sheet": "Dash", "cell": "A1", "target": "Nope!A1" }""")]
    public void AReferenceToAMissingSheet_IsSheetNotFound(string operation)
    {
        string source = Seed("Data");
        string output = _fixture.Temp.File("missing.out.xlsx");
        File.Delete(output);

        CliException error = Assert.Throws<CliException>(() => Apply(source, $$"""{ "ops": [ {{operation}} ] }""", "missing.out.xlsx"));

        Assert.Equal(CellsDiagnostics.SheetNotFound, error.Code);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("Data")]
    [InlineData("data")]
    [InlineData("DATA")]
    public void SearchSheetFilter_MatchesTheSheetCaseInsensitivelyLikeEveryOtherCommand(string sheet)
    {
        string source = Seed("Data");

        SearchResult result = _fixture.Engine.Search(source, new SearchRequest { Pattern = "East", SheetName = sheet });

        SearchHit hit = Assert.Single(result.Hits);
        Assert.Equal(("Data", "A2"), (hit.Sheet, hit.Cell));
    }

    [Fact]
    public void SearchSheetFilter_ReportsAMissingSheet()
    {
        CliException error = Assert.Throws<CliException>(() =>
            _fixture.Engine.Search(Seed("Data"), new SearchRequest { Pattern = "East", SheetName = "Nope" }));

        Assert.Equal(CellsDiagnostics.SheetNotFound, error.Code);
    }

    [Fact]
    public void HyperlinkTarget_MustBeACellReference()
    {
        CliException error = Assert.Throws<CliException>(() => CellsOps.Catalog.Parse<OpsBatch>(
            """{ "ops": [ { "op": "set_hyperlink", "cell": "A1", "target": "SomeDefinedName" } ] }""",
            Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void Pivots_GetUniqueDefaultNamesAndRejectADuplicateName()
    {
        string source = Seed("Data");
        string output = Apply(
            source,
            """
            { "ops": [
              { "op": "create_pivot", "sheet": "Dash", "sourceRange": "Data!A1:B3", "at": "A1", "rows": ["Region"], "values": [{ "field": "Amount" }] },
              { "op": "create_pivot", "sheet": "Dash", "sourceRange": "Data!A1:B3", "at": "E1", "rows": ["Region"], "values": [{ "field": "Amount" }] },
              { "op": "create_pivot", "sheet": "Data", "sourceRange": "A1:B3", "at": "E1", "values": [{ "field": "Amount" }] }
            ] }
            """,
            "pivot-names.out.xlsx");

        using (var workbook = new Workbook(output))
        {
            Assert.Equal(
                ["PivotTable1", "PivotTable2"],
                workbook.Worksheets["Dash"].PivotTables.Cast<PivotTable>().Select(static pivot => pivot.Name));
            Assert.Equal("PivotTable3", workbook.Worksheets["Data"].PivotTables[0].Name);
        }

        CliException error = Assert.Throws<CliException>(() => Apply(
            output,
            """{ "ops": [ { "op": "create_pivot", "sheet": "Dash", "sourceRange": "Data!A1:B3", "at": "J1", "name": "pivottable1", "values": [{ "field": "Amount" }] } ] }""",
            "pivot-duplicate.out.xlsx"));
        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("PivotTable1", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPivotTotals(Worksheet sheet, double expected)
    {
        PivotTable pivot = sheet.PivotTables[0];
        pivot.PivotCache.Refresh();
        pivot.CalculateData();
        CellArea area = pivot.TableRange1;
        Assert.Equal(expected, sheet.Cells[area.EndRow, area.EndColumn].DoubleValue);
    }

    private string Seed(string dataSheet)
    {
        string created = _fixture.Engine.CreateWorkbook(new NewWorkbookRequest
        {
            OutputPath = _fixture.Temp.File("references.xlsx"),
            SheetNames = [dataSheet, "Dash"],
            Overwrite = true,
        }).Output.Path;
        return Apply(
            created,
            $$"""{ "ops": [ { "op": "set_values", "sheet": "{{dataSheet}}", "range": "A1", "values": [["Region","Amount"],["East",10],["West",20]] } ] }""",
            "references.seeded.xlsx");
    }

    private string Apply(string path, string operations, string output) =>
        _fixture.Engine.ApplyOps(
            path,
            CellsOps.Catalog.Parse<OpsBatch>(operations, Aspose.Cli.Generated.ProductJsonContext.Definition),
            new EditRequest { OutputPath = _fixture.Temp.File(output), Overwrite = true }).Output!.Path;
}
