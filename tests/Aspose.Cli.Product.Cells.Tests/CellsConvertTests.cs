using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Conversions with a license: the sheet and the value strategy a text output is written with.</summary>
public sealed class CellsConvertTests
{
    [Theory]
    [InlineData(null, "Revenue,\"13,530\"")]
    [InlineData("None", "Revenue,13529.5")]
    [InlineData("DisplayString", "Revenue,\"13,530\"")]
    public void ConvertToCsv_WritesTheSheetNamed_WithTheStrategyStated(string? strategy, string expected)
    {
        using var fixture = new CellsFixture();
        string input = fixture.CreateSales();
        using (var workbook = new Workbook(input))
        {
            Cell revenue = workbook.Worksheets["汇总 Summary"].Cells["B1"];
            Style style = revenue.GetStyle();
            style.Custom = "#,##0";
            revenue.SetStyle(style);
            workbook.Worksheets["销售 Sales"].Cells["B2"].PutValue(1234.5);
            workbook.CalculateFormula();
            workbook.Save(input);
        }

        string output = fixture.File("summary.csv");

        CellsConvertResult result = CellsConvert.Run(fixture.Session, new CellsConvertRequest
        {
            Input = input,
            Output = CellsOutput.At(output, "csv"),
            Sheets = ["汇总 SUMMARY"],
            FormatStrategy = strategy,
        });

        Assert.Equal(["汇总 SUMMARY"], result.Sheets);
        Assert.StartsWith(expected, File.ReadAllText(output), StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_OfASheetNoneHas_IsNotFoundAndWritesNothing()
    {
        using var fixture = new CellsFixture();
        string input = fixture.CreateSales();
        string output = fixture.File("missing.pdf");

        CliException error = Assert.Throws<CliException>(() => CellsConvert.Run(fixture.Session, new CellsConvertRequest
        {
            Input = input,
            Output = CellsOutput.At(output, "pdf"),
            Sheets = ["Missing"],
        }));

        Assert.Equal(ErrorCodes.AddressNotFound, error.Code);
        Assert.False(File.Exists(output));
    }
}
