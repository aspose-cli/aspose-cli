using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Tests against the real Aspose.Cells engine (never mocked, per the team
/// rule). The in-process engine runs licensed only (see <see cref="CellsFixture"/>);
/// CellsCliTests covers what evaluation mode discloses.
/// </summary>
public sealed class CellsEngineTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsEngineTests(CellsFixture fixture) => _fixture = fixture;

    private static SheetInfo FindSheet(WorkbookInfoResult result, string name) =>
        Assert.Single(result.Workbook.Sheets, sheet => sheet.Name == name);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditOwnsDocumentBackupAndEvidenceAcrossSiblingDirectories(bool verify)
    {
        using var temp = new Aspose.Cli.TestKit.TempDirectory();
        string documents = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(documents);
        string source = Path.Combine(documents, "book.xlsx");
        File.Copy(_fixture.CreateSalesWorkbook($"backup-root-{verify}.xlsx"), source);
        byte[] original = File.ReadAllBytes(source);
        string backup = Path.Combine(temp.Path, "backups", "original.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Data","range":"B2","values":[[7]]}]}"""),
            new EditRequest
            {
                OutputPath = source, Overwrite = true, BackupPath = backup,
                Verify = verify,
            });
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.Equal(backup, result.Backup!.Path);
        Assert.Equal(source, result.Output!.Path);
        using var reopened = new Aspose.Cells.Workbook(source);
        Assert.Equal(7, reopened.Worksheets["Data"].Cells["B2"].IntValue);
        if (verify)
        {
            Assert.Equal("B2", Assert.Single(result.Verification!.DirectChanges).Cell);
        }
    }

    [Fact]
    public void RemoveDuplicates_ReportsTheRowsItRemoved()
    {
        string path = _fixture.CreateSalesWorkbook("dedupe-count.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(path,
            ParseOps("""
                {"ops":[
                  {"op":"add_sheet","name":"Orders"},
                  {"op":"set_values","sheet":"Orders","range":"A1","values":[["Order","Amount"],["A-1",10],["A-1",10],["A-2",20],["A-1",10]]},
                  {"op":"remove_duplicates","sheet":"Orders","range":"A1:B5","hasHeader":true}
                ]}
                """),
            new EditRequest { OutputPath = _fixture.Temp.File("dedupe-count.out.xlsx") });

        Assert.Equal(2, result.Applied.Single(static outcome => outcome.Op == "remove_duplicates").ItemsAffected);
    }

    [Fact]
    public void TextCreationReportsTheSameSheetLossAsConversionAndEditing()
    {
        CreateResult result = _fixture.Engine.Create(new NewWorkbookRequest
        {
            OutputPath = _fixture.Temp.File("multiple.csv"), SheetNames = ["One", "Two"],
        });
        Assert.Contains(result.Warnings ?? [], warning => warning.Code == "SHEETS_DROPPED" && warning.AffectsCompleteness);
    }

    [Fact]
    public void TextEditCommitsButDoesNotCertifyAnImplicitlyDroppedWorksheet()
    {
        string source = _fixture.CreateSalesWorkbook("text-loss.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Data","range":"B2","values":[[7]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("text-loss.csv"), Verify = true });
        Assert.True(File.Exists(result.Output!.Path));
        Warning dropped = Assert.Single(result.Warnings ?? [], warning => warning.Code == "SHEETS_DROPPED");
        Assert.False(result.Verification!.Ok);
        VerificationIssue issue = Assert.Single(result.Verification.Issues, issue => issue.Code == "SHEETS_DROPPED");
        Assert.Equal(dropped.Message, issue.Message);
        Assert.NotNull(issue.Hint);
        Assert.Equal(dropped.Hint, issue.Hint);
    }

    [Fact]
    public void VerificationForwardsACompletenessWarningWithItsLocationAndHint()
    {
        var located = new Warning
        {
            Code = "DATA_TRUNCATED", Message = "Data was discarded.", Hint = "Use xlsx.",
            Location = "'Data'!A1:C3", AffectsCompleteness = true,
        };
        var informational = new Warning { Code = "FORMULAS_CALCULATED_ON_OPEN", Message = "Recalculated." };

        VerificationIssue issue = Assert.Single(
            Aspose.Cli.Product.Cells.Engine.Editing.CellsEditVerifier.CompletenessIssues([located, informational]));

        Assert.Equal(VerificationIssue.From(located), issue);
        Assert.Equal("'Data'!A1:C3", issue.Location);
    }

    [Fact]
    public void VerificationReportsAFormulaErrorAtItsCell()
    {
        string source = _fixture.CreateSalesWorkbook("verify-formula-error.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_formula","sheet":"Data","range":"E5","formula":"=1/0"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-formula-error.out.xlsx"), Verify = true });

        Assert.False(result.Verification!.Ok);
        CellError error = Assert.Single(result.Verification.FormulaErrors);
        Assert.Equal(("Data", "E5"), (error.Sheet, error.Cell));
        VerificationIssue issue = Assert.Single(result.Verification.Issues);
        Assert.Equal("FORMULA_ERRORS", issue.Code);
        Assert.Equal("'Data'!E5", issue.Location);
    }

    [Fact]
    public void VerificationMarksAFormulaErrorTheInputAlreadyHad()
    {
        string source = _fixture.CreateSalesWorkbook("verify-preexisting-error.xlsx");
        using (var workbook = new Aspose.Cells.Workbook(source))
        {
            Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
            cells["E5"].Formula = "=1/0";
            cells["E6"].Formula = "=1/0";
            workbook.CalculateFormula();
            workbook.Save(source);
        }

        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_formula","sheet":"Data","range":"E6:E7","formula":"=2/0"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-preexisting-error.out.xlsx"), Verify = true });

        Assert.False(result.Verification!.Ok);
        Assert.Equal(
            [("E5", true), ("E6", (bool?)null), ("E7", null)],
            result.Verification.FormulaErrors.Select(static error => (error.Cell, error.Preexisting)));
        VerificationIssue issue = Assert.Single(result.Verification.Issues);
        Assert.Equal("FORMULA_ERRORS", issue.Code);
        Assert.EndsWith("1 of the listed error(s) were already in the input.", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerificationPointsACappedFormulaErrorListAtTheEditResult()
    {
        string source = _fixture.CreateSalesWorkbook("verify-formula-errors-capped.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_formula","sheet":"Second","range":"B1:B1001","formula":"=1/0"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-formula-errors-capped.out.xlsx"), Verify = true });

        EditVerification verification = result.Verification!;
        Assert.Equal(1000, verification.FormulaErrors.Count);
        VerificationIssue capped = Assert.Single(verification.Issues, issue => issue.Code == "LIST_TRUNCATED");
        Assert.Equal("verification.formulaErrors", capped.Location);
        Assert.Contains("'verification.formulaErrors'", capped.Message, StringComparison.Ordinal);
        VerificationIssue errors = Assert.Single(verification.Issues, issue => issue.Code == "FORMULA_ERRORS");
        Assert.Contains("1001 formula error(s)", errors.Message, StringComparison.Ordinal);
        Assert.Contains("first 1000", errors.Message, StringComparison.Ordinal);
        Assert.Null(errors.Location);
    }

    [Fact]
    public void VerificationOfALargeEditListsTheFirstChangesAndStillChecksEveryCell()
    {
        // A sort or a large write cannot be split; the change lists are capped, while the
        // formula-error scan that decides verification still covers the whole workbook.
        string source = _fixture.CreateSalesWorkbook("verify-diff-truncated.xlsx");
        string rows = string.Join(",", Enumerable.Range(1, 1001).Select(static value => $"[{value}]"));
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps($$"""{"ops":[{"op":"set_values","sheet":"Second","range":"B1","values":[{{rows}}]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-diff-truncated.out.xlsx"), Verify = true });

        Assert.True(result.Verification!.Ok);
        Assert.True(result.Verification.Truncated);
        Assert.Equal(1000, result.Verification.DirectChanges.Count);
        Assert.Empty(result.Verification.Issues);

        EditResult broken = _fixture.Engine.ApplyOps(source,
            ParseOps($$"""{"ops":[{"op":"set_values","sheet":"Second","range":"B1","values":[{{rows}}]},{"op":"set_formula","sheet":"Second","range":"C1002","formula":"=1/0"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-diff-truncated-error.out.xlsx"), Verify = true });

        Assert.False(broken.Verification!.Ok);
        Assert.Equal("'Second'!C1002", Assert.Single(broken.Verification.Issues).Location);
    }

    [Fact]
    public void VerificationCountsEveryCellAnAnchoredMatrixWroteAsRequested()
    {
        string source = _fixture.CreateSalesWorkbook("verify-anchored-matrix.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Data","range":"A5","values":[["West",900],["North",700]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-anchored-matrix.out.xlsx"), Verify = true });

        EditVerification verification = result.Verification!;
        Assert.Equal(["A5", "B5", "A6", "B6"], verification.DirectChanges.Select(static change => change.Cell));
        Assert.Empty(verification.OtherChanges);
        Assert.Equal("A5:B6", Assert.Single(verification.RequestedTargets).Range);
        Assert.Equal(["Data!A5:B6"], Assert.Single(result.Applied).Targets);
    }

    [Fact]
    public void VerificationListsTheCellChangesOfASheetTheBatchRenamed()
    {
        string source = _fixture.CreateSalesWorkbook("verify-renamed.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Second","range":"A1","values":[["before"]]},{"op":"rename_sheet","sheet":"Second","to":"Notes"},{"op":"set_values","sheet":"Notes","range":"B1","values":[["after"]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-renamed.out.xlsx"), Verify = true });

        EditVerification verification = result.Verification!;
        Assert.Equal(["Notes!A1", "Notes!B1"], verification.DirectChanges.Select(static change => $"{change.Sheet}!{change.Cell}"));
        VerificationOtherChange renamed = Assert.Single(verification.OtherChanges);
        Assert.Equal(("Notes", "renamed"), (renamed.Sheet, renamed.Status));
    }

    [Fact]
    public void VerificationExplainsANameErrorFromAnUnknownFunction()
    {
        string source = _fixture.CreateSalesWorkbook("verify-unknown-function.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_formula","sheet":"Data","range":"D2","formula":"=求和(B2:C2)"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-unknown-function.out.xlsx"), Verify = true });

        Assert.Equal("#NAME?", Assert.Single(result.Verification!.FormulaErrors).Error);
        Assert.Contains("English function names", Assert.Single(result.Verification.Issues).Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanVerificationReportsAnEmptyIssueList()
    {
        string source = _fixture.CreateSalesWorkbook("verify-clean.xlsx");
        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_values","sheet":"Data","range":"B2","values":[[7]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("verify-clean.out.xlsx"), Verify = true });

        Assert.True(result.Verification!.Ok);
        Assert.Empty(result.Verification.Issues);
    }

    [Fact]
    public void ALinkTheBatchStoresByFileNameAloneIsDisclosedOnce()
    {
        string source = _fixture.CreateSalesWorkbook("relative-link.xlsx");
        string beside = System.Text.Json.JsonSerializer.Serialize(
            "='" + Path.GetDirectoryName(source) + "\\[fx.xlsx]Rates'!$B$2");
        string output = _fixture.Temp.File("relative-link.out.xlsx");

        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps($$"""{"ops":[{"op":"set_formula","sheet":"Data","range":"E5","formula":{{beside}}},{"op":"set_formula","sheet":"Data","range":"E6","formula":"='C:\\far\\[far.xlsx]Rates'!$B$2"}]}"""),
            new EditRequest { OutputPath = output, Overwrite = true });
        EditResult again = _fixture.Engine.ApplyOps(output,
            ParseOps("""{"ops":[{"op":"set_formula","sheet":"Data","range":"E7","formula":"='[fx.xlsx]Rates'!$B$3"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("relative-link-again.out.xlsx"), Overwrite = true });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "EXTERNAL_LINK_RELATIVE");
        Assert.Contains("fx.xlsx", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("far.xlsx", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(again.Warnings ?? [], static warning => warning.Code == "EXTERNAL_LINK_RELATIVE");
    }

    [Fact]
    public void ALinkNamedLikeASheetSuggestsTheSheet()
    {
        string source = _fixture.CreateSalesWorkbook("sheet-typo.xlsx");

        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""{"ops":[{"op":"set_formula","sheet":"Data","range":"E5","formula":"='Secnd'!A1"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("sheet-typo.out.xlsx") });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "EXTERNAL_LINK_RELATIVE");
        Assert.StartsWith("No sheet is named 'Secnd'; did you mean 'Second'?", warning.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void AMisspelledFunctionIsNamedWithTheClosestFunction()
    {
        string source = _fixture.CreateSalesWorkbook("function-typo.xlsx");

        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""
                {"ops":[
                  {"op":"set_formula","sheet":"Data","range":"E2:E3","formula":"=summ(B2:C2)"},
                  {"op":"set_formula","sheet":"Data","range":"E5","formula":"=IF(B2>0,VLOKUP(A2,A2:C3,2,FALSE),\"MYFUNC(\")"},
                  {"op":"set_formula","sheet":"Data","range":"E6","formula":"=SUM(B2:C2)+FOOBAR(1)"},
                  {"op":"set_formula","sheet":"Data","range":"E7","formula":"=SUM(B2:C3)"},
                  {"op":"set_formula","sheet":"Data","range":"E8","formula":"=LET(fn,LAMBDA(a,a+1),fn(2))"}
                ]}
                """),
            new EditRequest { OutputPath = _fixture.Temp.File("function-typo.out.xlsx") });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "FORMULA_FUNCTION_UNKNOWN");
        Assert.StartsWith(
            "Aspose.Cells does not know the function(s) in 'Data'!E2: summ (did you mean SUM?); 'Data'!E5: VLOKUP (did you mean VLOOKUP?); 'Data'!E6: FOOBAR.",
            warning.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ALocalizedFunctionNameIsNamedLikeAMisspelledOne()
    {
        string source = _fixture.CreateSalesWorkbook("function-localized.xlsx");

        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""
                {"ops":[
                  {"op":"set_formula","sheet":"Data","range":"E2","formula":"=求和(B2:C2)"},
                  {"op":"set_formula","sheet":"Data","range":"E3","formula":"=SUMME(B3:C3)"}
                ]}
                """),
            new EditRequest { OutputPath = _fixture.Temp.File("function-localized.out.xlsx") });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "FORMULA_FUNCTION_UNKNOWN");
        Assert.StartsWith("Aspose.Cells does not know the function(s) in 'Data'!E2: 求和; 'Data'!E3: SUMME", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownFunctionIsFoundWhereLaterOperationsMovedItsCell()
    {
        string source = _fixture.CreateSalesWorkbook("function-typo-moved.xlsx");

        EditResult result = _fixture.Engine.ApplyOps(source,
            ParseOps("""
                {"ops":[
                  {"op":"set_formula","range":"E2","formula":"=summ(B2:C2)"},
                  {"op":"set_formula","sheet":"Second","range":"B1","formula":"=VLOKUP(1,A1:A2,1,FALSE)"},
                  {"op":"set_formula","sheet":"Data","range":"E6","formula":"=FOOBAR(1)"},
                  {"op":"set_active_sheet","sheet":"Second"},
                  {"op":"rename_sheet","sheet":"Second","to":"Notes"},
                  {"op":"delete_rows","sheet":"Data","at":6},
                  {"op":"insert_rows","sheet":"Data","at":1,"count":2}
                ]}
                """),
            new EditRequest { OutputPath = _fixture.Temp.File("function-typo-moved.out.xlsx") });

        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "FORMULA_FUNCTION_UNKNOWN");
        Assert.StartsWith(
            "Aspose.Cells does not know the function(s) in 'Data'!E4: summ (did you mean SUM?); 'Notes'!B1: VLOKUP (did you mean VLOOKUP?).",
            warning.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GetInfo_ReportsSheetStructure()
    {
        string path = _fixture.CreateSalesWorkbook("info.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest());

        Assert.Equal("workbook", result.Kind);
        Assert.Equal("xlsx", result.Source.Format);
        Assert.True(result.Source.SizeBytes > 0);

        SheetInfo data = FindSheet(result, "Data");
        Assert.Equal("A1:C3", data.UsedRange);
        Assert.Equal(3, data.RowCount);
        Assert.Equal(3, data.ColumnCount);
        Assert.False(data.Hidden);
        Assert.Null(data.Preview);

        Assert.True(FindSheet(result, "Backstage").Hidden);
        Assert.False(result.Workbook.HasVba);
    }

    [Fact]
    public void GetInfo_WithPreview_ReturnsDisplayValues()
    {
        string path = _fixture.CreateSalesWorkbook("preview.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { IncludePreview = true, PreviewRows = 2 });

        SheetInfo data = FindSheet(result, "Data");
        Assert.NotNull(data.Preview);
        Assert.Equal(2, data.Preview.Count);
        Assert.Equal(["Region", "Q1", "Q2"], data.Preview[0]);
        Assert.Equal(["East", "1200", "1500"], data.Preview[1]);
    }

    [Fact]
    public void GetInfo_ListsOnlyTheNamesTheWorkbookDefines()
    {
        // The engine keeps a placeholder name for each newer function a formula calls, such as
        // _xlfn.XLOOKUP; the saved file does not define it.
        string path = _fixture.Temp.File("future-functions.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A1"].Formula = "=XLOOKUP(1,B1:B2,C1:C2)";
            workbook.Worksheets.Names.Add("Threshold");
            workbook.Worksheets.Names["Threshold"].RefersTo = "=Sheet1!$B$1";
            workbook.Save(path);
        }

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest { Details = [InfoDetails.Names] });

        Assert.Equal(1, result.Workbook.DefinedNameCount);
        Assert.Equal("Threshold", Assert.Single(result.Workbook.DefinedNames!).Name);
    }

    [Fact]
    public void GetInfo_WithDetails_ReturnsNamesFormulaErrorsAndFonts()
    {
        string path = _fixture.CreateWorkbookWithDetails("details.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { Details = [InfoDetails.Names, InfoDetails.Errors, InfoDetails.Fonts] });

        Assert.NotNull(result.Workbook.DefinedNames);
        Assert.Contains(result.Workbook.DefinedNames, n => n.Name == "Threshold");

        Assert.NotNull(result.Workbook.FormulaErrors);
        CellError error = Assert.Single(result.Workbook.FormulaErrors);
        Assert.Equal("Data", error.Sheet);
        Assert.Equal("A3", error.Cell);
        Assert.Equal("#DIV/0!", error.Error);

        // Every workbook uses at least its default font.
        Assert.NotNull(result.Workbook.Fonts);
        Assert.NotEmpty(result.Workbook.Fonts);
    }

    // A real corpus xlsb held 19,318 formula errors; the old scan returned exactly
    // 1,000 from the first sheet with no signal, so later sheets looked clean. The
    // list stays capped, but the honest total now rides a LIST_TRUNCATED warning.
    [Fact]
    public void GetInfo_WithErrorsDetail_PastTheCap_WarnsWithTheHonestTotal()
    {
        string path = _fixture.Temp.File("many-errors.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            for (int i = 0; i < 1001; i++)
            {
                cells[i, 0].Formula = "=1/0"; // #DIV/0!
            }

            workbook.CalculateFormula();
            workbook.Save(path, Aspose.Cells.SaveFormat.Xlsx);
        }

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { Details = [InfoDetails.Errors] });

        // The list is capped, but the warning reports the true count.
        Assert.Equal(1000, result.Workbook.FormulaErrors!.Count);
        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == WarningCodes.ListTruncated);
        Assert.NotNull(warning);
        Assert.Contains("1001", warning!.Message, StringComparison.Ordinal);
        Assert.NotNull(warning.Hint);
    }

    [Fact]
    public void GetInfo_WithoutDetails_OmitsDetailSections()
    {
        string path = _fixture.CreateWorkbookWithDetails("no-details.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest());

        Assert.Null(result.Workbook.DefinedNames);
        Assert.Null(result.Workbook.FormulaErrors);
    }

    [Fact]
    public void GetInfo_ReportsChartsAndValidationsInTheEditVocabulary()
    {
        string source = _fixture.CreateSalesWorkbook("vocabulary-source.xlsx");
        string[] chartTypes = [ChartTypes.Column, ChartTypes.Bar, ChartTypes.Line, ChartTypes.Pie, ChartTypes.Scatter, ChartTypes.Area];
        string[] validationTypes = [ValidationTypes.List, ValidationTypes.WholeNumber, ValidationTypes.Decimal, ValidationTypes.Date, ValidationTypes.TextLength, ValidationTypes.Custom];
        IEnumerable<string> charts = chartTypes.Select((type, index) =>
            $$"""{"op":"create_chart","sheet":"Data","type":"{{type}}","dataRange":"A1:C2","at":"E{{1 + (index * 10)}}:J{{8 + (index * 10)}}"}""");
        IEnumerable<string> validations = validationTypes.Select((type, index) => type switch
        {
            ValidationTypes.List => $$"""{"op":"set_validation","sheet":"Second","range":"B{{index + 1}}","type":"list","listItems":["a","b"]}""",
            ValidationTypes.Custom => $$"""{"op":"set_validation","sheet":"Second","range":"B{{index + 1}}","type":"custom","value1":"=B{{index + 1}}>0"}""",
            _ => $$"""{"op":"set_validation","sheet":"Second","range":"B{{index + 1}}","type":"{{type}}","operator":"greaterThan","value1":"=1"}""",
        });
        string path = _fixture.Temp.File("vocabulary.xlsx");
        _fixture.Engine.ApplyOps(source,
            ParseOps($$"""{"ops":[{{string.Join(",", charts.Concat(validations))}}]}"""),
            new EditRequest { OutputPath = path });
        using (var workbook = new Aspose.Cells.Workbook(path))
        {
            Aspose.Cells.Charts.ChartCollection sdkCharts = workbook.Worksheets["Data"].Charts;
            sdkCharts[sdkCharts.Add(Aspose.Cells.Charts.ChartType.ColumnStacked, 70, 4, 78, 9)].Name = "Stacked";
            workbook.Save(path);
        }

        WorkbookInfoResult result = _fixture.Engine.GetInfo(
            path, new InfoRequest { Details = [InfoDetails.Charts, InfoDetails.Validation] });

        // What inspect reads is what create_chart, update_chart and set_validation accept.
        Assert.Equal([.. chartTypes, "columnStacked"], result.Workbook.Charts!.Select(static chart => chart.Type));
        Assert.Equal(Enumerable.Range(0, chartTypes.Length + 1), result.Workbook.Charts!.Select(static chart => chart.Index));
        Assert.Equal(validationTypes, result.Workbook.Validations!.Select(static validation => validation.Type));
    }

    [Fact]
    public void GetInfo_ReadsBackTheSheetLayoutTheOperationsSet()
    {
        string path = _fixture.Temp.File("layout.xlsx");
        _fixture.Engine.ApplyOps(_fixture.CreateSalesWorkbook("layout-source.xlsx"),
            ParseOps("""
                {"ops":[
                  {"op":"freeze_panes","sheet":"Data","cell":"B2"},
                  {"op":"group_rows","sheet":"Data","from":2,"to":6},
                  {"op":"group_rows","sheet":"Data","from":3,"to":4,"collapse":true},
                  {"op":"group_rows","sheet":"Data","from":9,"to":10},
                  {"op":"group_columns","sheet":"Data","from":"D","to":"E"},
                  {"op":"set_autofilter","sheet":"Data","range":"A1:C3"},
                  {"op":"set_print_area","sheet":"Data","range":"A1:C3","titleRows":"1"},
                  {"op":"set_page_setup","sheet":"Data","orientation":"landscape","fitToWidth":1,"fitToHeight":0,
                   "header":"Roster","footer":"Page &P of &N"}
                ]}
                """),
            new EditRequest { OutputPath = path });

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest { Details = [InfoDetails.Layout] });

        SheetLayoutInfo data = Assert.Single(result.Workbook.Layouts!, static layout => layout.Sheet == "Data");
        Assert.Equal("B2", data.FreezePanes);
        Assert.Equal(
            [(2, 6, 1, false), (9, 10, 1, false), (3, 4, 2, true)],
            data.RowGroups!.Select(static group => (group.From, group.To, group.Level, group.Collapsed)));
        Assert.Equal([("D", "E", 1, false)], data.ColumnGroups!.Select(static group => (group.From, group.To, group.Level, group.Collapsed)));
        Assert.Equal("A1:C3", data.AutoFilter);
        Assert.Equal("A1:C3", data.PrintArea);
        Assert.Equal("1:1", data.TitleRows);
        Assert.Null(data.TitleColumns);
        Assert.Equal("landscape", data.Orientation);
        Assert.Equal((1, 0, null), (data.FitToWidth, data.FitToHeight, data.Scale));
        Assert.Equal(("Roster", "Page &P of &N"), (data.Header, data.Footer));

        SheetLayoutInfo plain = Assert.Single(result.Workbook.Layouts!, static layout => layout.Sheet == "Second");
        Assert.Equal(new SheetLayoutInfo { Sheet = "Second", Orientation = "portrait", Scale = 100 }, plain);
        Assert.Null(_fixture.Engine.GetInfo(path, new InfoRequest()).Workbook.Layouts);
    }

    [Fact]
    public void GetInfo_ReportsTheLicensedMode()
    {
        string path = _fixture.CreateSalesWorkbook("license.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest());

        Assert.Equal(LicenseModes.Licensed, result.License?.Mode);
        // Read-only operations never carry the evaluation warning.
        Assert.Null(result.Warnings);
    }

    [Fact]
    public void Convert_ToPdf_ProducesAPdfDocument()
    {
        string path = _fixture.CreateSalesWorkbook("to-pdf.xlsx");
        string output = _fixture.Temp.File("out.pdf");

        ConvertResult result = _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "pdf",
            OutputPath = output,
        });

        Assert.Equal(output, result.Output.Path);
        Assert.True(HasHeader(output, [0x25, 0x50, 0x44, 0x46]));
        Assert.Equal(new FileInfo(output).Length, result.Output.SizeBytes);
    }

    [Fact]
    public void Convert_PreservesInputFormat_WhenSourceIsCsv()
    {
        // Regression: Workbook.FileFormat mutates on save; the reported input
        // format must reflect the file that was read, not the one written.
        string path = _fixture.CreateCsv("input-format.csv");
        string output = _fixture.Temp.File("from-csv.xlsx");

        ConvertResult result = _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "xlsx",
            OutputPath = output,
        });

        Assert.Equal("csv", result.Input.Format);
        Assert.Equal("xlsx", result.Output.Format);
    }

    [Fact]
    public void Convert_LicensedOutput_CarriesNoEvaluationWarning()
    {
        string path = _fixture.CreateSalesWorkbook("warnings.xlsx");
        string output = _fixture.Temp.File("warnings.csv");

        ConvertResult result = _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "csv",
            OutputPath = output,
        });

        // The sales fixture is multi-sheet, so a csv export also carries a
        // SHEETS_DROPPED warning; only the evaluation warning must be absent.
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalMode);
    }

    /// <summary>
    /// A licensed engine reading a workbook an evaluation save left with its warning sheet active
    /// skips that sheet like the evaluation engine does, while a sheet of that name holding
    /// anything else stays the active default; licensed saves add no warning sheet.
    /// </summary>
    [Fact]
    public void Licensed_TheActiveEvaluationWarningSheetIsSkippedByActiveSheetDefaults()
    {
        string marked = WorkbookWithActiveSheet("marked.xlsx", "Evaluation Only. Created with Aspose.Cells for .NET.");
        string lookalike = WorkbookWithActiveSheet("lookalike.xlsx", "notes");

        WorkbookReadResult read = _fixture.Engine.Read(marked, new ReadRequest());
        ConvertResult csv = _fixture.Engine.Convert(marked, new ConvertRequest
        {
            TargetFormatId = "csv",
            OutputPath = _fixture.Temp.File("marked.csv"),
        });
        ConvertResult copied = _fixture.Engine.Convert(marked, new ConvertRequest
        {
            TargetFormatId = "xlsx",
            OutputPath = _fixture.Temp.File("marked-copy.xlsx"),
        });
        EditResult edited = _fixture.Engine.ApplyOps(marked,
            ParseOps("""{"ops":[{"op":"set_values","range":"A2","values":[["edited"]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("marked-edited.xlsx"), Overwrite = true });
        EditResult activated = _fixture.Engine.ApplyOps(marked,
            ParseOps("""{"ops":[{"op":"set_active_sheet","sheet":"Data"}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("marked-activated.xlsx"), Overwrite = true });
        EditResult notActivated = _fixture.Engine.ApplyOps(marked,
            ParseOps("""{"ops":[{"op":"set_active_sheet","sheet":"Missing"},{"op":"set_values","range":"A2","values":[["edited"]]}]}"""),
            new EditRequest
            {
                OutputPath = _fixture.Temp.File("marked-not-activated.xlsx"), Overwrite = true,
                Options = new EditCommandOptions { BestEffort = true },
            });
        EditResult editedCsv = _fixture.Engine.ApplyOps(marked,
            ParseOps("""{"ops":[{"op":"set_values","range":"A2","values":[["edited"]]}]}"""),
            new EditRequest { OutputPath = _fixture.Temp.File("marked-edited.csv"), Overwrite = true });
        WorkbookReadResult kept = _fixture.Engine.Read(lookalike, new ReadRequest());

        Assert.Equal("Data", read.Sheet!.Name);
        Warning skipped = Assert.Single(read.Warnings!);
        Assert.Equal("EVALUATION_SHEET_SKIPPED", skipped.Code);
        Assert.Equal("Evaluation Warning", skipped.Location);
        Assert.Contains(csv.Warnings!, static warning => warning.Code == "EVALUATION_SHEET_SKIPPED");
        Assert.StartsWith("data", File.ReadAllText(csv.Output.Path), StringComparison.Ordinal);
        Assert.Null(copied.Warnings);
        using var copy = new Aspose.Cells.Workbook(copied.Output.Path);
        Assert.Equal(["Data", "Evaluation Warning"], copy.Worksheets.Cast<Aspose.Cells.Worksheet>().Select(static sheet => sheet.Name));
        // Skipping changes the default only: whole-workbook saves keep the input's active sheet
        // unless the batch chose one.
        Assert.Equal("Evaluation Warning", ActiveSheet(copied.Output.Path));
        Assert.Contains(edited.Warnings!, static warning => warning.Code == "EVALUATION_SHEET_SKIPPED");
        Assert.Equal("Evaluation Warning", ActiveSheet(edited.Output!.Path));
        using (var editedBook = new Aspose.Cells.Workbook(edited.Output.Path))
        {
            Assert.Equal("edited", editedBook.Worksheets["Data"].Cells["A2"].StringValue);
        }
        Assert.Equal("Data", ActiveSheet(activated.Output!.Path));
        Assert.Equal(OpStatuses.Failed, notActivated.Applied[0].Status);
        Assert.Equal("Evaluation Warning", ActiveSheet(notActivated.Output!.Path));
        // A text output writes the one sheet the batch edited.
        Assert.StartsWith("data\r\nedited", File.ReadAllText(editedCsv.Output!.Path), StringComparison.Ordinal);
        Assert.Equal("Evaluation Warning", kept.Sheet!.Name);
        Assert.Null(kept.Warnings);

        static string ActiveSheet(string path)
        {
            using var workbook = new Aspose.Cells.Workbook(path);
            return workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex].Name;
        }

        string WorkbookWithActiveSheet(string fileName, string text)
        {
            string path = _fixture.Temp.File(fileName);
            using var workbook = new Aspose.Cells.Workbook();
            workbook.Worksheets[0].Name = "Data";
            workbook.Worksheets[0].Cells["A1"].PutValue("data");
            Aspose.Cells.Worksheet warning = workbook.Worksheets.Add("Evaluation Warning");
            warning.Cells["A5"].PutValue(text);
            workbook.Worksheets.ActiveSheetIndex = warning.Index;
            workbook.Save(path);
            return path;
        }
    }

    [Fact]
    public void Convert_UnknownSheet_ThrowsSheetNotFoundWithAlternatives()
    {
        string path = _fixture.CreateSalesWorkbook("bad-sheet.xlsx");

        CliException exception = Assert.Throws<CliException>(() => _fixture.Engine.Convert(path, new ConvertRequest
        {
            TargetFormatId = "csv",
            OutputPath = _fixture.Temp.File("never.csv"),
            SheetName = "Dat",
        }));

        Assert.Equal("SHEET_NOT_FOUND", exception.Code.Name);
        Assert.Equal("Dat", exception.Details!["requested"]!.GetValue<string>());
        Assert.Contains("Data", exception.Details["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
        Assert.Equal("Data", exception.Details["suggestions"]![0]!.GetValue<string>());
    }

    // The legacy xls grid is 65,536 rows × 256 columns. A modern source that
    // overflows it loses everything past the limit on save — the engine does it
    // silently, so the CLI must WARN (a real 200,001-row corpus file lost 134,465
    // rows to an exit-0 convert with no warning at all).
    [Fact]
    public void Convert_ToXls_RowsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A1"].PutValue("keep");
            workbook.Worksheets[0].Cells["A65537"].PutValue("lost"); // one row past the xls grid
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("tall.xls"),
            Overwrite = true,
        });

        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == "DATA_TRUNCATED");
        Assert.NotNull(warning);
        Assert.Contains("row", warning!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(warning.Hint);
    }

    [Fact]
    public void Convert_ToXls_ColumnsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("wide.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells[0, 0].PutValue("keep");
            workbook.Worksheets[0].Cells[0, 256].PutValue("lost"); // column 257, one past the 256-col grid
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("wide.xls"),
            Overwrite = true,
        });

        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == "DATA_TRUNCATED");
        Assert.NotNull(warning);
        Assert.Contains("column", warning!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A sheet that fits (and a modern target that never truncates) must NOT warn.
    [Fact]
    public void Convert_ToXls_WithinTheGrid_DoesNotWarnTruncated()
    {
        string src = _fixture.CreateSalesWorkbook("small.xlsx");

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("small.xls"),
            Overwrite = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    [Fact]
    public void Convert_ToXlsb_RowsBeyondTheXlsGrid_DoesNotWarn()
    {
        // xlsb keeps the modern 1,048,576-row grid, so a 65,537-row sheet fits.
        string src = _fixture.Temp.File("tall2.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A65537"].PutValue("fits in xlsb");
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xlsb",
            OutputPath = _fixture.Temp.File("tall2.xlsb"),
            Overwrite = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    // The same silent truncation lurks in every write path, not just convert:
    // editing or recalculating a modern-sized workbook out to .xls drops the
    // overflow too. The shared Save helper now warns for all of them.
    [Fact]
    public void Edit_ToXls_RowsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("edit-tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A65537"].PutValue("lost");
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        EditResult result = _fixture.Engine.ApplyOps(
            src,
            ParseOps("""{ "ops": [ { "op": "set_values", "sheet": "Sheet1", "range": "A1", "values": [["x"]] } ] }"""),
            new EditRequest { OutputPath = _fixture.Temp.File("edit-tall.xls"), Overwrite = true });

        Assert.Contains(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    [Fact]
    public void FormulaOnlyEdit_ToXls_RowsBeyondTheGrid_WarnsDataTruncated()
    {
        string src = _fixture.Temp.File("calc-tall.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Cells["A65537"].PutValue("lost");
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        EditResult result = _fixture.Engine.ApplyOps(
            src,
            ParseOps("""{ "ops": [ { "op": "set_formula", "range": "B1", "formula": "=1" } ] }"""),
            new EditRequest { OutputPath = _fixture.Temp.File("calc-tall.xls"), Overwrite = true });

        Assert.Contains(result.Warnings ?? [], w => w.Code == "DATA_TRUNCATED");
    }

    // A whole-column total (=SUM(A5:A1048576)) references rows past the xls grid.
    // The engine's save to xls rewrites it to =SUM(#REF!) — silent formula
    // corruption on a real GST-return template (29 such totals). The convert path
    // now measures the rise in #REF! formulas the save produced and warns.
    [Fact]
    public void Convert_ToXls_FormulaReferencingBeyondTheGrid_WarnsFormulasBroken()
    {
        string src = _fixture.Temp.File("wholecol.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            cells["A5"].PutValue(5);
            cells["B1"].Formula = "=SUM(A5:A1048576)"; // endpoint past the 65,536-row xls grid
            workbook.CalculateFormula();
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xls",
            OutputPath = _fixture.Temp.File("wholecol.xls"),
            Overwrite = true,
        });

        Warning? warning = result.Warnings?.FirstOrDefault(w => w.Code == "FORMULAS_BROKEN");
        Assert.NotNull(warning);
        Assert.Contains("#REF!", warning!.Message, StringComparison.Ordinal);
        Assert.NotNull(warning.Hint);
    }

    // The same formula converted to a modern grid stays intact — no false warning.
    [Fact]
    public void Convert_ToXlsb_FormulaReferencingBeyondTheXlsGrid_DoesNotWarnBroken()
    {
        string src = _fixture.Temp.File("wholecol2.xlsx");
        using (var workbook = new Aspose.Cells.Workbook())
        {
            Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
            cells["A5"].PutValue(5);
            cells["B1"].Formula = "=SUM(A5:A1048576)";
            workbook.CalculateFormula();
            workbook.Save(src, Aspose.Cells.SaveFormat.Xlsx);
        }

        ConvertResult result = _fixture.Engine.Convert(src, new ConvertRequest
        {
            TargetFormatId = "xlsb",
            OutputPath = _fixture.Temp.File("wholecol2.xlsb"),
            Overwrite = true,
        });

        Assert.DoesNotContain(result.Warnings ?? [], w => w.Code == "FORMULAS_BROKEN");
    }

    [Fact]
    public void Render_ToPng_ProducesAPngImage()
    {
        string path = _fixture.CreateSalesWorkbook("render.xlsx");
        string output = _fixture.Temp.File("sheet.png");

        RenderResult result = _fixture.Engine.Render(path, new RenderRequest
        {
            TargetFormatId = "png",
            OutputPath = output,
            SheetName = "Data",
            Range = new RangeRef(new CellRef(0, 0), new CellRef(2, 2)),
            Dpi = 96,
        });

        Assert.Equal("Data", result.Sheet);
        Assert.Equal("A1:C3", result.Range);
        Assert.Equal(96, result.Dpi);
        Assert.True(HasHeader(output, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
    }

    [Fact]
    public void Render_ToSvg_OmitsDpi()
    {
        string path = _fixture.CreateSalesWorkbook("render-svg.xlsx");
        string output = _fixture.Temp.File("sheet.svg");

        RenderResult result = _fixture.Engine.Render(path, new RenderRequest
        {
            TargetFormatId = "svg",
            OutputPath = output,
            SheetName = "Data",
        });

        Assert.Null(result.Dpi);
        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public void Open_EncryptedWithoutPassword_ReportsPasswordRequired()
    {
        string path = _fixture.CreateEncryptedWorkbook("secret", "locked1.xlsx");

        CliException exception = Assert.Throws<CliException>(
            () => _fixture.Engine.GetInfo(path, new InfoRequest()));

        Assert.Equal(ErrorCodes.PasswordRequired, exception.Code);
    }

    [Fact]
    public void Open_EncryptedWithWrongPassword_ReportsPasswordInvalid()
    {
        string path = _fixture.CreateEncryptedWorkbook("secret", "locked2.xlsx");

        CliException exception = Assert.Throws<CliException>(
            () => _fixture.Engine.GetInfo(path, new InfoRequest { Password = "wrong" }));

        Assert.Equal(ErrorCodes.PasswordInvalid, exception.Code);
    }

    [Fact]
    public void Open_EncryptedWithCorrectPassword_Succeeds()
    {
        string path = _fixture.CreateEncryptedWorkbook("secret", "locked3.xlsx");

        WorkbookInfoResult result = _fixture.Engine.GetInfo(path, new InfoRequest { Password = "secret" });

        Assert.True(result.Workbook.SheetCount >= 1);
    }

    [Fact]
    public void Open_GarbageBytes_ReportsFileCorrupt()
    {
        string path = _fixture.Temp.File("garbage.xlsx");
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE]);

        CliException exception = Assert.Throws<CliException>(
            () => _fixture.Engine.GetInfo(path, new InfoRequest()));

        Assert.Equal(ErrorCodes.FileCorrupt, exception.Code);
    }

    private static bool HasHeader(string path, byte[] expected)
    {
        byte[] actual = new byte[expected.Length];
        using FileStream stream = File.OpenRead(path);
        return stream.Read(actual, 0, actual.Length) == actual.Length
            && actual.AsSpan().SequenceEqual(expected);
    }

    private static CellsOpsBatch ParseOps(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
