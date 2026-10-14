using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// An edit counts a change the engine keeps even where a read reports the object more coarsely
/// than the workbook holds it, and counts nothing where only what a read adds differs: what an
/// operation counts is decided by the workbook's own state, of which a read is one view.
/// </summary>
public sealed class CellsSnapshotFidelityTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsSnapshotFidelityTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void FreezingAtTheSameCellWithOtherFrozenCounts_CountsThePanes()
    {
        string path = Workbook("frozen", static sheet => sheet.FreezePanes(3, 0, 1, 0));

        Assert.Equal(1, Affected(path, """{"op":"freeze_panes","sheet":"Data","cell":"A4"}"""));
    }

    [Fact]
    public void ProtectingTheStructureOfAWorkbookProtectedWhole_CountsTheProtection()
    {
        string path = Workbook("protected-all", static sheet => sheet.Workbook.Protect(ProtectionType.All, null));

        Assert.Equal(1, Affected(path, """{"op":"protect_workbook"}"""));
    }

    [Fact]
    public void GivingARowTheHeightItIsFittedTo_ChangesNothing()
    {
        string path = Workbook("fitted", static sheet => sheet.Cells["A6"].PutValue("x"));
        double fitted = Height(path, 5);

        Assert.Equal(0, Affected(path, $$"""{"op":"resize_rows","sheet":"Data","first":6,"height":{{fitted.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""));
    }

    [Fact]
    public void CopyingAnAlignmentOutsideTheVocabulary_CountsTheCell()
    {
        string path = Workbook("justify", static sheet =>
        {
            Align(sheet.Cells["A1"], TextAlignmentType.Justify);
            Align(sheet.Cells["B1"], TextAlignmentType.Distributed);
        });

        Assert.Equal(1, Affected(path, """{"op":"copy_range","sheet":"Data","from":"A1","to":"B1"}"""));
    }

    [Fact]
    public void HidingTheLabelsOfEverySeries_CountsAChartWhoseSecondSeriesShowedThem()
    {
        string path = Workbook("labels", static sheet =>
        {
            Fill(sheet);
            Chart chart = sheet.Charts[sheet.Charts.Add(ChartType.Column, 0, 15, 10, 22)];
            chart.NSeries.Add("=Data!B1:B3", true);
            chart.NSeries.Add("=Data!C1:C3", true);
            chart.NSeries[1].DataLabels.ShowValue = true;
        });

        Assert.Equal(1, Affected(path, """{"op":"set_chart","sheet":"Data","index":1,"dataLabels":{"visible":false}}"""));
    }

    [Fact]
    public void InsertingARowInsideWhatAChartPlots_CountsTheChartWhenOnlyOneSeriesMoves()
    {
        string path = Workbook("series", static sheet =>
        {
            Worksheet other = sheet.Workbook.Worksheets[sheet.Workbook.Worksheets.Add()];
            other.Name = "Other";
            Chart chart = sheet.Charts[sheet.Charts.Add(ChartType.Column, 0, 15, 2, 22)];
            chart.NSeries.Add("=Other!A1:A30", true);
            chart.NSeries.Add("=Data!B10:B11", true);
        });

        BoundedOperationOutcome outcome = Outcome(path, """{"op":"insert_rows","sheet":"Data","at":5}""");

        Assert.Contains("chart/Data/1", outcome.Targets);
    }

    [Fact]
    public void DeletingTheFirstOfTwoCharts_CountsOnlyTheDeletedOne()
    {
        string path = Workbook("two-charts", Fill);
        string charted = Edit(path, """
            {"op":"insert_chart","sheet":"Data","kind":"column","dataRange":"A1:B3","at":"E2:J12"},
            {"op":"insert_chart","sheet":"Data","kind":"line","dataRange":"A1:C3","at":"E14:J24"}
            """).Output!.Path;

        BoundedOperationOutcome outcome = Outcome(charted, """{"op":"delete_chart","sheet":"Data","index":1}""");

        Assert.Equal(1L, outcome.ItemsAffected);
        Assert.Equal(["chart/Data/1"], outcome.Targets);
    }

    private static void Fill(Worksheet sheet)
    {
        sheet.Cells["A1"].PutValue("k");
        sheet.Cells["B1"].PutValue("b");
        sheet.Cells["C1"].PutValue("c");
        for (int row = 1; row < 3; row++)
        {
            sheet.Cells[row, 0].PutValue("r" + row);
            sheet.Cells[row, 1].PutValue(row);
            sheet.Cells[row, 2].PutValue(row * 2);
        }
    }

    private static void Align(Cell cell, TextAlignmentType alignment)
    {
        cell.PutValue("x");
        Style style = cell.GetStyle();
        style.HorizontalAlignment = alignment;
        cell.SetStyle(style);
    }

    private static double Height(string path, int row)
    {
        using var workbook = new Workbook(path);
        return workbook.Worksheets["Data"].Cells.GetRowHeight(row);
    }

    private string Workbook(string name, Action<Worksheet> build)
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        build(sheet);
        string path = _fixture.Temp.File($"{name}-{Guid.NewGuid():N}.xlsx");
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    private long Affected(string path, string operation) => Outcome(path, operation).ItemsAffected;

    private BoundedOperationOutcome Outcome(string path, string operation) => Assert.Single(Edit(path, operation).Applied);

    private CellsEditResult Edit(string path, string operations) =>
        CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>($$"""{"ops":[{{operations}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"fidelity-{Guid.NewGuid():N}.xlsx")),
        });
}
