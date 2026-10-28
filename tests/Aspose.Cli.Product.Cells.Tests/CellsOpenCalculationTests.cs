using Aspose.Cli.Product.Cells.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A workbook that asks to be calculated when opened reads as Excel shows it: formulas stored
/// without results (as openpyxl and many exporters write them) read as calculated values, and the
/// read discloses that the values differ from the stored ones.
/// </summary>
public sealed class CellsOpenCalculationTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsOpenCalculationTests(CellsFixture fixture) => _fixture = fixture;

    [Fact]
    public void UncalculatedFormulas_ReadAsCalculatedValuesAndAreDisclosed()
    {
        string path = Workbook("uncalculated.xlsx", calculate: false);

        CellsReadResult read = CellsRead.Run(_fixture.Session, new CellsReadRequest { Input = path });

        Assert.Equal("42", read.Sheet!.Cells![0][2].V?.ToString());
        Assert.Contains(read.Warnings ?? [], static warning => warning.Code.Name == "FORMULAS_CALCULATED_ON_OPEN");
    }

    [Fact]
    public void CalculatedWorkbook_ReadsWithoutADisclosure()
    {
        string path = Workbook("calculated.xlsx", calculate: true);

        CellsReadResult read = CellsRead.Run(_fixture.Session, new CellsReadRequest { Input = path });

        Assert.Equal("42", read.Sheet!.Cells![0][2].V?.ToString());
        Assert.DoesNotContain(read.Warnings ?? [], static warning => warning.Code.Name == "FORMULAS_CALCULATED_ON_OPEN");
    }

    /// <summary>
    /// A read of a workbook whose volatile formulas change on every load states the counts in the
    /// warning's details, so its message stays the same from one load to the next.
    /// </summary>
    [Fact]
    public void VolatileFormulas_CountTheDifferingResultsInTheDetails()
    {
        string path = _fixture.Temp.File("volatile.xlsx");
        CreateVolatileWorkbook(path);

        CellsReadResult read = CellsRead.Run(_fixture.Session, new CellsReadRequest { Input = path });

        Aspose.Cli.Sdk.Contracts.Warning warning = Assert.Single(read.Warnings ?? [], static warning => warning.Code.Name == "FORMULAS_CALCULATED_ON_OPEN");
        Assert.DoesNotMatch(@"\d", warning.Message);
        Assert.NotNull(warning.Details);
        Assert.Equal(VolatileFormulaCount, warning.Details["formulaCount"]!.GetValue<int>());
        Assert.InRange(warning.Details["differingCount"]!.GetValue<int>(), 1, VolatileFormulaCount);
        Assert.Equal(0, warning.Details["storedWithoutResultCount"]!.GetValue<int>());
    }

    /// <summary>The number of volatile formulas in <see cref="CreateVolatileWorkbook"/>.</summary>
    internal const int VolatileFormulaCount = 200;

    /// <summary>
    /// Writes a workbook that asks to be calculated when opened, with stored results for
    /// <see cref="VolatileFormulaCount"/> formulas <c>=RANDBETWEEN(1,2)</c>: each load recalculates
    /// about half of them to a different result, so two loads almost never count the same number.
    /// </summary>
    internal static void CreateVolatileWorkbook(string path)
    {
        using var workbook = new Aspose.Cells.Workbook();
        Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
        for (int row = 0; row < VolatileFormulaCount; row++)
        {
            cells[row, 0].Formula = "=RANDBETWEEN(1,2)";
        }

        workbook.CalculateFormula();
        workbook.Settings.FormulaSettings.CalculateOnOpen = true;
        workbook.Save(path, Aspose.Cells.SaveFormat.Xlsx);
    }

    private string Workbook(string name, bool calculate)
    {
        string path = _fixture.Temp.File(name);
        using var workbook = new Aspose.Cells.Workbook();
        Aspose.Cells.Cells cells = workbook.Worksheets[0].Cells;
        cells["A1"].PutValue(40);
        cells["B1"].PutValue(2);
        cells["C1"].Formula = "=A1+B1";
        if (calculate)
        {
            workbook.CalculateFormula();
        }

        workbook.Settings.FormulaSettings.CalculateOnOpen = true;
        workbook.Save(path, Aspose.Cells.SaveFormat.Xlsx);
        return path;
    }
}
