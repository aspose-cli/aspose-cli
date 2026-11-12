using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The "Evaluation Warning" sheet Aspose.Cells evaluation mode adds and activates gives way to
/// the workbook's content with or without a license. A sheet of that name that holds anything
/// besides the notice is the user's sheet, and a command that defaults to the active sheet uses it.
/// </summary>
public sealed class CellsEvaluationSheetTests
{
    private const string Notice = "Evaluation Only. Created with Aspose.Cells for .NET.Copyright 2003 - 2026 Aspose Pty Ltd.";

    [Theory]
    [InlineData(LicenseState.Evaluation)]
    [InlineData(LicenseState.Licensed)]
    public void AnActiveEvaluationWarningSheet_IsSkippedWithOrWithoutALicense(LicenseState state)
    {
        using var temp = new TempDirectory();
        string input = Save(temp, "book.xlsx", Notice);

        // An unlicensed test run saves its own warning sheet too, and activates it; either is skipped.
        (string active, Warning? skipped) = OpenActive(input, state);

        Assert.Equal("Data", active);
        Assert.Equal("ACTIVE_SHEET_SKIPPED", skipped?.Code.Name);
    }

    [LicensedFact]
    public void AnActiveSheetNamedEvaluationWarning_ThatHoldsMoreThanTheNotice_IsTheWorkbooksOwn()
    {
        using var fixture = new CellsFixture();
        string input = Save(fixture.Temp, "own.xlsx", Notice, "Own data");

        (string active, Warning? skipped) = OpenActive(input, LicenseState.Licensed);

        Assert.Equal("Evaluation Warning", active);
        Assert.Null(skipped);
    }

    // A workbook of a "Data" sheet and an active "Evaluation Warning" sheet that holds the texts in column A.
    private static string Save(TempDirectory temp, string name, params string[] texts)
    {
        string input = temp.File(name);
        using var workbook = new Workbook();
        workbook.Worksheets[0].Name = "Data";
        workbook.Worksheets[0].Cells["A1"].PutValue("Region");
        Worksheet warning = workbook.Worksheets[workbook.Worksheets.Add()];
        warning.Name = "Evaluation Warning";
        for (int row = 0; row < texts.Length; row++)
        {
            warning.Cells[row, 0].PutValue(texts[row]);
        }
        workbook.Worksheets.ActiveSheetIndex = warning.Index;
        workbook.Save(input);
        return input;
    }

    private static (string Active, Warning? Skipped) OpenActive(string input, LicenseState state)
    {
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<CellsModule>();
        var outputs = new OutputPipeline<Workbook>(EvaluationLimits.Gate(state), new CellsEvaluationProfile(), new SafeFileWriter(budgets));
        using LoadedWorkbook loaded = new CellsWorkbookLoader(budgets, outputs).Open(input, password: null);
        WorksheetCollection sheets = loaded.Workbook.Worksheets;
        return (sheets[sheets.ActiveSheetIndex].Name,
            loaded.SkippedSheetWarning(defaultedToActiveSheet: true, "Pass --sheet with the sheet's name from inspect to read another sheet."));
    }
}
