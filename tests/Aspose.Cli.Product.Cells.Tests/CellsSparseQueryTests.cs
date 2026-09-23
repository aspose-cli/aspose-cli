using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsSparseQueryTests
{
    [Fact]
    public void Search_VisitsSparseExtremesInAddressOrderWithoutCreatingCells()
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Cells["XFD1048576"].PutValue("needle-last");
        sheet.Cells["C2"].PutValue("needle-middle");
        sheet.Cells["A1"].PutValue("needle-first");
        int original = sheet.Cells.Count;
        using var deadline = OperationDeadline.Start(null);
        var budgets = Budgets(deadline, 3);
        var result = SearchMatcher.Find(budgets, workbook, new SearchRequest { Pattern = "needle" }, null);
        Assert.Equal(["A1", "C2", "XFD1048576"], result.Hits.Select(hit => hit.Cell));
        Assert.False(result.Truncated);
        Assert.Equal(0, budgets.Remaining(CellsBudgetDomains.Cells));
        Assert.Equal(original, sheet.Cells.Count);
    }

    [Fact]
    public void Search_ResultLimitIsIndependentOfWorkAdmission()
    {
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue("needle");
        workbook.Worksheets[0].Cells["XFD1048576"].PutValue("needle");
        using var deadline = OperationDeadline.Start(null);
        var result = SearchMatcher.Find(Budgets(deadline, 2), workbook,
            new SearchRequest { Pattern = "needle", MaxHits = 1 }, null);
        Assert.Equal("A1", Assert.Single(result.Hits).Cell);
        Assert.True(result.Truncated);
        Assert.Equal(ErrorCodes.InputBudgetExceeded, Assert.Throws<CliException>(() =>
            SearchMatcher.Find(Budgets(deadline, 1), workbook,
                new SearchRequest { Pattern = "needle", MaxHits = 1 }, null)).Code);
    }

    [Fact]
    public void FormulaErrors_CountPastTheSampleLimitAcrossSheets()
    {
        using var workbook = new Workbook();
        Worksheet first = workbook.Worksheets[0];
        for (int row = 0; row < 1001; row++) { first.Cells[row, 0].Formula = "=1/0"; }
        Worksheet second = workbook.Worksheets.Add("Second");
        second.Cells["XFD1048576"].Formula = "=1/0";
        workbook.CalculateFormula();
        int original = first.Cells.Count + second.Cells.Count;
        using var deadline = OperationDeadline.Start(null);
        var result = InfoProjection.Summarize(Budgets(deadline, original), workbook,
            "sample.xlsx", new InfoRequest { Details = [InfoDetails.Errors] });
        Assert.Equal(1000, result.Summary.FormulaErrors!.Count);
        Assert.Equal("A1", result.Summary.FormulaErrors[0].Cell);
        Assert.Equal("A1000", result.Summary.FormulaErrors[^1].Cell);
        Assert.Contains("1002", result.ErrorsTruncated!.Message);
        Assert.True(result.ErrorsTruncated.AffectsCompleteness);
        Assert.Equal(original, first.Cells.Count + second.Cells.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queries_ChargeUnmatchedCellsAcrossSheets(bool inspect)
    {
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue("not a match");
        workbook.Worksheets.Add("Second").Cells["XFD1048576"].PutValue("not a match");
        using var deadline = OperationDeadline.Start(null);
        var budgets = Budgets(deadline, 1);
        Assert.Equal(ErrorCodes.InputBudgetExceeded, Assert.Throws<CliException>(() => Query(inspect, budgets, workbook)).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queries_StopForCancellationAndExpiredDeadline(bool inspect)
    {
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["XFD1048576"].PutValue("needle");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var deadline = OperationDeadline.Start(null, cancelled.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => Query(inspect, Budgets(deadline, 1), workbook));
        using var expired = OperationDeadline.FromAbsoluteTick(TimeSpan.FromSeconds(1), Environment.TickCount64 - 1);
        Assert.Equal(ErrorCodes.OperationTimeout, Assert.Throws<CliException>(() => Query(inspect, Budgets(expired, 1), workbook)).Code);
    }

    [Fact]
    public void Search_AdmitsSortStorageBeforeRetainingCells()
    {
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue("needle");
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline, new Dictionary<string, long>
        { [CellsBudgetDomains.Cells] = 1, [ResourceBudgetKinds.MemoryBufferBytes] = 128 });
        CliException error = Assert.Throws<CliException>(() => Query(false, budgets, workbook));
        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
        Assert.Equal(1, workbook.Worksheets[0].Cells.Count);
    }

    [Fact]
    public void SparseQueries_RunThroughTheRealExecutable()
    {
        using var workspace = new Aspose.Cli.TestKit.TempWorkspace();
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["XFD1048576"].PutValue("needle");
        workbook.Save(workspace.File("sparse.xlsx"));
        var search = workspace.Run("cells", "query", "search", "sparse.xlsx", "--pattern", "needle", "--max-hits", "1", "--timeout", "15", "--output", "json");
        Assert.True(search.ExitCode == 0, search.StdErr);
        Assert.Contains("XFD1048576", search.StdOut);
        var inspect = workspace.Run("cells", "inspect", "sparse.xlsx", "--detail", "errors", "--timeout", "15", "--output", "json");
        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
    }

    private static void Query(bool inspect, ResourceBudgetLedger budgets, Workbook workbook)
    {
        if (inspect)
        {
            _ = InfoProjection.Summarize(budgets, workbook, "sample.xlsx", new InfoRequest { Details = [InfoDetails.Errors] });
        }
        else { _ = SearchMatcher.Find(budgets, workbook, new SearchRequest { Pattern = "needle", MaxHits = 1 }, null); }
    }

    private static ResourceBudgetLedger Budgets(OperationDeadline deadline, int cells) =>
        new(deadline, new Dictionary<string, long> { [CellsBudgetDomains.Cells] = cells });
}
