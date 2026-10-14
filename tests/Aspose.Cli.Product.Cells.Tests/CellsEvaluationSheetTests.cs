using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The "Evaluation Warning" sheet Aspose.Cells evaluation mode adds and activates gives way to
/// the workbook's content only in evaluation mode. With a license a workbook's active sheet of
/// that name is the user's sheet, and a command that defaults to the active sheet uses it.
/// </summary>
public sealed class CellsEvaluationSheetTests
{
    private const string Notice = "Evaluation Only. Created with Aspose.Cells for .NET.Copyright 2003 - 2026 Aspose Pty Ltd.";

    [Theory]
    [InlineData(LicenseState.Evaluation)]
    [InlineData(LicenseState.Licensed)]
    public void AnActiveSheetNamedEvaluationWarning_IsSkippedOnlyInEvaluationMode(LicenseState state)
    {
        using var temp = new TempDirectory();
        string input = temp.File("book.xlsx");
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Data";
            workbook.Worksheets[0].Cells["A1"].PutValue("Region");
            Worksheet warning = workbook.Worksheets[workbook.Worksheets.Add()];
            warning.Name = "Evaluation Warning";
            warning.Cells["A1"].PutValue(Notice);
            workbook.Worksheets.ActiveSheetIndex = warning.Index;
            workbook.Save(input);
        }

        ResourceBudgetLedger budgets = ProductTestBudgets.Create<CellsModule>();
        var outputs = new OutputPipeline<Workbook>(EvaluationLimits.Gate(state), new CellsEvaluationProfile(), new SafeFileWriter(budgets));
        using LoadedWorkbook loaded = new CellsWorkbookLoader(budgets, outputs).Open(input, password: null);

        WorksheetCollection sheets = loaded.Workbook.Worksheets;
        string active = sheets[sheets.ActiveSheetIndex].Name;
        Warning? skipped = loaded.SkippedSheetWarning(defaultedToActiveSheet: true, "Pass --sheet with the sheet's name from inspect to read another sheet.");
        if (state == LicenseState.Evaluation)
        {
            Assert.Equal("Data", active);
            Assert.Equal("ACTIVE_SHEET_SKIPPED", skipped?.Code.Name);
        }
        else
        {
            // An unlicensed test run saves its own warning sheet too; either way a sheet of that
            // name stays active and nothing is skipped.
            Assert.StartsWith("Evaluation Warning", active, StringComparison.Ordinal);
            Assert.Null(skipped);
        }
    }
}
