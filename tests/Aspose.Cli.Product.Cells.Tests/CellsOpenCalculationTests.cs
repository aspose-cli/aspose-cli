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

        WorkbookReadResult read = _fixture.Engine.Read(path, new ReadRequest());

        Assert.Equal("42", read.Sheet!.Cells![0][2].V?.ToString());
        Assert.Contains(read.Warnings ?? [], static warning => warning.Code == "FORMULAS_CALCULATED_ON_OPEN");
    }

    [Fact]
    public void CalculatedWorkbook_ReadsWithoutADisclosure()
    {
        string path = Workbook("calculated.xlsx", calculate: true);

        WorkbookReadResult read = _fixture.Engine.Read(path, new ReadRequest());

        Assert.Equal("42", read.Sheet!.Cells![0][2].V?.ToString());
        Assert.DoesNotContain(read.Warnings ?? [], static warning => warning.Code == "FORMULAS_CALCULATED_ON_OPEN");
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
