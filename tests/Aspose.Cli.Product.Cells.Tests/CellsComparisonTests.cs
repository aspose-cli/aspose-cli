using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsComparisonTests
{
    [Fact]
    public void Dates_CompareRawSerialsAcrossFormatsAndDateSystems()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        left.Worksheets[0].Cells["A1"].PutValue(45000d);
        right.Settings.Date1904 = true;
        Cell dated = right.Worksheets[0].Cells["A1"];
        dated.PutValue(45000d);
        Style style = dated.GetStyle();
        style.Custom = "yyyy-mm-dd";
        dated.SetStyle(style);
        Assert.True(Compare(left, right).Identical);
        dated.PutValue("2023-03-15", false);
        CellDiff diff = Assert.Single(Assert.Single(Compare(left, right).Sheets).Cells!);
        Assert.Equal("number", diff.Left!.T);
        Assert.Equal(45000d, diff.Left.V);
        Assert.Equal("string", diff.Right!.T);
    }

    [Fact]
    public void ErrorAndLiteralText_AreDifferentStoredTypes()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        left.Worksheets[0].Cells["A1"].Formula = "=1/0";
        left.CalculateFormula();
        right.Worksheets[0].Cells["A1"].PutValue("'#DIV/0!", false);
        Assert.Equal(CellValueType.IsString, right.Worksheets[0].Cells["A1"].Type);
        CellDiff diff = Assert.Single(Assert.Single(Compare(left, right, formulas: false).Sheets).Cells!);
        Assert.Equal("error", diff.Left!.T);
        Assert.Equal("string", diff.Right!.T);
        Assert.Equal(diff.Left.V, diff.Right.V);
    }

    [Fact]
    public void EmptyAndEmptyString_AreDifferentButStyledEmptyIsNot()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        right.Worksheets[0].Cells["A1"].PutValue(string.Empty, false);
        CellDiff diff = Assert.Single(Assert.Single(Compare(left, right).Sheets).Cells!);
        Assert.Null(diff.Left);
        Assert.Equal("string", diff.Right!.T);
        Assert.Equal(string.Empty, diff.Right.V);
        right.Worksheets[0].Cells["A1"].PutValue((object?)null);
        Style style = right.CreateStyle();
        style.Font.IsBold = true;
        right.Worksheets[0].Cells["XFD1048576"].SetStyle(style);
        Assert.True(Compare(left, right).Identical);
    }

    [Fact]
    public void FormulaTextAndCachedValues_UseTheRequestedScopeWithoutRecalculation()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        left.Worksheets[0].Cells["A1"].SetFormula("=1+1", 42d);
        right.Worksheets[0].Cells["A1"].SetFormula("=2", 42d);
        Assert.True(Compare(left, right, formulas: false).Identical);
        Assert.False(Compare(left, right, formulas: true).Identical);
        right.Worksheets[0].Cells["A1"].SetFormula("=1+1", "42");
        CellDiff diff = Assert.Single(Assert.Single(Compare(left, right).Sheets).Cells!);
        Assert.Equal("number", diff.Left!.T);
        Assert.Equal("string", diff.Right!.T);
        Assert.Equal(diff.Left.F, diff.Right.F);
        Assert.Equal(42d, left.Worksheets[0].Cells["A1"].DoubleValue);
        Assert.Equal("42", right.Worksheets[0].Cells["A1"].StringValue);
    }

    [Fact]
    public void FormulaWithoutCachedValue_RetainsExplicitEmptyType()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        right.Worksheets[0].Cells["A1"].Formula = "=B1";
        CellDiff diff = Assert.Single(Assert.Single(Compare(left, right).Sheets).Cells!);
        Assert.Null(diff.Left);
        Assert.Equal("empty", diff.Right!.T);
        Assert.Null(diff.Right.V);
        Assert.Equal("=B1", diff.Right.F);
    }

    [Fact]
    public void SparseExtremes_AreComparedInAddressOrderWithoutCreatingCells()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        left.Worksheets[0].Cells["XFD1048576"].PutValue(10);
        left.Worksheets[0].Cells["A1"].PutValue(20);
        right.Worksheets[0].Cells["B2"].PutValue(30);
        int before = left.Worksheets[0].Cells.Count + right.Worksheets[0].Cells.Count;
        DiffComparer.Result result = Compare(left, right);
        Assert.Equal(["A1", "B2", "XFD1048576"], Assert.Single(result.Sheets).Cells!.Select(cell => cell.Cell));
        Assert.Equal(3, result.Summary.CellsDiffering);
        Assert.Equal(before, left.Worksheets[0].Cells.Count + right.Worksheets[0].Cells.Count);
    }

    [Fact]
    public void ListingLimit_IsGlobalAndDoesNotLimitTheTotal()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        left.Worksheets.Add("Second");
        right.Worksheets.Add("Second");
        left.Worksheets[0].Cells["A1"].PutValue(1);
        left.Worksheets[1].Cells["A1"].PutValue(2);
        DiffComparer.Result result = Compare(left, right, maximum: 1);
        Assert.Equal(2, result.Summary.CellsDiffering);
        Assert.Equal(2, result.Summary.SheetsModified);
        Assert.Equal(1, result.Sheets.Sum(sheet => sheet.Cells?.Count ?? 0));
        Assert.True(result.Truncated);
        Assert.False(Compare(left, right, maximum: 2).Truncated);
    }

    [Fact]
    public void WorkBudget_CountsBothInputsAndAllSheets()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        left.Worksheets.Add("Second");
        right.Worksheets.Add("Second");
        foreach (Workbook workbook in new[] { left, right })
        {
            foreach (Worksheet sheet in workbook.Worksheets)
            {
                sheet.Cells["A1"].PutValue(1);
            }
        }
        using var deadline = OperationDeadline.Start(null);
        var budget = new ResourceBudgetLedger(deadline,
            new Dictionary<string, long> { [CellsBudgetDomains.Cells] = 3 });
        Assert.Equal(ErrorCodes.InputBudgetExceeded, Assert.Throws<CliException>(() =>
            DiffComparer.Compare(budget, left, right, true, 1)).Code);
    }

    [Fact]
    public void CancellationAndDeadline_AbortTheComparison()
    {
        using var left = new Workbook();
        using var right = new Workbook();
        using var cancellation = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancellation.Token);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            DiffComparer.Compare(new ResourceBudgetLedger(deadline), left, right, true, 1));
        using var expired = OperationDeadline.FromAbsoluteTick(TimeSpan.FromSeconds(1), Environment.TickCount64 - 1);
        Assert.Equal(ErrorCodes.OperationTimeout, Assert.Throws<CliException>(() =>
            DiffComparer.Compare(new ResourceBudgetLedger(expired), left, right, true, 1)).Code);
    }

    [Fact]
    public void SmallRandomGrids_MatchAnIndependentExhaustiveOracle()
    {
        var random = new Random(193742);
        for (int sample = 0; sample < 40; sample++)
        {
            using var left = new Workbook();
            using var right = new Workbook();
            var expected = new List<string>();
            bool formulas = sample % 2 == 0;
            int maximum = sample % 11;
            for (int row = 0; row < 7; row++)
            {
                for (int column = 0; column < 6; column++)
                {
                    Spec a = Next(random);
                    Spec b = Next(random);
                    Write(left.Worksheets[0], row, column, a);
                    Write(right.Worksheets[0], row, column, b);
                    if (a.Kind != b.Kind || !Equals(a.Value, b.Value)
                        || (formulas && a.Formula != b.Formula))
                    {
                        expected.Add($"{(char)('A' + column)}{row + 1}");
                    }
                }
            }
            DiffComparer.Result actual = Compare(left, right, formulas, maximum);
            Assert.Equal(expected.Count, actual.Summary.CellsDiffering);
            Assert.Equal(expected.Count == 0, actual.Identical);
            Assert.Equal(expected.Count > maximum, actual.Truncated);
            Assert.Equal(expected.Take(maximum), actual.Sheets.SelectMany(sheet => sheet.Cells ?? []).Select(cell => cell.Cell));
        }
    }

    private static Spec Next(Random random) => random.Next(5) switch
    {
        0 => new("empty", null, null),
        1 => new("number", (double)random.Next(4), null),
        2 => new("string", random.Next(4).ToString(System.Globalization.CultureInfo.InvariantCulture), null),
        3 => new("boolean", random.Next(2) == 0, null),
        _ => new("number", (double)random.Next(4), random.Next(2) == 0 ? "=1+1" : "=2"),
    };

    private static void Write(Worksheet sheet, int row, int column, Spec value)
    {
        if (value.Kind == "empty") { return; }
        Cell cell = sheet.Cells[row, column];
        if (value.Formula is { } formula) { cell.SetFormula(formula, value.Value); }
        else { cell.PutValue(value.Value); }
    }

    /// <summary>
    /// A row inserted or deleted in the middle of a sheet shifts the rows below it, so cells
    /// compared by address pair different rows; the comparison names where the rows shifted, and
    /// an edit in place shifts nothing.
    /// </summary>
    [Fact]
    public void AnInsertedOrDeletedRow_IsNamedAsAShift()
    {
        using var original = new Workbook();
        Worksheet sheet = original.Worksheets[0];
        sheet.Name = "Quote";
        sheet.Cells["A1"].PutValue("Item");
        sheet.Cells["B1"].PutValue("Amount");
        for (int row = 2; row <= 5; row++)
        {
            sheet.Cells[$"A{row}"].PutValue($"Item {row - 1}");
            sheet.Cells[$"B{row}"].PutValue(row * 100);
        }
        sheet.Cells["A7"].PutValue("Total");
        sheet.Cells["B7"].Formula = "=SUM(B2:B5)";
        sheet.Cells["A9"].PutValue("Valid for 30 days");
        using Workbook left = Reload(original);
        using Workbook inserted = Reload(original);
        Worksheet changed = inserted.Worksheets[0];
        changed.Cells.InsertRows(5, 1);
        changed.Cells["A6"].PutValue("Support");
        changed.Cells["B6"].PutValue(900);
        changed.Cells["B2"].PutValue(250);
        changed.Cells["A10"].PutValue("Valid for 15 days");
        using Workbook right = Reload(inserted);
        using Workbook edited = Reload(original);
        edited.Worksheets[0].Cells["B2"].PutValue(250);

        Warning shift = Assert.Single(Compare(left, right).Warnings);
        Warning reverse = Assert.Single(Compare(right, left).Warnings);

        Assert.Equal(("ROWS_SHIFTED", "Quote"), (shift.Code.Name, shift.Location));
        Assert.Contains("1 row inserted at right row 6", shift.Message, StringComparison.Ordinal);
        Assert.Contains("1 row deleted at left row 6", reverse.Message, StringComparison.Ordinal);
        Assert.Empty(Compare(left, edited).Warnings);
    }

    /// <summary>
    /// A row whose first cell holds a label no other row of its sheet has keeps that label as its
    /// identity, so a period comparison that changed every value still names the inserted row,
    /// while a label that repeats, such as a region in a log, does not pair rows on its own.
    /// </summary>
    [Fact]
    public void ARowInsertedWhileEveryValueChanged_IsNamedByItsUniqueLabel()
    {
        using var august = new Workbook();
        using var september = new Workbook();
        string[] lines = ["Revenue", "Cost of sales", "Gross profit", "Selling", "Admin", "Finance", "Profit before tax", "Tax", "Net profit"];
        Fill(august.Worksheets[0], lines, factor: 1);
        Fill(september.Worksheets[0], [.. lines[..5], "R&D (allocated)", .. lines[5..]], factor: 3);
        using var log = new Workbook();
        using var relabeled = new Workbook();
        Fill(log.Worksheets[0], ["East", "West", "East", "West"], factor: 1);
        Fill(relabeled.Worksheets[0], ["East", "East", "East", "West"], factor: 2);

        Warning shift = Assert.Single(Compare(august, september).Warnings);

        Assert.Contains("1 row inserted at right row 7", shift.Message, StringComparison.Ordinal);
        Assert.Empty(Compare(log, relabeled).Warnings);

        static void Fill(Worksheet sheet, string[] labels, int factor)
        {
            sheet.Cells["A1"].PutValue("Line");
            sheet.Cells["B1"].PutValue("Amount");
            for (int row = 0; row < labels.Length; row++)
            {
                sheet.Cells[row + 1, 0].PutValue(labels[row]);
                sheet.Cells[row + 1, 1].PutValue((row + 1) * 1000 * factor);
            }
        }
    }

    [Fact]
    public void ARenamedSheet_PairsWithTheSheetItWasAndListsItsCells()
    {
        using var original = new Workbook();
        original.Worksheets[0].Name = "Data";
        original.Worksheets[original.Worksheets.Add()].Name = "Second";
        original.Worksheets["Second"].Cells["A1"].PutValue("before");
        using Workbook left = Reload(original);
        using Workbook edited = Reload(original);
        edited.Worksheets["Second"].Name = "Renamed";
        edited.Worksheets["Renamed"].Cells["A1"].PutValue("after");
        edited.Worksheets.RemoveAt("Data");
        edited.Worksheets[edited.Worksheets.Add()].Name = "Other";
        using Workbook right = Reload(edited);

        DiffComparer.Result result = Compare(left, right);

        Assert.Equal(["Data:removed:", "Renamed:renamed:Second", "Other:added:"],
            result.Sheets.Where(static sheet => !sheet.Name.StartsWith("Evaluation", StringComparison.Ordinal))
                .Select(static sheet => $"{sheet.Name}:{sheet.Status}:{sheet.From}"));
        CellDiff cell = Assert.Single(result.Sheets.Single(static sheet => sheet.Status == "renamed").Cells!);
        Assert.Equal(("A1", "before", "after"), (cell.Cell, cell.Left!.V, cell.Right!.V));
        Assert.Equal(1, result.Summary.SheetsRenamed);
        Assert.False(result.Identical);
    }

    private static Workbook Reload(Workbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.Save(stream, SaveFormat.Xlsx);
        stream.Position = 0;
        return new Workbook(stream);
    }

    private static DiffComparer.Result Compare(Workbook left, Workbook right, bool formulas = true, int maximum = 100)
    {
        using var deadline = OperationDeadline.Start(null);
        var budgets = new ResourceBudgetLedger(deadline,
            new Dictionary<string, long> { [CellsBudgetDomains.Cells] = 10000 });
        return DiffComparer.Compare(budgets, left, right, formulas, maximum);
    }

    private sealed record Spec(string Kind, object? Value, string? Formula);
}
