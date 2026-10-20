using System.Buffers.Binary;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A worksheet too large for one review image contributes its first rows, and the review still
/// renders every other sheet, instead of failing with RENDER_TOO_LARGE. A review image stays
/// within the 10000 pixels on a side that a model reads in full, and the warning gives the
/// command that renders the next window.
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
            // Ten characters fit the column in the default font but show as #### at 18 points.
            cells["D5"].PutValue(44573100);
            Aspose.Cells.Style large = cells["D5"].GetStyle();
            large.Font.Size = 18;
            large.Custom = "#,##0";
            cells["D5"].SetStyle(large);
            cells.SetColumnWidth(3, 11);
            workbook.Save(path);
        }

        CellsReviewSheetLayout sheet = Assert.Single(
            CellsView.Layout(_fixture.Session, path, null).Sheets);

        Assert.Equal(3, sheet.ClippedCells.Count);
        Assert.Equal(["A1", "A2", "D5"], sheet.ClippedCells.Samples);
    }

    /// <summary>
    /// The engine draws the item labels of a pivot table's outer column fields indented and cuts
    /// them off at their own column's edge, though the cell to their right is empty, and auto-fit
    /// leaves the indent out (CELLS-PIVOT-LABEL-INDENT): after auto-fit, review counts exactly
    /// those labels as clipped and points to widthChars. Value field captions, subtotal and grand
    /// total labels and the innermost field's items are drawn like any cell, and review leaves
    /// merged labels unmeasured.
    /// </summary>
    [Theory]
    [InlineData("products-over-values", "B4,D4,F4,H4")]
    [InlineData("regions-over-products", "B4,G4")]
    [InlineData("values-over-products", "")]
    [InlineData("merged-labels", "")]
    public void PivotColumnLabels_AfterAutoFit_AreClippedOnlyWhereTheEngineIndentsThem(string layout, string clipped)
    {
        string path = _fixture.Temp.File($"pivot-{layout}.xlsx");
        SavePivot(path, layout, widthChars: null);

        (CellsReviewSheetLayout sheet, ReviewFinding? finding) = ReviewPivot(path);

        string[] expected = clipped.Length == 0 ? [] : clipped.Split(',');
        Assert.Equal(expected, sheet.ClippedCells.Samples);
        Assert.Equal(expected.Length, sheet.ClippedCells.Count);
        if (expected.Length > 0)
        {
            Assert.Contains("widthChars", finding!.Hint, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The same pivot labels with their columns widened by an explicit width show in full, and
    /// review no longer reports them.
    /// </summary>
    [Fact]
    public void PivotColumnLabels_InWidenedColumns_AreNotClipped()
    {
        string path = _fixture.Temp.File("pivot-widened.xlsx");
        SavePivot(path, "products-over-values", widthChars: 16);

        (CellsReviewSheetLayout sheet, ReviewFinding? finding) = ReviewPivot(path);

        Assert.Equal(0, sheet.ClippedCells.Count);
        Assert.Null(finding);
    }

    private (CellsReviewSheetLayout Sheet, ReviewFinding? Finding) ReviewPivot(string path)
    {
        var adapter = new CellsViewAdapter();
        var request = new ViewRenderRequest { View = CellsViews.Sheets, MaxPartCount = 8, Purpose = ViewPurpose.Evidence };
        ViewManifest rendered = adapter.Render(_fixture.Session, path, request, new MemoryArtifactSink());
        ProductReviewAssessment assessment = adapter.Assess(_fixture.Session, path, request, rendered);
        CellsReviewSheetLayout sheet = Assert.Single(
            CellsView.Layout(_fixture.Session, path, null).Sheets,
            static sheet => sheet.Name == "Pivot");
        ReviewFinding? finding = assessment.Findings!.SingleOrDefault(static finding =>
            finding.Code == "CELLS_VALUES_CLIPPED" && finding.Location == "sheet/Pivot");
        return (sheet, finding);
    }

    /// <summary>
    /// A Chinese sales pivot as the UAT built it, in Arial, its columns auto-fitted, or widened to
    /// <paramref name="widthChars"/> from B on.
    /// </summary>
    private static void SavePivot(string path, string layout, double? widthChars)
    {
        using var workbook = new Aspose.Cells.Workbook();
        Aspose.Cells.Style standard = workbook.DefaultStyle;
        standard.Font.Name = "Arial";
        standard.Font.Size = 10;
        workbook.DefaultStyle = standard;
        Aspose.Cells.Worksheet source = workbook.Worksheets[0];
        source.Name = "Src";
        string[] headers = ["区域", "产品", "金额", "数量"];
        for (int column = 0; column < headers.Length; column++)
        {
            source.Cells[0, column].PutValue(headers[column]);
        }

        int row = 1;
        foreach (string region in new[] { "Eastern Region", "Southern Region" })
        {
            foreach (string product in new[] { "笔记本 Laptop", "显示器 Monitor", "耳机 Headset", "键盘 Keyboard" })
            {
                source.Cells[row, 0].PutValue(region);
                source.Cells[row, 1].PutValue(product);
                source.Cells[row, 2].PutValue(1000 + row);
                source.Cells[row, 3].PutValue(row);
                row++;
            }
        }

        Aspose.Cells.Worksheet sheet = workbook.Worksheets[workbook.Worksheets.Add()];
        sheet.Name = "Pivot";
        Aspose.Cells.Pivot.PivotTable pivot = sheet.PivotTables[sheet.PivotTables.Add($"=Src!A1:D{row}", "A3", "Sales")];
        const Aspose.Cells.Pivot.PivotFieldType Row = Aspose.Cells.Pivot.PivotFieldType.Row;
        const Aspose.Cells.Pivot.PivotFieldType Column = Aspose.Cells.Pivot.PivotFieldType.Column;
        const Aspose.Cells.Pivot.PivotFieldType Data = Aspose.Cells.Pivot.PivotFieldType.Data;
        switch (layout)
        {
            case "regions-over-products":
                pivot.AddFieldToArea(Column, "区域");
                pivot.AddFieldToArea(Column, "产品");
                pivot.AddFieldToArea(Data, "金额");
                break;
            case "values-over-products":
                pivot.AddFieldToArea(Row, "区域");
                pivot.AddFieldToArea(Data, "金额");
                pivot.AddFieldToArea(Data, "数量");
                pivot.AddFieldToArea(Column, pivot.ValuesField);
                pivot.AddFieldToArea(Column, "产品");
                break;
            default:
                pivot.AddFieldToArea(Row, "区域");
                pivot.AddFieldToArea(Column, "产品");
                pivot.AddFieldToArea(Data, "金额");
                pivot.AddFieldToArea(Data, "数量");
                pivot.AddFieldToArea(Column, pivot.ValuesField);
                pivot.MergeLabels = layout == "merged-labels";
                break;
        }

        pivot.CalculateData();
        sheet.AutoFitColumns(0, 15);
        if (widthChars is { } width)
        {
            for (int column = 1; column <= 15; column++)
            {
                sheet.Cells.SetColumnWidth(column, width);
            }
        }

        workbook.Save(path);
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
        ViewManifest manifest = CellsView.Render(_fixture.Session, path,
            new ViewRenderRequest { View = CellsViews.Sheets, MaxPartCount = 8, Purpose = ViewPurpose.Evidence },
            sink);

        Assert.Equal(["Detail", "Summary"], manifest.Parts.Select(static part => part.Id));
        var window = Assert.Single(manifest.Warnings ?? [], static warning => warning.Code.Name == "SHEET_PARTIALLY_RENDERED");
        Assert.Equal("sheet/Detail", window.Location);
        Assert.True(window.AffectsCompleteness);
    }

    /// <summary>
    /// A sheet far taller than a readable image, though well within the pixel budget, shows its
    /// first rows within 10000 pixels, and the warning's command renders the rows after them.
    /// </summary>
    [Fact]
    public void TallSheet_ShowsAReadableWindowAndTheCommandForTheNext()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.File("tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Worksheet detail = workbook.Worksheets[0];
            detail.Name = "Detail";
            for (int row = 0; row < 2_000; row++)
            {
                for (int column = 0; column < 6; column++)
                {
                    detail.Cells[row, column].PutValue(row * 10 + column);
                }
            }

            workbook.Worksheets[workbook.Worksheets.Add()].Cells["A1"].PutValue("Total");
            workbook.Save(path);
        }

        AssertReadableWindowAndNext(workspace, path, "Detail", nextRows: true);
    }

    /// <summary>
    /// A sheet of 16000 columns, over the pixel budget, shows its first columns instead of
    /// failing the review with RENDER_TOO_LARGE, and the command renders the columns after them.
    /// </summary>
    [Fact]
    public void WideSheet_ShowsAReadableWindowInsteadOfFailingTheReview()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.File("wide.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Worksheet wide = workbook.Worksheets[0];
            wide.Name = "Wide";
            for (int column = 0; column < 16_000; column++)
            {
                for (int row = 0; row < 3; row++)
                {
                    wide.Cells[row, column].PutValue(column);
                }
            }

            workbook.Worksheets[workbook.Worksheets.Add()].Cells["A1"].PutValue("Total");
            workbook.Save(path);
        }

        AssertReadableWindowAndNext(workspace, path, "Wide", nextRows: false);
    }

    private void AssertReadableWindowAndNext(TempWorkspace workspace, string path, string sheet, bool nextRows)
    {
        var sink = new MemoryArtifactSink();
        ViewManifest manifest = CellsView.Render(_fixture.Session, path,
            new ViewRenderRequest { View = CellsViews.Sheets, MaxPartCount = 8, Purpose = ViewPurpose.Evidence },
            sink);

        Assert.Equal(2, manifest.Parts.Count);
        byte[] png = sink.Bytes(manifest.Parts[0].File);
        int width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        Assert.True(Math.Max(width, height) <= 10_000, $"The review image is {width} x {height} pixels.");
        Warning window = Assert.Single(manifest.Warnings ?? [], static warning => warning.Code.Name == "SHEET_PARTIALLY_RENDERED");
        Assert.Equal("sheet/" + sheet, window.Location);
        Assert.True(window.AffectsCompleteness);

        Match command = Regex.Match(window.Hint ?? string.Empty, "aspose-cli cells render \"[^\"]+\" --range \"'" + sheet + "'!([A-Z]+)([0-9]+):[A-Z]+[0-9]+\"");
        Assert.True(command.Success, window.Hint);
        // The next window starts below the rows shown, or right of the columns shown.
        (string column, string row) = (command.Groups[1].Value, command.Groups[2].Value);
        Assert.Equal(nextRows ? "A" : "1", nextRows ? column : row);
        Assert.NotEqual(nextRows ? "1" : "A", nextRows ? row : column);

        CliResult next = workspace.RunCommandLine(command.Value + " --out next.png --output-format json");
        Assert.True(next.ExitCode == 0, next.StdErr);
        JsonNode result = JsonNode.Parse(next.StdOut)!;
        Assert.DoesNotContain(result["warnings"]?.AsArray() ?? [], static warning => warning!["code"]!.GetValue<string>() == "RENDER_IMAGE_LARGE");
    }
}
