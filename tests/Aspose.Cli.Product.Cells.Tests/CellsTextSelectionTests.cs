using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Text export must never substitute another worksheet silently.</summary>
public sealed class CellsTextSelectionTests : IClassFixture<CellsTextSelectionFixture>
{
    private readonly CellsFixture _fixture;

    private readonly CellsTextSelectionFixture _sources;

    public CellsTextSelectionTests(CellsTextSelectionFixture fixture)
    {
        _sources = fixture;
        _fixture = fixture.Runtime;
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void Convert_SelectedNonFirstSheet_ExportsIt(string format)
    {
        string source = _sources.DashboardActive;
        string output = _fixture.Temp.File($"text-selection.{format}");
        byte[] sentinel = "existing report must remain intact"u8.ToArray();
        File.WriteAllBytes(output, sentinel);
        byte[] sourceBytes = File.ReadAllBytes(source);
        var request = new ConvertRequest
        {
            Input = source,
            Output = TestOutput.At(output, format: format, overwrite: true),
            SheetName = "Detail",
        };

        ConvertResult result = CellsConvert.Run(_fixture.Session, request);
        Assert.Equal("Detail", result.Sheet);
        string text = File.ReadAllText(output);
        Assert.Contains("SO-001", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Executive overview", text, StringComparison.Ordinal);

        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void Convert_FirstSheet_RemainsAvailableWithTruthfulContent(string format)
    {
        string source = _sources.DashboardActive;
        string output = _fixture.Temp.File($"text-first.{format}");
        ConvertResult result = CellsConvert.Run(_fixture.Session, new ConvertRequest
        {
            Input = source,
            Output = TestOutput.At(output, format: format, overwrite: true),
            SheetName = "Dashboard",
        });

        Assert.Equal("Dashboard", result.Sheet);
        string text = File.ReadAllText(output);
        Assert.Contains("Executive overview", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SO-001", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void Convert_DefaultSelection_ReportsTheSheetActuallyExported(string format)
    {
        string source = _sources.DetailActive;
        string output = _fixture.Temp.File($"text-default.{format}");
        ConvertResult result = CellsConvert.Run(_fixture.Session, new ConvertRequest
        {
            Input = source,
            Output = TestOutput.At(output, format: format, overwrite: true),
        });

        string text = File.ReadAllText(output);
        Assert.Contains("SO-001", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Executive overview", text, StringComparison.Ordinal);
        Assert.Contains(result.Warnings!, static warning => warning.Code == "SHEETS_DROPPED"
            && warning.Message.Contains("'Detail'", StringComparison.Ordinal));
    }
}

/// <summary>Immutable inputs shared by the text-export cases; outputs remain per test.</summary>
public sealed class CellsTextSelectionFixture : IDisposable
{
    internal CellsFixture Runtime { get; } = new();
    internal string DashboardActive { get; }
    internal string DetailActive { get; }

    public CellsTextSelectionFixture()
    {
        using var workbook = new Workbook();
        Worksheet dashboard = workbook.Worksheets[0];
        dashboard.Name = "Dashboard";
        dashboard.Cells["A1"].PutValue("Executive overview");
        dashboard.Cells["B1"].PutValue(1276.5);
        Worksheet detail = workbook.Worksheets.Add("Detail");
        detail.Cells["A1"].PutValue("Order");
        detail.Cells["B1"].PutValue("Net revenue");
        detail.Cells["A2"].PutValue("SO-001");
        detail.Cells["B2"].PutValue(1234.5);
        detail.Cells["A3"].PutValue("SO-002");
        detail.Cells["B3"].PutValue(42);
        DashboardActive = Runtime.Temp.File("text-source-dashboard.xlsx");
        DetailActive = Runtime.Temp.File("text-source-detail.xlsx");
        workbook.Worksheets.ActiveSheetIndex = dashboard.Index;
        workbook.Save(DashboardActive, SaveFormat.Xlsx);
        workbook.Worksheets.ActiveSheetIndex = detail.Index;
        workbook.Save(DetailActive, SaveFormat.Xlsx);
    }

    public void Dispose() => Runtime.Dispose();
}
