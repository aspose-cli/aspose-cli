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

    [LicensedFact]
    public void CopyingASheetFromAnotherWorkbook_GivesALinkWithoutCachedValuesAnEmptyCache()
    {
        using var source = new Workbook();
        Cell linked = source.Worksheets[0].Cells["A1"];
        linked.Formula = "='[missing-rates.xlsx]Rates'!$B$2";
        source.CalculateFormula();

        using var target = new Workbook();
        Worksheet copy = target.Worksheets[target.Worksheets.Add()];
        copy.Copy(source.Worksheets[0], new CopyOptions());
        target.Worksheets[0].Cells.CreateRange("B1").Copy(
            source.Worksheets[0].Cells.CreateRange("A1"), new PasteOptions { PasteType = PasteType.All });
        target.CalculateFormula();
        Cell sheetCopy = copy.Cells["A1"];
        Cell rangeCopy = target.Worksheets[0].Cells["B1"];

        KnownIssue.Reproduces(
            "CELLS-COPY-EXTERNAL-CACHE",
            linked.Type == CellValueType.IsError
                && sheetCopy.Type != CellValueType.IsError && rangeCopy.Type != CellValueType.IsError,
            $"the source evaluates to {linked.Value}, the sheet copy to {sheetCopy.Value} and the range copy to {rangeCopy.Value}");
    }

    [LicensedFact]
    public void ALinkToAFileBesideTheOpenedWorkbook_IsSavedAsARelativePathMissingTarget()
    {
        using var fixture = new CellsFixture();
        string opened = fixture.Temp.File(Path.Combine("opened", "book.xlsx"));
        Directory.CreateDirectory(Path.GetDirectoryName(opened)!);
        using (var created = new Workbook())
        {
            created.Save(opened, SaveFormat.Xlsx);
        }
        string saved = fixture.Temp.File(Path.Combine("elsewhere", "book.xlsx"));
        Directory.CreateDirectory(Path.GetDirectoryName(saved)!);

        using (var workbook = new Workbook(opened))
        {
            workbook.Worksheets[0].Cells["A1"].Formula = "='" + Path.GetDirectoryName(opened) + "\\[fx.xlsx]Rates'!$B$2";
            workbook.Save(saved, SaveFormat.Xlsx);
        }

        using var package = System.IO.Compression.ZipFile.OpenRead(saved);
        using var rels = new StreamReader(package.GetEntry("xl/externalLinks/_rels/externalLink1.xml.rels")!.Open());
        System.Xml.Linq.XElement link = System.Xml.Linq.XDocument.Parse(rels.ReadToEnd()).Root!.Elements().Single();
        string type = (string)link.Attribute("Type")!;
        string target = (string)link.Attribute("Target")!;

        KnownIssue.Reproduces(
            "CELLS-LINK-RELATIVE-TARGET",
            type.EndsWith("/xlPathMissing", StringComparison.Ordinal) && target == "fx.xlsx",
            $"the link to the opened folder's fx.xlsx, saved to another folder, has type {type} and target {target}");
    }

    [LicensedFact]
    public void ValueWidth_MeasuresEastAsianTextInALatinFontNarrowerThanAutoFitAndRendering()
    {
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        Cell latin = Put(sheet, 0, "Accessories");
        Cell eastAsian = Put(sheet, 1, "配件配件配件配件");
        sheet.AutoFitColumns();
        // AutoFit adds the same padding to every value it measures in one font.
        int padding = sheet.Cells.GetColumnWidthPixel(0) - latin.GetWidthOfValue();
        int shortfall = sheet.Cells.GetColumnWidthPixel(1) - padding - eastAsian.GetWidthOfValue();

        KnownIssue.Reproduces(
            "CELLS-WIDTH-EAST-ASIAN",
            shortfall > 10,
            $"GetWidthOfValue measures the East Asian text {eastAsian.GetWidthOfValue()}px wide, {shortfall}px short of the width AutoFit gives it");

        static Cell Put(Worksheet sheet, int column, string text)
        {
            Cell cell = sheet.Cells[0, column];
            cell.PutValue(text);
            Style style = cell.GetStyle();
            style.Font.Name = "Calibri";
            style.Font.Size = 10;
            cell.SetStyle(style);
            return cell;
        }
    }

    /// <summary>
    /// Long text in A1 spills over the empty B1 and C1. For each width of column B, the right edge
    /// of its ink is compared with its edge when B is wide enough to hold it: the page layout of a
    /// whole-sheet render cuts it at some width, while the print-area layout of a range render
    /// draws it in full at every width.
    /// </summary>
    [LicensedFact]
    public void PageLayout_CutsOverflowingTextThatEndsAtAColumnEdge()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        _ = fixture.LicenseState;
        double[] widths = [.. Enumerable.Range(0, 21).Select(static step => 24 + step * 0.25)];

        double[] cutInPage = [.. widths.Where(width => InkRight(width, onlyArea: false) < InkRight(100, onlyArea: false))];
        double[] cutInArea = [.. widths.Where(width => InkRight(width, onlyArea: true) < InkRight(100, onlyArea: true))];

        KnownIssue.Reproduces(
            "CELLS-OVERFLOW-EDGE",
            cutInPage.Length > 0 && cutInArea.Length == 0,
            $"page layout cut the text at column B widths [{string.Join(", ", cutInPage)}], print-area layout at [{string.Join(", ", cutInArea)}]");

        int InkRight(double columnB, bool onlyArea)
        {
            using var workbook = new Workbook();
            Style normal = workbook.DefaultStyle;
            normal.Font.Name = "Microsoft YaHei";
            normal.Font.Size = 10;
            workbook.DefaultStyle = normal;
            Worksheet sheet = workbook.Worksheets[0];
            Cell text = sheet.Cells["A1"];
            text.PutValue("查询条件：日期 2026-09-01 至 2026-09-30；部门：全部");
            Style red = text.GetStyle();
            red.Font.Color = System.Drawing.Color.Red;
            text.SetStyle(red);
            sheet.Cells["D1"].PutValue("end");
            sheet.Cells.SetColumnWidth(0, 14);
            sheet.Cells.SetColumnWidth(1, columnB);
            sheet.Cells.SetColumnWidth(2, 12);
            string path = fixture.Temp.File($"overflow-{columnB}-{onlyArea}.png");
            new Aspose.Cells.Rendering.SheetRender(sheet, new Aspose.Cells.Rendering.ImageOrPrintOptions
            {
                ImageType = Aspose.Cells.Drawing.ImageType.Png,
                OnePagePerSheet = true,
                OnlyArea = onlyArea,
            }).ToImage(0, path);
            using SkiaSharp.SKBitmap image = SkiaSharp.SKBitmap.Decode(path);
            int right = -1;
            for (int pixel = 0; pixel < image.Width * image.Height; pixel++)
            {
                SkiaSharp.SKColor color = image.GetPixel(pixel % image.Width, pixel / image.Width);
                if (color.Red > 150 && color.Green < 120 && color.Blue < 120)
                {
                    right = Math.Max(right, pixel % image.Width);
                }
            }
            return right;
        }
    }

    /// <summary>
    /// A pivot with a column field and two value fields labels each value field's grand-total
    /// column with the word of <see cref="Aspose.Cells.Settings.PivotGlobalizationSettings.GetTextOfTotal"/>
    /// before the caption, so Excel's Simplified Chinese form, the caption followed by 汇总, is out of reach.
    /// </summary>
    [LicensedFact]
    public void PivotGrandTotalColumns_PutTheTotalWordBeforeTheCaption()
    {
        using var fixture = new CellsFixture();
        _ = fixture.LicenseState;
        using var workbook = new Workbook();
        workbook.Settings.GlobalizationSettings = new ChineseTotals();
        Worksheet data = workbook.Worksheets[0];
        object[][] rows = [["地区", "产品", "金额", "数量"], ["北京", "甲", 10, 1], ["上海", "乙", 20, 2]];
        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                data.Cells[row, column].PutValue(rows[row][column]);
            }
        }

        Worksheet sheet = workbook.Worksheets.Add("Pivot");
        Aspose.Cells.Pivot.PivotTable pivot = sheet.PivotTables[sheet.PivotTables.Add("=Sheet1!A1:D3", "A1", "P")];
        pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Row, "地区");
        pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Column, "产品");
        pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Data, "金额");
        pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Data, "数量");
        pivot.DataFields[0].DisplayName = "求和项:金额";
        pivot.DataFields[1].DisplayName = "求和项:数量";
        pivot.AddFieldToArea(Aspose.Cells.Pivot.PivotFieldType.Column, pivot.ValuesField);
        pivot.CalculateData();

        string[] labels = [.. sheet.Cells.Cast<Cell>().Select(static cell => cell.StringValue).Where(static text => text.Contains("汇总", StringComparison.Ordinal))];
        KnownIssue.Reproduces(
            "CELLS-PIVOT-TOTAL-CAPTION",
            labels.SequenceEqual(["汇总 求和项:金额", "汇总 求和项:数量"]),
            $"the grand-total columns read [{string.Join(", ", labels)}]");
    }

    private sealed class ChineseTotals : GlobalizationSettings
    {
        public ChineseTotals() => PivotSettings = new ChinesePivotTotals();
    }

    private sealed class ChinesePivotTotals : Aspose.Cells.Settings.PivotGlobalizationSettings
    {
        public override string GetTextOfTotal() => "汇总";
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
