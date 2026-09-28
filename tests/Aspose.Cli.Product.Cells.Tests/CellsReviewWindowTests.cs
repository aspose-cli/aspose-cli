using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A worksheet too large for one review image contributes its first rows, and the review still
/// renders every other sheet, instead of failing with RENDER_TOO_LARGE.
/// </summary>
public sealed class CellsReviewWindowTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsReviewWindowTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void ClippedValues_AreFoundOnlyWhereTheyCannotShowInFull()
    {
        string path = _fixture.Temp.File("clipped.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            cells["A1"].PutValue("Disposable Syringe 5ml, box of 100");
            cells["B1"].PutValue("North");
            cells["A2"].PutValue("一次性使用输液器（带针）");
            cells["B2"].PutValue("South");
            cells["A3"].PutValue("Spills into the empty cell to its right");
            Aspose.Cells.Style wrapped = cells["A4"].GetStyle();
            wrapped.IsTextWrapped = true;
            cells["A4"].SetStyle(wrapped);
            cells["A4"].PutValue("Wrapped text never spills or clips");
            cells["B4"].PutValue("West");
            workbook.Save(path);
        }

        Ports.CellsReviewSheetLayout sheet = Assert.Single(
            ((Ports.ICellsReviewLayoutPort)_fixture.Engine).Inspect(path, null).Sheets);

        Assert.Equal(2, sheet.ClippedCells.Count);
        Assert.Equal(["A1", "A2"], sheet.ClippedCells.Samples);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void OversizedSheet_RendersItsFirstRowsAndTheOtherSheets()
    {
        string path = _fixture.Temp.File("detail-and-summary.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells detail = workbook.Worksheets[0].Cells;
            workbook.Worksheets[0].Name = "Detail";
            for (int row = 0; row < 20_000; row++)
            {
                for (int column = 0; column < 6; column++)
                {
                    detail[row, column].PutValue(row * 10 + column);
                }
            }

            Aspose.Cells.Worksheet summary = workbook.Worksheets[workbook.Worksheets.Add()];
            summary.Name = "Summary";
            summary.Cells["A1"].PutValue("Total");
            workbook.Save(path);
        }

        var sink = new MemoryArtifactSink();
        ViewManifest manifest = _fixture.Engine.RenderView(
            path,
            new ViewRenderRequest { View = CellsViews.Sheets, MaxParts = 8, Purpose = ViewPurpose.Evidence },
            sink);

        Assert.Equal(["Detail", "Summary"], manifest.Parts.Select(static part => part.Id));
        var window = Assert.Single(manifest.Warnings ?? [], static warning => warning.Code == "CELLS_SHEET_PARTIALLY_RENDERED");
        Assert.Equal("Detail", window.Location);
        Assert.True(window.AffectsCompleteness);
    }
}
