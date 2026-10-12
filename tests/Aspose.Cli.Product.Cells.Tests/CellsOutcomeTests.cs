using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// What an edit's outcome says it changed (C-B01): the objects whose state the operation changed,
/// counted from snapshots, an <c>OP_NO_EFFECT</c> warning for an operation that changed nothing,
/// and a verification that keeps the batch's own changes apart from recalculated results.
/// </summary>
public sealed class CellsOutcomeTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsOutcomeTests(CellsFixture fixture) => _fixture = fixture;

    private CellsEditResult Apply(string path, string operations, bool verify = false) =>
        CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>(operations, Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"outcome-{Guid.NewGuid():N}.xlsx")),
            Verify = verify,
        });

    [Fact]
    public void WritingTheValuesACellAlreadyHolds_ChangesNothingAndSaysSo()
    {
        string path = _fixture.CreateSalesWorkbook($"same-value-{Guid.NewGuid():N}.xlsx");
        CellsEditResult first = Apply(path, """{"ops":[{"op":"set_values","sheet":"Data","range":"A20","values":[[5,"five"]]}]}""");

        CellsEditResult again = Apply(first.Output!.Path, """{"ops":[{"op":"set_values","sheet":"Data","range":"A20","values":[[5,"five"]]}]}""");

        Assert.Equal(2, Assert.Single(first.Applied).ItemsAffected);
        Assert.Equal(0, Assert.Single(again.Applied).ItemsAffected);
        Assert.Contains(again.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Fact]
    public void FormattingAndWritingCells_CountTheCellsThatChanged()
    {
        string path = _fixture.CreateSalesWorkbook($"counts-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"A20","values":[[1,2,3]]},
              {"op":"format_range","sheet":"Data","range":"A20:Z20","style":{"bold":true}},
              {"op":"clear_range","sheet":"Data","range":"H40:K60"}
            ]}
            """);

        // A range counts the cells it changed, not its size: bold on A20:Z20 changes all 26 cells' style.
        Assert.Equal([3L, 26L, 0L], result.Applied.Select(static outcome => outcome.ItemsAffected));
        Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Theory]
    [InlineData("clear_autofilter")]
    [InlineData("clear_tab_color")]
    [InlineData("clear_print_area")]
    [InlineData("unfreeze_panes")]
    public void ClearingWhatTheSheetDoesNotHave_ChangesNothingAndSaysSo(string op)
    {
        string path = _fixture.CreateSalesWorkbook($"nothing-{op}-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, $$"""{"ops":[{"op":"{{op}}","sheet":"Data"}]}""");

        Assert.Equal(0, Assert.Single(result.Applied).ItemsAffected);
        Assert.Contains(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    [Fact]
    public void AWholeSheetOperation_LeavesRecalculatedResultsOutOfTheDirectChanges()
    {
        string path = _fixture.CreateSalesWorkbook($"cells-3-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, """
            {"ops":[{"op":"set_values","sheet":"Data","range":"A20","values":[[1]]},{"op":"set_formula","sheet":"Data","range":"B20","formula":"=A20*2"}]}
            """);

        CellsEditResult result = Apply(seeded.Output!.Path, """
            {"ops":[{"op":"set_values","sheet":"Data","range":"A20","values":[[3]]},{"op":"freeze_panes","sheet":"Data","cell":"B2"}]}
            """, verify: true);

        CellsEditVerification verification = result.Verification!;
        Assert.Equal(["A20"], verification.DirectChanges.Select(static change => change.Cell));
        Assert.Equal(["B20"], verification.FormulaResultChanges.Select(static change => change.Cell));
    }

    [Fact]
    public void AnOperationThatNamesNoSheet_IsVerifiedOnTheSheetActiveBeforeTheBatch()
    {
        string path = _fixture.CreateSalesWorkbook($"active-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """
            {"ops":[{"op":"set_values","range":"A30","values":[[9]]},{"op":"set_active_sheet","sheet":"Second"}]}
            """, verify: true);

        Assert.Contains("op-0001", result.Verification!.CheckedOps);
        Assert.Equal(["A30"], result.Verification.DirectChanges.Select(static change => change.Cell));
    }
    private string Workbook(string name, Action<Workbook> build)
    {
        using var workbook = new Workbook();
        build(workbook);
        string path = _fixture.Temp.File($"{name}-{Guid.NewGuid():N}.xlsx");
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    [Fact]
    public void SheetNamesInTargets_EscapeOnlyWhatWouldBreakTheAddress()
    {
        string path = Workbook("names", static workbook =>
        {
            workbook.Worksheets[0].Name = " lead ";
            workbook.Worksheets.Add("P&L 100%");
            workbook.Worksheets.Add("a!b");
            workbook.Worksheets.Add("数据 表");
            workbook.Worksheets["P&L 100%"].Cells["A1"].PutValue(1);
        });

        CellsEditResult result = Apply(path, """
            {"ops":[
              {"op":"set_tab_color","sheet":" lead ","color":"#FF0000"},
              {"op":"set_values","sheet":"P&L 100%","range":"B2","values":[[2]]},
              {"op":"add_comment","sheet":"a!b","cell":"C3","text":"note"},
              {"op":"set_values","sheet":"数据 表","range":"A1","values":[[3]]},
              {"op":"insert_rows","sheet":"P&L 100%","at":1},
              {"op":"insert_sheet","name":"Extra"},
              {"op":"insert_chart","sheet":"P&L 100%","kind":"column","dataRange":"A1:B2","at":"D2:H10"}
            ]}
            """);

        Assert.Equal(
            [["tab-color/%20lead%20"], ["cell/P&L 100%25!B2"], ["comment/a!b!C3"], ["cell/数据 表!A1"], ["row/P&L 100%25!1"], ["sheet/Extra"], ["chart/P&L 100%25/1"]],
            result.Applied.Select(static outcome => outcome.Targets));
    }

    [Fact]
    public void RenamingASheet_ChangesOneSheetAtItsNewName()
    {
        string path = _fixture.CreateSalesWorkbook($"rename-{Guid.NewGuid():N}.xlsx");

        BoundedOperationOutcome renamed = Assert.Single(Apply(path, """{"ops":[{"op":"rename_sheet","sheet":"Second","name":"Notes"}]}""").Applied);

        Assert.Equal(1, renamed.ItemsAffected);
        Assert.Equal(["sheet/Notes"], renamed.Targets);
    }

    [Theory]
    [InlineData("pie", """["#FF0000","#00FF00"]""")]
    [InlineData("scatter", """["#FF0000"]""")]
    public void RecoloringAPieOrScatterChart_CountsTheChart(string kind, string colors)
    {
        string path = _fixture.CreateSalesWorkbook($"recolor-{kind}-{Guid.NewGuid():N}.xlsx");
        CellsEditResult charted = Apply(path, $$"""{"ops":[{"op":"insert_chart","sheet":"Data","kind":"{{kind}}","dataRange":"A1:B3","at":"E2:K12"}]}""");

        CellsEditResult result = Apply(charted.Output!.Path, $$"""{"ops":[{"op":"set_chart","sheet":"Data","index":1,"seriesColors":{{colors}}}]}""");

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
    }

    [Fact]
    public void EditingALineChart_LeavesItWithoutMarkers()
    {
        string path = _fixture.CreateSalesWorkbook($"line-{Guid.NewGuid():N}.xlsx");
        CellsEditResult charted = Apply(path, """{"ops":[{"op":"insert_chart","sheet":"Data","kind":"line","dataRange":"A1:C2","at":"E2:K12"}]}""");

        CellsEditResult edited = Apply(charted.Output!.Path, """{"ops":[{"op":"set_chart","sheet":"Data","index":1,"title":"Trend"}]}""");

        CellsChartInfo chart = Assert.Single(CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = edited.Output!.Path,
            Details = [CellsInspectDetails.Charts],
        }).Workbook.Charts!);
        Assert.Equal(("line", "Trend"), (chart.Kind, chart.Title));
    }

    [Theory]
    [InlineData("""{"op":"insert_rows","sheet":"Data","at":20}""")]
    [InlineData("""{"op":"delete_rows","sheet":"Data","at":20}""")]
    [InlineData("""{"op":"insert_columns","sheet":"Data","at":"M"}""")]
    [InlineData("""{"op":"delete_columns","sheet":"Data","at":"M"}""")]
    public void InsertingOrDeletingBeforeAnchoredObjects_CountsWhatMoved(string operation)
    {
        string path = _fixture.CreateSalesWorkbook($"anchored-{Guid.NewGuid():N}.xlsx");
        CellsEditResult placed = Apply(path, """
            {"ops":[
              {"op":"insert_chart","sheet":"Data","kind":"column","dataRange":"A1:B2","at":"P30:T40"},
              {"op":"add_comment","sheet":"Data","cell":"Q45","text":"note"},
              {"op":"merge_cells","sheet":"Data","range":"Q50:R51"}
            ]}
            """);

        BoundedOperationOutcome moved = Assert.Single(Apply(placed.Output!.Path, $$"""{"ops":[{{operation}}]}""").Applied);

        Assert.True(moved.ItemsAffected > 0, $"{operation} counted {moved.ItemsAffected}");
    }

    [Fact]
    public void ConditionalFormatsOutsideTheVocabulary_ReadBackByEngineNameAndCountWhenCleared()
    {
        string path = Workbook("excel-rules", static workbook =>
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Data";
            FormatConditionCollection conditions = sheet.ConditionalFormattings[sheet.ConditionalFormattings.Add()];
            conditions.AddArea(CellArea.CreateCellArea(0, 0, 9, 0));
            conditions[conditions.AddCondition(FormatConditionType.DuplicateValues)].Style.Font.IsBold = true;
            int text = conditions.AddCondition(FormatConditionType.ContainsText);
            conditions[text].Text = "late";
        });

        IReadOnlyList<CellsConditionalFormatInfo> read = CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = path,
            Details = [CellsInspectDetails.ConditionalFormats],
        }).Workbook.ConditionalFormats!;
        CellsEditResult cleared = Apply(path, """{"ops":[{"op":"clear_conditional_formats","sheet":"Data","range":"A1:A10"}]}""");

        Assert.Equal(["duplicates", "contains-text"], read.Select(static format => format.Rule.Kind));
        Assert.Equal(read.Count, Assert.Single(cleared.Applied).ItemsAffected);
    }

    [Fact]
    public void APivotAggregationOutsideTheVocabulary_ReadsBackByEngineName()
    {
        string path = Workbook("std-dev", static workbook =>
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Data";
            sheet.Cells["A1"].PutValue("Region");
            sheet.Cells["B1"].PutValue("Sales");
            sheet.Cells["A2"].PutValue("East");
            sheet.Cells["B2"].PutValue(3);
            sheet.Cells["A3"].PutValue("West");
            sheet.Cells["B3"].PutValue(5);
            Aspose.Cells.Pivot.PivotTable pivot = sheet.PivotTables[sheet.PivotTables.Add("=Data!A1:B3", "E2", "Spread")];
            pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Row, "Region");
            pivot.DataFields[pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Data, "Sales")].Function = ConsolidationFunction.StdDev;
            pivot.CalculateData();
        });

        CellsPivotInfo pivot = Assert.Single(CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = path,
            Details = [CellsInspectDetails.Pivots],
        }).Workbook.Pivots!);

        Assert.Equal(("Sales", "stdDev"), (pivot.Values![0].Field, pivot.Values[0].Function));
    }
    [Fact]
    public void InsertingALineInsideASparklineGroup_ReadsBackTheCellsItIsDrawnIn()
    {
        string path = _fixture.CreateSalesWorkbook($"sparkline-gap-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, """
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"B5","values":[[1,2],[3,4],[5,6]]},
              {"op":"add_sparkline","sheet":"Data","dataRange":"B5:C7","location":"D5:D7"},
              {"op":"insert_rows","sheet":"Data","at":6}
            ]}
            """);

        CellsSparklineInfo group = Assert.Single(CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = seeded.Output!.Path,
            Details = [CellsInspectDetails.Sparklines],
        }).Workbook.Sparklines!);

        Assert.Equal(("B5:C5,B7:C7,B8:C8", "D5,D7,D8"), (group.DataRange, group.Location));
    }

    [Theory]
    [InlineData("""{"op":"insert_rows","sheet":"Data","at":3}""", new[] { "row/Data!3", "comment/Data!E11" })]
    [InlineData("""{"op":"delete_rows","sheet":"Data","at":3}""", new[] { "comment/Data!E9", "row/Data!3" })]
    [InlineData("""{"op":"insert_columns","sheet":"Data","at":"B"}""", new[] { "column/Data!B", "comment/Data!F10" })]
    [InlineData("""{"op":"insert_columns","sheet":"Data","at":"H"}""", new[] { "column/Data!H" })]
    public void InsertingOrDeletingALine_CountsItAndTheObjectsThatMoved(string operation, string[] targets)
    {
        string path = _fixture.CreateSalesWorkbook($"line-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, """{"ops":[{"op":"add_comment","sheet":"Data","cell":"E10","text":"note"}]}""");

        BoundedOperationOutcome outcome = Assert.Single(Apply(seeded.Output!.Path, $$"""{"ops":[{{operation}}]}""").Applied);

        Assert.Equal(targets, outcome.Targets);
        Assert.Equal(targets.Length, outcome.ItemsAffected);
    }

    [Fact]
    public void DeletingALineAnObjectDoesNotFollow_LeavesTheObjectOutOfTheCount()
    {
        string path = _fixture.CreateSalesWorkbook($"line-still-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, """
            {"ops":[{"op":"set_validation","sheet":"Data","range":"D1:D3","kind":"whole-number","operator":"greater-than","value1":"=0"}]}
            """);

        CellsEditResult deleted = Apply(seeded.Output!.Path, """{"ops":[{"op":"delete_rows","sheet":"Data","at":3}]}""");
        IReadOnlyList<CellsValidationInfo> read = CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = deleted.Output!.Path,
            Details = [CellsInspectDetails.Validation],
        }).Workbook.Validations!;

        // Whatever the engine did with the validation, the targets name only what the output holds.
        BoundedOperationOutcome outcome = Assert.Single(deleted.Applied);
        string range = Assert.Single(read).Range;
        Assert.Equal(range == "D1:D3" ? ["row/Data!3"] : ["row/Data!3", $"validation/Data!{range}"], outcome.Targets.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(outcome.Targets.Count, outcome.ItemsAffected);
    }

    [Theory]
    [InlineData("""{"op":"merge_cells","sheet":"Data","range":"Q3:R3"},{"op":"merge_cells","sheet":"Data","range":"Q4:R4"}""", "merge/Data!Q3:R3")]
    [InlineData("""{"op":"add_comment","sheet":"Data","cell":"E3","text":"gone"},{"op":"add_comment","sheet":"Data","cell":"E4","text":"moved"}""", "comment/Data!E3")]
    [InlineData("""{"op":"set_hyperlink","sheet":"Data","cell":"E3","url":"https://example.com/gone"},{"op":"set_hyperlink","sheet":"Data","cell":"E4","url":"https://example.com/moved"}""", "hyperlink/Data!E3")]
    [InlineData("""{"op":"set_validation","sheet":"Data","range":"E3","kind":"whole-number","operator":"greater-than","value1":"=0"},{"op":"set_validation","sheet":"Data","range":"E4","kind":"whole-number","operator":"less-than","value1":"=0"}""", "validation/Data!E3")]
    public void DeletingALine_CountsAnObjectItRemovesApartFromOneThatMovesIntoItsPlace(string seed, string place)
    {
        string path = _fixture.CreateSalesWorkbook($"line-replace-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, $$"""{"ops":[{{seed}}]}""");

        BoundedOperationOutcome outcome = Assert.Single(Apply(seeded.Output!.Path, """{"ops":[{"op":"delete_rows","sheet":"Data","at":3}]}""").Applied);

        // The object on row 3 is removed and the one on row 4 moves to row 3: two changes at one address.
        Assert.Equal(new[] { "row/Data!3", place }.Order(StringComparer.Ordinal), outcome.Targets.Order(StringComparer.Ordinal));
        Assert.Equal(3, outcome.ItemsAffected);
    }
}
