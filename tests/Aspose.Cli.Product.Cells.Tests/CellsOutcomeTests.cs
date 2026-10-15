using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
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

    [Fact]
    public void ASheetRenamedLaterInTheBatch_NamesEarlierTargetsByItsNewName()
    {
        string path = _fixture.CreateSalesWorkbook($"renamed-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"A20","values":[[1]]},
              {"op":"set_tab_color","sheet":"Data","color":"#FF0000"},
              {"op":"rename_sheet","sheet":"Data","name":"Q3 Data"}
            ]}
            """);

        Assert.Equal(["cell/Q3 Data!A20"], result.Applied[0].Targets);
        Assert.Equal(["tab-color/Q3 Data"], result.Applied[1].Targets);
        Assert.Equal(["sheet/Q3 Data"], result.Applied[2].Targets);
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
            [["tab-color/%20lead%20"], ["cell/P&L 100%25!B2"], ["comment/a%21b!C3"], ["cell/数据 表!A1"], ["row/P&L 100%25!1"], ["sheet/Extra"], ["chart/P&L 100%25/1"]],
            result.Applied.Select(static outcome => outcome.Targets));
    }

    /// <summary>
    /// An edit's targets and its verification name each object by the names a read of the output
    /// reports, escaped as an address: white space at the ends, '%', '/' and the '!' that ends a
    /// sheet included.
    /// </summary>
    [Fact]
    public void TargetsAndVerification_NameObjectsAsAReadOfTheOutputDoes()
    {
        string path = Workbook("read-names", static workbook =>
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = " Q3 100%! ";
            sheet.Cells["A1"].PutValue("Region");
            sheet.Cells["B1"].PutValue("Sales");
            sheet.Cells["A2"].PutValue("East");
            sheet.Cells["B2"].PutValue(3);
            sheet.Cells["A3"].PutValue("West");
            sheet.Cells["B3"].PutValue(5);
        });

        CellsEditResult edited = Apply(path, """
            {"ops":[
              {"op":"set_values","sheet":" Q3 100%! ","range":"B2","values":[[9]]},
              {"op":"insert_pivot","sheet":" Q3 100%! ","sourceRange":"A1:B3","at":"E2","name":" P/1 100%! ","rows":["Region"],"values":[{"field":"Sales"}]}
            ]}
            """, verify: true);
        CellsWorkbookSummary read = CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = edited.Output!.Path,
            Details = [CellsInspectDetails.Pivots],
        }).Workbook;

        string sheet = Assert.Single(read.Sheets).Name;
        CellsPivotInfo pivot = Assert.Single(read.Pivots!);
        Assert.Equal((" Q3 100%! ", " Q3 100%! ", " P/1 100%! "), (sheet, pivot.Sheet, pivot.Name));
        Assert.Equal(
            [["cell/%20Q3 100%25%21%20!B2"], ["pivot/%20Q3 100%25!%20/%20P%2F1 100%25!%20"]],
            edited.Applied.Select(static outcome => outcome.Targets));
        CellsVerifiedCellChange change = Assert.Single(edited.Verification!.DirectChanges, static change => change.Cell == "B2");
        Assert.Equal(sheet, change.Sheet);
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
              {"op":"add_sparkline","sheet":"Data","dataRange":"B5:C7","at":"D5:D7"},
              {"op":"insert_rows","sheet":"Data","at":6}
            ]}
            """);

        CellsSparklineInfo group = Assert.Single(CellsInfo.Run(_fixture.Session, new CellsInspectRequest
        {
            Input = seeded.Output!.Path,
            Details = [CellsInspectDetails.Sparklines],
        }).Workbook.Sparklines!);

        Assert.Equal(("B5:C5,B7:C7,B8:C8", "D5,D7,D8"), (group.DataRange, group.At));
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

    private const string TwoValidations = """
        {"op":"set_validation","sheet":"Data","range":"E2:E8","kind":"list","listItems":["a","b"]},
        {"op":"set_validation","sheet":"Data","range":"G2:G4,I2:I4","kind":"whole-number","operator":"greater-than","value1":"0"}
        """;

    private const string OneRule = """
        {"op":"add_conditional_format","sheet":"Data","range":"B5:D7","rule":{"kind":"cell-value","operator":"greater-than","value1":"1"},"matchStyle":{"bold":true}},
        {"op":"add_conditional_format","sheet":"Data","range":"F5:F7","rule":{"kind":"cell-value","operator":"less-than","value1":"1"},"matchStyle":{"italic":true}}
        """;

    /// <summary>
    /// An operation that changes the cells a validation or a conditional format covers changes
    /// that one object once, at its address in the output, or in the input when it is gone.
    /// </summary>
    [Theory]
    [InlineData(TwoValidations, """{"op":"clear_validation","sheet":"Data","range":"I2:I4,E2:E3"}""", new[] { "validation/Data!E4:E8", "validation/Data!G2:G4" })]
    [InlineData(TwoValidations, """{"op":"clear_validation","sheet":"Data","range":"E2:E8"}""", new[] { "validation/Data!E2:E8" })]
    [InlineData(TwoValidations, """{"op":"clear_validation","sheet":"Data","range":"E5"}""", new[] { "validation/Data!E2:E4,E6:E8" })]
    [InlineData(TwoValidations, """{"op":"set_validation","sheet":"Data","range":"E4:E5","kind":"list","listItems":["c"]}""", new[] { "validation/Data!E2:E3,E6:E8", "validation/Data!E4:E5" })]
    [InlineData(TwoValidations, """{"op":"set_validation","sheet":"Data","range":"E2:E8,G2:G4","kind":"list","listItems":["a","b"]}""", new[] { "validation/Data!E2:E8,G2:G4", "validation/Data!E2:E8", "validation/Data!I2:I4" })]
    [InlineData(OneRule, """{"op":"clear_conditional_formats","sheet":"Data","range":"C5:C7"}""", new[] { "conditional-format/Data/1" })]
    [InlineData(OneRule, """{"op":"clear_conditional_formats","sheet":"Data","range":"C5:C7,F5:F7"}""", new[] { "conditional-format/Data/1", "conditional-format/Data/2" })]
    public void ChangingTheCellsOfAValidationOrAConditionalFormat_CountsEachObjectOnce(string seed, string operation, string[] targets)
    {
        string path = _fixture.CreateSalesWorkbook($"areas-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, $$"""{"ops":[{{seed}}]}""");

        BoundedOperationOutcome outcome = Assert.Single(Apply(seeded.Output!.Path, $$"""{"ops":[{{operation}}]}""").Applied);

        Assert.Equal(targets.Order(StringComparer.Ordinal), outcome.Targets.Order(StringComparer.Ordinal));
        Assert.Equal(targets.Length, outcome.ItemsAffected);
    }

    [Theory]
    [InlineData("set_validation", """ "kind":"list","listItems":["a"] """)]
    [InlineData("clear_validation", "")]
    [InlineData("add_conditional_format", """ "rule":{"kind":"duplicates"},"matchStyle":{"bold":true} """)]
    [InlineData("clear_conditional_formats", "")]
    [InlineData("set_print_area", "")]
    public void ARangeListWithAnEmptyOrQualifiedPart_IsRefusedWithTheListForm(string op, string fields)
    {
        string path = _fixture.CreateSalesWorkbook($"bad-list-{Guid.NewGuid():N}.xlsx");
        string rest = fields.Trim().Length == 0 ? string.Empty : "," + fields.Trim();

        foreach (string range in new[] { "B2:B4,,D2:D4", "B2:B4,D2:D4:E5", "Data!B2:B4" })
        {
            Aspose.Cli.Sdk.Errors.CliException refused = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
                Apply(path, $$"""{"ops":[{"op":"{{op}}","sheet":"Data","range":"{{range}}"{{rest}}}]}"""));

            Assert.Equal("OPS_INVALID", refused.Code.Name);
            Assert.Contains("separated by commas, such as F2:F10 or D5,D7,D8", refused.Message, StringComparison.Ordinal);
        }
    }

    private const string SparklineData = """
        {"op":"set_values","sheet":"Data","range":"E2","values":[[1,2],[null,null],[3,4],[5,6]]}
        """;

    private const string Sparklines = """
        {"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"column","color":"#2E75B6"}
        """;

    private IReadOnlyList<CellsSparklineInfo> SparklinesOf(string path) =>
        CellsInfo.Run(_fixture.Session, new CellsInspectRequest { Input = path, Details = [CellsInspectDetails.Sparklines] }).Workbook.Sparklines ?? [];

    [Fact]
    public void AddingTheSparklinesTheCellsAlreadyHold_ChangesNothingAndSaysSo()
    {
        string path = _fixture.CreateSalesWorkbook($"sparkline-same-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, $$"""{"ops":[{{SparklineData}},{{Sparklines}}]}""");

        CellsEditResult again = Apply(seeded.Output!.Path, $$"""{"ops":[{{Sparklines}}]}""");

        Assert.Equal(0, Assert.Single(again.Applied).ItemsAffected);
        Assert.Contains(again.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Single(SparklinesOf(again.Output!.Path));
    }

    [Theory]
    [InlineData("""{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"line","color":"#2E75B6"}""", new[] { "G2", "G4:G5" })]
    [InlineData("""{"op":"add_sparkline","sheet":"Data","dataRange":"E4:F4","at":"G4"}""", new[] { "G2", "G4:G5" })]
    [InlineData("""{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"column"}""", new[] { "G2", "G4:G5" })]
    [InlineData("""{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F5","at":"H2:H5","kind":"winloss"}""", new[] { "H2:H5" })]
    public void AddingSparklinesOverCellsAnotherGroupIsDrawnIn_IsRefusedWithTheOperationsThatClearIt(string operation, string[] cleared)
    {
        string path = _fixture.CreateSalesWorkbook($"sparkline-overlap-{Guid.NewGuid():N}.xlsx");
        string seeded = Apply(path, $$"""{"ops":[{{SparklineData}},{{Sparklines}},{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F5","at":"H2:H5"}]}""").Output!.Path;

        Aspose.Cli.Sdk.Errors.CliException refused = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() => Apply(seeded, $$"""{"ops":[{{operation}}]}"""));

        // The hint gives the operations that clear the group, ready to run, and says they clear the cells' values too.
        Assert.Equal("OPS_INVALID", refused.Code.Name);
        Assert.Contains("sparkline group", refused.Message, StringComparison.Ordinal);
        Assert.Contains("values", refused.Hint, StringComparison.Ordinal);
        string[] clears = [.. System.Text.RegularExpressions.Regex.Matches(refused.Hint!, """\{"op":"clear_range"[^{}]*\}""").Select(static match => match.Value)];
        Assert.Equal(cleared, clears.Select(static op => System.Text.Json.Nodes.JsonNode.Parse(op)!["range"]!.GetValue<string>()));
        CellsEditResult clearedGroup = Apply(seeded, $$"""{"ops":[{{string.Join(",", clears)}}]}""");
        Assert.Single(SparklinesOf(clearedGroup.Output!.Path));
        CellsEditResult added = Apply(clearedGroup.Output!.Path, $$"""{"ops":[{{operation}}]}""");
        Assert.Equal(1, Assert.Single(added.Applied).ItemsAffected);
        Assert.Equal(2, SparklinesOf(added.Output!.Path).Count);
    }

    [Fact]
    public void AddingSparklinesWithoutAColorOverAGroupOfAnotherColor_IsRefused()
    {
        string path = _fixture.CreateSalesWorkbook($"sparkline-color-{Guid.NewGuid():N}.xlsx");
        string seeded = Apply(path, $$"""{"ops":[{{SparklineData}},{{Sparklines}}]}""").Output!.Path;

        Aspose.Cli.Sdk.Errors.CliException refused = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() => Apply(seeded,
            """{"ops":[{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"column"}]}"""));

        Assert.Equal("OPS_INVALID", refused.Code.Name);
    }

    [Fact]
    public void AddingSparklinesWithoutAColorOverTheSameGroupInTheDefaultColor_ChangesNothing()
    {
        const string plain = """{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"column"}""";
        string path = _fixture.CreateSalesWorkbook($"sparkline-default-{Guid.NewGuid():N}.xlsx");
        string seeded = Apply(path, $$"""{"ops":[{{SparklineData}},{{plain}}]}""").Output!.Path;

        CellsEditResult again = Apply(seeded, $$"""{"ops":[{{plain}}]}""");

        Assert.Equal(0, Assert.Single(again.Applied).ItemsAffected);
        Assert.Contains(again.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
    }

    /// <summary>A workbook whose theme's first accent is red, so the default series color is not Office's.</summary>
    private string RedThemeWorkbook() => Workbook("red-theme", static workbook =>
    {
        workbook.Worksheets[0].Name = "Data";
        workbook.SetThemeColor(ThemeColorType.Accent1, System.Drawing.Color.FromArgb(255, 0, 0));
    });

    [Fact]
    public void AddingSparklinesWithoutAColorAgainInAWorkbookOfAnotherTheme_ChangesNothing()
    {
        const string plain = """{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"column"}""";
        string seeded = Apply(RedThemeWorkbook(), $$"""{"ops":[{{SparklineData}},{{plain}}]}""").Output!.Path;

        CellsEditResult again = Apply(seeded, $$"""{"ops":[{{plain}}]}""");
        CellsEditResult twice = Apply(RedThemeWorkbook(), $$"""{"ops":[{{SparklineData}},{{plain}},{{plain}}]}""");

        Assert.NotEqual("#4472C4", Assert.Single(SparklinesOf(seeded)).Color);
        Assert.Equal(0, Assert.Single(again.Applied).ItemsAffected);
        Assert.Contains(again.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal(0, twice.Applied[^1].ItemsAffected);
    }

    [Fact]
    public void AddingSparklinesWithoutAColorOverAGroupOfAColorGivenInAWorkbookOfAnotherTheme_IsRefused()
    {
        string seeded = Apply(RedThemeWorkbook(), $$"""{"ops":[{{SparklineData}},{{Sparklines}}]}""").Output!.Path;

        Aspose.Cli.Sdk.Errors.CliException refused = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() => Apply(seeded,
            """{"ops":[{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2,E4:F4,E5:F5","at":"G2,G4,G5","kind":"column"}]}"""));

        Assert.Equal("OPS_INVALID", refused.Code.Name);
    }

    [Fact]
    public void AddingSparklinesOverSeveralGroups_IsRefusedWithTheOperationsThatClearEachOfThem()
    {
        string path = _fixture.CreateSalesWorkbook($"sparkline-several-{Guid.NewGuid():N}.xlsx");
        string seeded = Apply(path, $$"""{"ops":[{{SparklineData}},{{Sparklines}},{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F5","at":"H2:H5"}]}""").Output!.Path;
        const string across = """{"op":"add_sparkline","sheet":"Data","dataRange":"E2:F2","at":"G2:H2"}""";

        Aspose.Cli.Sdk.Errors.CliException refused = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() => Apply(seeded, $$"""{"ops":[{{across}}]}"""));
        string[] clears = [.. System.Text.RegularExpressions.Regex.Matches(refused.Hint!, """\{"op":"clear_range"[^{}]*\}""").Select(static match => match.Value)];

        Assert.Equal(["G2", "G4:G5", "H2:H5"], clears.Select(static op => System.Text.Json.Nodes.JsonNode.Parse(op)!["range"]!.GetValue<string>()));
        string cleared = Apply(seeded, $$"""{"ops":[{{string.Join(",", clears)}}]}""").Output!.Path;
        Assert.Empty(SparklinesOf(cleared));
        Assert.Equal(1, Assert.Single(Apply(cleared, $$"""{"ops":[{{across}}]}""").Applied).ItemsAffected);
    }

    [Theory]
    [InlineData("G2:G5", 0)]
    [InlineData("G4", 1)]
    public void ClearingAllOfARange_RemovesTheSparklinesDrawnInIt(string range, int left)
    {
        string path = _fixture.CreateSalesWorkbook($"sparkline-clear-{Guid.NewGuid():N}.xlsx");
        CellsEditResult seeded = Apply(path, $$"""{"ops":[{{SparklineData}},{{Sparklines}}]}""");

        CellsEditResult contents = Apply(seeded.Output!.Path, $$"""{"ops":[{"op":"clear_range","sheet":"Data","range":"{{range}}"}]}""");
        CellsEditResult all = Apply(seeded.Output!.Path, $$"""{"ops":[{"op":"clear_range","sheet":"Data","range":"{{range}}","what":"all"}]}""");

        Assert.Single(SparklinesOf(contents.Output!.Path));
        BoundedOperationOutcome outcome = Assert.Single(all.Applied);
        Assert.Equal(["sparkline-group/Data/1"], outcome.Targets);
        Assert.Equal(1, outcome.ItemsAffected);
        IReadOnlyList<CellsSparklineInfo> after = SparklinesOf(all.Output!.Path);
        Assert.Equal(left, after.Count);
        if (left == 1)
        {
            Assert.Equal(("E2:F2,E5:F5", "G2,G5"), (after[0].DataRange, after[0].At));
        }
    }

    [Fact]
    public void ShowingAHiddenSheet_ChangesItsVisibilityAndLeavesTheActiveSheet()
    {
        string path = _fixture.CreateSalesWorkbook($"show-sheet-{Guid.NewGuid():N}.xlsx");

        // The second operation names no sheet, so it writes to the sheet active after the first.
        CellsEditResult result = Apply(path, """
            {"ops":[
              {"op":"set_sheet_visibility","sheet":"Backstage","visibility":"visible"},
              {"op":"set_values","range":"A9","values":[["after"]]}
            ]}
            """);

        Assert.Equal([["sheet/Backstage"], ["cell/Data!A9"]], result.Applied.Select(static outcome => outcome.Targets));
    }

    [Fact]
    public void HidingTheActiveSheet_NamesTheActiveSheetItMovesOn()
    {
        string path = _fixture.CreateSalesWorkbook($"hide-active-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """{"ops":[{"op":"set_sheet_visibility","sheet":"Data","visibility":"hidden"}]}""");

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(["sheet/Data", "workbook/active-sheet"], outcome.Targets);
        Assert.Equal(2, outcome.ItemsAffected);
    }

    [Theory]
    [InlineData("Second", "very-hidden")]
    [InlineData("Second", "hidden")]
    [InlineData("Backstage", "visible")]
    public void SheetVisibility_ReadsBackAsWritten(string sheet, string visibility)
    {
        string path = _fixture.CreateSalesWorkbook($"visibility-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, $$"""{"ops":[{"op":"set_sheet_visibility","sheet":"{{sheet}}","visibility":"{{visibility}}"}]}""");

        CellsInspectResult read = CellsInfo.Run(_fixture.Session, new CellsInspectRequest { Input = result.Output!.Path });
        Assert.Equal(visibility, read.Workbook.Sheets.Single(info => info.Name == sheet).Visibility);
    }

    [Fact]
    public void HidingTheLastVisibleSheet_IsRefused()
    {
        string path = _fixture.CreateSalesWorkbook($"hide-last-{Guid.NewGuid():N}.xlsx");

        CliException error = Assert.Throws<CliException>(() => Apply(path, """
            {"ops":[
              {"op":"set_sheet_visibility","sheet":"Second","visibility":"hidden"},
              {"op":"set_sheet_visibility","sheet":"Data","visibility":"very-hidden"}
            ]}
            """));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains("only visible sheet", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACamelCaseVisibility_SuggestsTheKebabCaseValueFirst()
    {
        CliException error = Assert.Throws<CliException>(() => Apply(
            _fixture.CreateSalesWorkbook($"camel-{Guid.NewGuid():N}.xlsx"),
            """{"ops":[{"op":"set_sheet_visibility","sheet":"Second","visibility":"veryHidden"}]}"""));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Equal("very-hidden", error.Details!["suggestions"]![0]!.GetValue<string>());
    }

    [Fact]
    public void TheBooleanHiddenField_SuggestsVisibility()
    {
        CliException error = Assert.Throws<CliException>(() => CellsOp.Catalog.Parse<CellsOpsBatch>(
            """{"ops":[{"op":"set_sheet_visibility","sheet":"Second","hidden":true}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains("visibility", error.Hint ?? error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacingText_CountsTheCellsItChangedAndExplainsNoMatch()
    {
        string path = _fixture.CreateSalesWorkbook($"replace-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"E1","values":[["Total due"]]},
              {"op":"replace_text","sheet":"Data","find":"total","replace":"Sum"},
              {"op":"replace_text","sheet":"Data","range":"A1:A2","find":"Sum","replace":"Total"}
            ]}
            """, verify: true);

        Assert.Equal([["cell/Data!E1"], ["cell/Data!E1", "cell/Data!A3"], []], result.Applied.Select(static outcome => outcome.Targets));
        Assert.Equal(0, result.Applied[2].ItemsAffected);
        Warning noMatch = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal("no-match", noMatch.Details!["reason"]!.ToString());
        Assert.DoesNotContain("Sum", noMatch.Message, StringComparison.Ordinal);
        Assert.Contains("op-0002", result.Verification!.CheckedOps);
    }

    [Fact]
    public void ReplacingWithAnInvalidRegex_IsRefusedBeforeAnythingRuns()
    {
        CliException error = Assert.Throws<CliException>(() => CellsOp.Catalog.Parse<CellsOpsBatch>(
            """{"ops":[{"op":"replace_text","find":"(open","replace":"x","regex":true}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains("regular expression", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawingABorderOnARange_NamesTheNeighborsThatShareIt()
    {
        string path = _fixture.CreateSalesWorkbook($"shared-border-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """
            {"ops":[{"op":"format_range","sheet":"Data","range":"A2:B2","style":{"borders":[{"side":"bottom","lineStyle":"thin","color":"#000000"}]}}]}
            """);

        BoundedOperationOutcome outcome = Assert.Single(result.Applied);
        Assert.Equal(["cell/Data!A2", "cell/Data!B2", "cell/Data!A3", "cell/Data!B3"], outcome.Targets);
        // A read reports the line as the top border of the cells below.
        CellsReadResult read = CellsRead.Run(_fixture.Session, new CellsReadRequest
        {
            Input = result.Output!.Path, SheetName = "Data", Range = A1.ParseRange("A3:B3").Range, Content = CellsReadContents.Styles,
        });
        Assert.All(read.Sheet.Cells![0], cell =>
            Assert.Contains(read.Styles![cell.StyleId!].Borders!, static border => border.Side == "top"));
    }

    [Fact]
    public void CenteringWithAnIndent_IsRefusedWithWhatToChange()
    {
        CliException error = Assert.Throws<CliException>(() => CellsOp.Catalog.Parse<CellsOpsBatch>(
            """{"ops":[{"op":"format_range","range":"A1","style":{"alignment":"center","indent":1}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains("left or right", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommonNameForTheNumberFormat_SuggestsNumberFormat()
    {
        CliException error = Assert.Throws<CliException>(() => CellsOp.Catalog.Parse<CellsOpsBatch>(
            """{"ops":[{"op":"format_range","range":"A1","style":{"numFmt":"0.00"}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains("numberFormat", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void HidingDeletingResizingAndCharting_CountWhatTheyChanged()
    {
        string path = _fixture.CreateSalesWorkbook($"c-b01-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = Apply(path, """
            {"ops":[
              {"op":"set_sheet_visibility","sheet":"Second","visibility":"hidden"},
              {"op":"delete_sheet","sheet":"Backstage"},
              {"op":"resize_columns","sheet":"Data","first":"A","last":"B","widthChars":20},
              {"op":"insert_chart","sheet":"Data","kind":"column","dataRange":"A1:C2","at":"E2:K12"}
            ]}
            """);

        Assert.Equal(
            [(1L, (IReadOnlyList<string>)["sheet/Second"]), (1L, ["sheet/Backstage"]), (2L, ["column/Data!A", "column/Data!B"]), (1L, ["chart/Data/1"])],
            result.Applied.Select(static outcome => (outcome.ItemsAffected, outcome.Targets)));
    }

    [Theory]
    [InlineData("""{"op":"insert_rows","sheet":"Data","at":1048575,"count":5}""", "rows 1048575-1048579 run past the sheet's last row")]
    [InlineData("""{"op":"delete_rows","sheet":"Data","at":1048575,"count":5}""", "rows 1048575-1048579 run past the sheet's last row")]
    [InlineData("""{"op":"insert_rows","sheet":"Data","at":1048577}""", "at must be at most 1048576")]
    [InlineData("""{"op":"insert_columns","sheet":"Data","at":"XFD","count":5}""", "5 columns from XFD run past the sheet's last column")]
    [InlineData("""{"op":"delete_columns","sheet":"Data","at":"XFC","count":3}""", "3 columns from XFC run past the sheet's last column")]
    public void LinesPastTheSheetsEnd_AreAnInvalidOperation(string operation, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => CellsOp.Catalog.Parse<CellsOpsBatch>(
            $$"""{"ops":[{{operation}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"op":"set_values","sheet":"Data","range":"A1048576","values":[[1]]},{"op":"insert_rows","sheet":"Data","at":3,"count":2}""", "push the data in row 1048576 past")]
    [InlineData("""{"op":"set_values","sheet":"Data","range":"XFD1","values":[[1]]},{"op":"insert_columns","sheet":"Data","at":"B"}""", "push the data in column XFD past")]
    public void InsertingLinesThatPushDataOffTheSheet_IsAnInvalidOperationTheBatchCanSkip(string operations, string reason)
    {
        string path = _fixture.CreateSalesWorkbook($"push-off-{Guid.NewGuid():N}.xlsx");

        CellsEditResult result = CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>($$"""{"ops":[{{operations}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"push-off-{Guid.NewGuid():N}.xlsx")),
            Options = new EditCommandOptions { BestEffort = true },
        });

        BoundedOperationOutcome refused = result.Applied[1];
        Assert.Equal(OpStatuses.Failed, refused.Status);
        Assert.Equal("OPS_INVALID", refused.Error!.Code);
        Assert.Contains(reason, refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(OpStatuses.Ok, result.Applied[0].Status);
    }

    [Fact]
    public void AnImageFileThatIsMissing_IsFileNotFoundNamingItsOperationAndPath()
    {
        string path = _fixture.CreateSalesWorkbook($"missing-image-{Guid.NewGuid():N}.xlsx");
        string image = _fixture.Temp.File($"missing-{Guid.NewGuid():N}.png");

        CliException error = Assert.Throws<CliException>(() => Apply(path,
            $$"""{"ops":[{"id":"logo","op":"insert_image","sheet":"Data","at":"B2","path":{{System.Text.Json.JsonSerializer.Serialize(image)}}}]}"""));

        Assert.Equal("FILE_NOT_FOUND", error.Code.Name);
        Assert.Equal(image, error.Details!["path"]!.GetValue<string>());
        Assert.Equal("logo", error.Details["id"]!.GetValue<string>());
        Assert.Equal("insert_image", error.Details["op"]!.GetValue<string>());
    }
}
