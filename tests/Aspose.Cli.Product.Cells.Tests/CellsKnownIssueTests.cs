using System.Text;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The Aspose.Cells defects in KNOWN-ISSUES.md, reproduced with the SDK alone. Each passes while
/// the pinned SDK still has its defect.
/// </summary>
public sealed class CellsKnownIssueTests
{
    [LicensedFact]
    public async Task SvgPictures_FetchTheirImagesPastTheResourceProvider()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        await using var server = new ResourceHttpServer();
        byte[] svg = Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40"><image xlink:href="{server.Url}/svg-image.png" width="20" height="20"/><rect y="25" width="10" height="10"/></svg>""");

        using (var workbook = new Workbook())
        {
            workbook.Settings.ResourceProvider = new RefuseEveryResource();
            using (var stream = new MemoryStream(svg, writable: false))
            {
                workbook.Worksheets[0].Pictures.Add(1, 1, stream);
            }
            workbook.Save(fixture.Temp.File("svg.xlsx"));
        }

        KnownIssue.Reproduces(
            "CELLS-SVG-EGRESS",
            server.RequestCount > 0,
            "adding an SVG picture made no request past a provider that refuses every resource");
    }

    [LicensedFact]
    public void OneCallSparklineAdd_RejectsASheetNameWithAnApostrophe()
    {
        using var fixture = new CellsFixture();
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "O'Brien";
        for (int column = 0; column < 3; column++)
        {
            sheet.Cells[0, column].PutValue(column + 1);
        }

        Exception? failure = Record.Exception(() => sheet.SparklineGroups.Add(
            SparklineType.Line, "'O''Brien'!A1:C1", false, CellArea.CreateCellArea(4, 4, 4, 4)));

        KnownIssue.Reproduces(
            "CELLS-SPARKLINE-APOSTROPHE",
            failure?.Message.StartsWith("Invalid \"'\"", StringComparison.Ordinal) == true,
            failure is null ? "the one-call Add accepted 'O''Brien'!A1:C1" : failure.Message);
    }

    [LicensedFact]
    public void CopyingASheetFromAnotherWorkbook_ScopesAClashingNameToTheFirstSheet()
    {
        using var source = new Workbook();
        source.Worksheets[0].Name = "Raw";
        source.Worksheets.Add("Rates").Cells["A1"].PutValue(3);
        int sourceName = source.Worksheets.Names.Add("Rate");
        source.Worksheets.Names[sourceName].RefersTo = "=Rates!$A$1";
        source.Worksheets[0].Cells["B1"].Formula = "=Rate*2";

        using var target = new Workbook();
        Worksheet report = target.Worksheets[0];
        report.Name = "Report";
        report.Cells["A1"].PutValue(100);
        target.Worksheets.Add("Rates").Cells["A1"].PutValue(7);
        int targetName = target.Worksheets.Names.Add("Rate");
        target.Worksheets.Names[targetName].RefersTo = "=Report!$A$1";
        report.Cells["B1"].Formula = "=Rate";
        target.Worksheets.Add("Imported").Copy(source.Worksheets[0], new CopyOptions());

        using var saved = new MemoryStream();
        target.Save(saved, SaveFormat.Xlsx);
        saved.Position = 0;
        using var reopened = new Workbook(saved);
        reopened.CalculateFormula();
        double reportRate = reopened.Worksheets["Report"].Cells["B1"].DoubleValue;

        KnownIssue.Reproduces(
            "CELLS-COPY-NAME-SCOPE",
            reportRate != 100,
            "the destination sheet's own formula still reads its workbook-level name after the copy");
    }

    /// <summary>The documented refusal: skip every resource and supply no stream.</summary>
    private sealed class RefuseEveryResource : IStreamProvider
    {
        public void InitStream(StreamProviderOptions options)
        {
            options.ResourceLoadingType = ResourceLoadingType.Skip;
            options.Stream = null;
        }

        public void CloseStream(StreamProviderOptions options)
        {
        }
    }
}
