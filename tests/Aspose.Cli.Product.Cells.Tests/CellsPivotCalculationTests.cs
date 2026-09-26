using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Pivot caches must capture formula results from preceding operations.</summary>
public sealed class CellsPivotCalculationTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsPivotCalculationTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void CreatePivot_UsesFormulaValuesAndDimensionsFromTheSameBatch()
    {
        string source = CreateWorkbook("pivot-formula-create.xlsx");
        string output = Apply(source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Region","Units","Revenue","Period"],["North",2,null,null],["South",3,null,null]] },
              { "op": "set_formula", "sheet": "Data", "range": "C2:C3", "formula": "=B2*100" },
              { "op": "set_formula", "sheet": "Data", "range": "D2:D3", "formula": "=IF(B2<3,\"Jan\",\"Feb\")" },
              { "op": "create_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:D3", "at": "A1",
                "rows": ["Region"], "columns": ["Period"], "values": [{ "field": "Revenue" }] }
            ] }
            """, "pivot-formula-create.out.xlsx");

        using var reopened = new Workbook(output);
        Worksheet data = reopened.Worksheets["Data"];
        Worksheet pivot = reopened.Worksheets["Pivot"];
        Assert.Equal(200d, data.Cells["C2"].DoubleValue);
        Assert.Equal(300d, data.Cells["C3"].DoubleValue);
        Assert.Equal("Jan", pivot.Cells["B2"].StringValue);
        Assert.Equal("Feb", pivot.Cells["C2"].StringValue);
        Assert.Equal(200d, pivot.Cells["B3"].DoubleValue);
        Assert.Equal(300d, pivot.Cells["C4"].DoubleValue);
        Assert.Equal(500d, pivot.Cells["D5"].DoubleValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ByRegion")]
    public void RefreshPivot_UsesChangedInputsAndFormulasFromTheSameBatch(string? name)
    {
        string source = CreateWorkbook("pivot-formula-refresh.xlsx");
        string seeded = Apply(source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Region","Units","Revenue"],["North",2,null],["South",3,null]] },
              { "op": "set_formula", "sheet": "Data", "range": "C2:C3", "formula": "=B2*100" },
              { "op": "create_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:C3", "at": "A1",
                "name": "ByRegion", "rows": ["Region"], "values": [{ "field": "Revenue" }] }
            ] }
            """, "pivot-formula-refresh.seeded.xlsx");
        string selector = name is null ? string.Empty : ", \"name\": \"ByRegion\"";
        string output = Apply(seeded,
            $$"""
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "B2", "values": [[4]] },
              { "op": "set_formula", "sheet": "Data", "range": "C3", "formula": "=B3*120" },
              { "op": "refresh_pivot", "sheet": "Pivot"{{selector}} }
            ] }
            """, "pivot-formula-refresh.out.xlsx");

        using var reopened = new Workbook(output);
        Worksheet data = reopened.Worksheets["Data"];
        Worksheet pivot = reopened.Worksheets["Pivot"];
        Assert.Equal(400d, data.Cells["C2"].DoubleValue);
        Assert.Equal(360d, data.Cells["C3"].DoubleValue);
        Assert.Equal(400d, pivot.Cells["B2"].DoubleValue);
        Assert.Equal(360d, pivot.Cells["B3"].DoubleValue);
        Assert.Equal(760d, pivot.Cells["B4"].DoubleValue);
    }

    [Fact]
    public void RefreshPivot_PreservesColumnWidthsAndExistingCellFormatting()
    {
        string source = CreateWorkbook("pivot-refresh-format.xlsx");
        string seeded = Apply(source,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1",
                "values": [["Region","Revenue"],["North",100],["South",200]] },
              { "op": "create_pivot", "sheet": "Pivot", "sourceRange": "Data!A1:B3", "at": "A5",
                "rows": ["Region"], "values": [{ "field": "Revenue", "numberFormat": "#,##0.00" }] },
              { "op": "set_values", "sheet": "Pivot", "range": "A1",
                "values": [["A long management report title must not resize the region column on refresh"]] },
              { "op": "format_range", "sheet": "Pivot", "range": "A1", "style": { "size": 16, "bold": true } },
              { "op": "format_range", "sheet": "Pivot", "range": "A5:B5",
                "style": { "bg": "#1F3864", "color": "#FFFFFF", "bold": true } },
              { "op": "resize_columns", "sheet": "Pivot", "from": "A", "width": 18 },
              { "op": "resize_columns", "sheet": "Pivot", "from": "B", "width": 16 }
            ] }
            """, "pivot-refresh-format.seeded.xlsx");
        double labelWidth;
        double valueWidth;
        using (var baseline = new Workbook(seeded))
        {
            labelWidth = baseline.Worksheets["Pivot"].Cells.GetColumnWidth(0);
            valueWidth = baseline.Worksheets["Pivot"].Cells.GetColumnWidth(1);
        }

        string output = Apply(seeded,
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "B2", "values": [[450]] },
              { "op": "refresh_pivot", "sheet": "Pivot" }
            ] }
            """, "pivot-refresh-format.out.xlsx");

        using var reopened = new Workbook(output);
        Worksheet pivot = reopened.Worksheets["Pivot"];
        Assert.Equal(650d, pivot.Cells["B8"].DoubleValue);
        Assert.Equal(labelWidth, pivot.Cells.GetColumnWidth(0));
        Assert.Equal(valueWidth, pivot.Cells.GetColumnWidth(1));
        Assert.Equal("A long management report title must not resize the region column on refresh",
            pivot.Cells["A1"].StringValue);
        Assert.Equal(16, pivot.Cells["A1"].GetStyle().Font.Size);
        foreach (string address in new[] { "A5", "B5" })
        {
            Style header = pivot.Cells[address].GetStyle();
            Assert.Equal(BackgroundType.Solid, header.Pattern);
            Assert.Equal("#1F3864", $"#{header.ForegroundColor.R:X2}{header.ForegroundColor.G:X2}{header.ForegroundColor.B:X2}");
            Assert.Equal("#FFFFFF", $"#{header.Font.Color.R:X2}{header.Font.Color.G:X2}{header.Font.Color.B:X2}");
            Assert.True(header.Font.IsBold);
        }
    }
    private string CreateWorkbook(string output) => _fixture.Engine.Create(new NewWorkbookRequest
    {
        OutputPath = _fixture.Temp.File(output),
        SheetNames = ["Data", "Pivot"],
        Overwrite = true,
    }).Output.Path;

    private string Apply(string source, string operations, string output) => _fixture.Engine.ApplyOps(
        source,
        ParseOps(operations),
        new EditRequest
        {
            OutputPath = _fixture.Temp.File(output),
            Overwrite = true,
        }).Output!.Path;

    private static CellsOpsBatch ParseOps(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
