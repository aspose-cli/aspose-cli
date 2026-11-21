using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Aspose.Slides.Warnings;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The Aspose.Slides defects in KNOWN-ISSUES.md: each reproduced with the SDK alone, passing
/// while the pinned SDK still has it. The class runs alone because one reproduction redirects
/// standard output.
/// </summary>
[Collection(ProcessRedirectionCollection.Name)]
public sealed class SlidesKnownIssueTests
{
    private const string FontRegistryHiveVariable = "ASPOSE_CLI_TEST_FONT_REGISTRY_HIVE";

    // The per-user Fonts key Aspose.Slides reads at font initialization.
    private const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

    private static string ImplicitTitleDeck =>
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Product.Slides.Tests", "Assets", "implicit-chart-title.pptx");

    private static string DefaultDesign => Path.Combine(AppContext.BaseDirectory, "Assets", "default-16x9.pptx");

    [LicensedFact]
    public void TextThatShrinksOnOverflow_IsLaidOutPastTheRightOfItsFrame()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IAutoShape body = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 100, 640, 257);
        body.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.Normal;
        body.TextFrame.Paragraphs.Clear();
        for (int item = 0; item < 12; item++)
        {
            var paragraph = new Paragraph();
            paragraph.ParagraphFormat.MarginLeft = 34;
            paragraph.ParagraphFormat.Indent = -27;
            paragraph.ParagraphFormat.Bullet.Type = BulletType.Symbol;
            paragraph.ParagraphFormat.Bullet.Char = '•';
            paragraph.Portions.Add(new Portion("培训 Training 10：每季度一次线下集训，线上课程全年开放 on-demand courses for every partner"));
            paragraph.Portions[0].PortionFormat.FontHeight = 18;
            body.TextFrame.Paragraphs.Add(paragraph);
        }

        double right = body.TextFrame.Paragraphs.Max(static paragraph => paragraph.GetRect().Right);
        double textRight = body.Width - body.TextFrame.TextFrameFormat.GetEffective().MarginRight;

        KnownIssue.Reproduces(
            "SLIDES-AUTOFIT-RECT",
            right > textRight,
            $"the widest laid-out line ends at {right} pt, inside the frame's text area ending at {textRight} pt");
    }

    [LicensedFact]
    public void ImplicitAutomaticTitle_LoadsAsATitleOverThePlot()
    {
        using var fixture = new SlidesEngineFixture();
        using (ZipArchive package = ZipFile.OpenRead(ImplicitTitleDeck))
        using (Stream part = package.GetEntry("ppt/charts/chart1.xml")!.Open())
        {
            XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
            XElement chart = XDocument.Load(part).Root!.Element(c + "chart")!;
            Assert.Null(chart.Element(c + "title"));
            Assert.Equal("0", chart.Element(c + "autoTitleDeleted")!.Attribute("val")!.Value);
        }

        using var presentation = new Presentation(ImplicitTitleDeck);
        IChart loaded = presentation.Slides.SelectMany(static slide => slide.Shapes).OfType<IChart>().Single();

        KnownIssue.Reproduces(
            "SLIDES-CHART-TITLE",
            loaded.HasTitle && loaded.ChartTitle.Overlay,
            $"an implicit automatic title loads as HasTitle={loaded.HasTitle}, Overlay={loaded.HasTitle && loaded.ChartTitle.Overlay}");
    }

    [LicensedFact]
    public void PptLoad_ListsLayoutsThatAPptxSaveCannotReference()
    {
        using var fixture = new SlidesEngineFixture();
        using var ppt = new MemoryStream();
        int pptxLayouts;
        using (var source = new Presentation(DefaultDesign))
        {
            pptxLayouts = source.LayoutSlides.Count;
            source.Save(ppt, SaveFormat.Ppt);
        }

        ppt.Position = 0;
        using var loaded = new Presentation(ppt);
        var unwritable = new List<string>();
        for (int index = 0; index < loaded.LayoutSlides.Count; index++)
        {
            ppt.Position = 0;
            using var deck = new Presentation(ppt);
            deck.Slides.AddEmptySlide(deck.LayoutSlides[index]);
            try
            {
                deck.Save(new MemoryStream(), SaveFormat.Pptx);
            }
            catch (KeyNotFoundException)
            {
                unwritable.Add($"{index}: {deck.LayoutSlides[index].Name}");
            }
        }

        KnownIssue.Reproduces(
            "SLIDES-PPT-TEMPLATE-LAYOUTS",
            unwritable.Count > 0,
            $"{pptxLayouts} layouts saved as PPT load as {loaded.LayoutSlides.Count}; a slide on layout [{string.Join(", ", unwritable)}] fails the PPTX save");
    }

    [LicensedFact]
    public void RenderingWithAFallbackRule_WritesToStandardOutput()
    {
        using var fixture = new SlidesEngineFixture();
        using var captured = new StringWriter();
        TextWriter original = Console.Out;
        using var presentation = new Presentation();
        try
        {
            Console.SetOut(captured);
            presentation.FontsManager.FontFallBackRulesCollection.Add(new FontFallBackRule(0x4E00, 0x9FFF, "SimSun"));
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "回退规则";
            presentation.Save(fixture.File("fallback.pdf"), SaveFormat.Pdf);
        }
        finally
        {
            Console.SetOut(original);
        }

        KnownIssue.Reproduces(
            "SLIDES-FALLBACK-STDOUT",
            captured.ToString().Length > 0,
            "rendering with a fallback rule wrote nothing to standard output");
    }

    /// <summary>
    /// Slides reads the per-user Fonts key once per process, so the reproduction reruns this test
    /// in a new test process whose current-user registry is an application hive file. The user's
    /// registry is never written.
    /// </summary>
    [LicensedFact]
    [SupportedOSPlatform("windows")]
    public void FontInitialization_BreaksOnARegistryValueThatIsNotAString()
    {
        Requires.Windows();
        string? hive = Environment.GetEnvironmentVariable(FontRegistryHiveVariable);
        if (hive is null)
        {
            using var directory = new TempDirectory();
            (int exitCode, string output) = TestProcess.RunAlone(typeof(SlidesKnownIssueTests),
                nameof(FontInitialization_BreaksOnARegistryValueThatIsNotAString), FontRegistryHiveVariable, directory.File("current-user.hive"));
            Assert.True(
                exitCode == 0 && output.Contains("Total: 1, Errors: 0, Failed: 0, Skipped: 0,", StringComparison.Ordinal),
                output);
            return;
        }

        using (RegistryKey fonts = PrivateFontsKey(hive))
        {
            fonts.SetValue("Readable (TrueType)", "arial.ttf", RegistryValueKind.String);
            fonts.SetValue("Other software", 1, RegistryValueKind.DWord);
        }
        int status = RegLoadAppKey(hive, out IntPtr redirected, 0xF003F, 0, 0);
        Assert.True(status == 0, $"RegLoadAppKey failed with {status}.");
        Exception? failure;
        try
        {
            status = RegOverridePredefKey(CurrentUser, redirected);
            Assert.True(status == 0, $"RegOverridePredefKey failed with {status}.");
            using var fixture = new SlidesEngineFixture();
            using var presentation = new Presentation();
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 50).TextFrame.Text = "Font registry";
            failure = Record.Exception(() => presentation.Save(fixture.File("registry.pptx"), SaveFormat.Pptx));
        }
        finally
        {
            _ = RegOverridePredefKey(CurrentUser, IntPtr.Zero);
            _ = RegCloseKey(redirected);
        }
        while (failure?.InnerException is { } inner)
        {
            failure = inner;
        }

        KnownIssue.Reproduces(
            "SLIDES-FONT-REGISTRY",
            failure is InvalidCastException,
            failure is null ? "the save succeeded with a REG_DWORD font value" : failure.ToString());
    }

    // Evaluation mode truncates the text it exports, so only a licensed run sees the break dropped.
    [LicensedFact]
    public void MarkdownExport_DropsALineBreakInsideAParagraph()
    {
        using var fixture = new SlidesEngineFixture();
        string path = fixture.File("break.md");
        using (var presentation = new Presentation())
        {
            IAutoShape shape = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 100);
            shape.TextFrame.Text = "First line\vSecond line";
            presentation.Save(path, SaveFormat.Md);
        }

        string markdown = File.ReadAllText(path);
        KnownIssue.Reproduces(
            "SLIDES-MD-LINE-BREAK",
            markdown.Contains("First lineSecond line", StringComparison.Ordinal),
            $"the Markdown reads \"{markdown.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}\"");
    }

    [LicensedFact]
    public void AlternativeText_LosesItsTabsOnceSaved()
    {
        using var fixture = new SlidesEngineFixture();
        string path = fixture.File("alt.pptx");
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 50, 50).AlternativeText = "a  b\tc";
            presentation.Save(path, SaveFormat.Pptx);
        }

        using var loaded = new Presentation(path);
        string read = loaded.Slides[0].Shapes[0].AlternativeText;

        KnownIssue.Reproduces(
            "SLIDES-ALT-TEXT-TAB",
            read == "a  bc",
            $"alternative text saved as \"a  b\\tc\" reads back as \"{read.Replace("\t", "\\t", StringComparison.Ordinal)}\"");
    }

    /// <summary>
    /// A chart added without sample data is saved with no categories for one series: alone, the
    /// series loses them, from cells or from literals; beside a second series, one has none.
    /// </summary>
    [LicensedFact]
    public void ChartAddedWithoutSampleData_SavesNoCategoriesForOneSeries()
    {
        using var fixture = new SlidesEngineFixture();
        (int Series, int Categories, int Reopened) fromCells = Chart(literal: false, seriesCount: 1);
        (int Series, int Categories, int Reopened) fromLiterals = Chart(literal: true, seriesCount: 1);
        (int Series, int Categories, int Reopened) twoSeries = Chart(literal: false, seriesCount: 2);

        KnownIssue.Reproduces(
            "SLIDES-CHART-NO-SAMPLE-CATEGORIES",
            fromCells is (1, 0, 0) && fromLiterals is (1, 0, 0) && twoSeries is (2, 1, 2),
            $"(c:ser, c:cat, categories reopened) of two categories: one series from cells {fromCells}, from literals {fromLiterals}; two series {twoSeries}");

        static (int Series, int Categories, int Reopened) Chart(bool literal, int seriesCount)
        {
            using var presentation = new Presentation();
            IChart chart = presentation.Slides[0].Shapes.AddChart(ChartType.ClusteredColumn, 10, 10, 300, 200, false);
            IChartDataWorkbook cells = chart.ChartData.ChartDataWorkbook;
            if (literal)
            {
                chart.ChartData.Categories.UseCells = false;
                chart.ChartData.Categories.Add("A");
                chart.ChartData.Categories.Add("B");
            }
            else
            {
                chart.ChartData.Categories.Add(cells.GetCell(0, 1, 0, "A"));
                chart.ChartData.Categories.Add(cells.GetCell(0, 2, 0, "B"));
            }

            for (int index = 0; index < seriesCount; index++)
            {
                IChartSeries series;
                if (literal)
                {
                    series = chart.ChartData.Series.Add($"S{index}", ChartType.ClusteredColumn);
                    series.DataPoints.DataSourceTypeForValues = DataSourceType.DoubleLiterals;
                    series.DataPoints.AddDataPointForBarSeries(10.0);
                    series.DataPoints.AddDataPointForBarSeries(20.0);
                }
                else
                {
                    series = chart.ChartData.Series.Add(cells.GetCell(0, 0, 1 + index, $"S{index}"), ChartType.ClusteredColumn);
                    series.DataPoints.AddDataPointForBarSeries(cells.GetCell(0, 1, 1 + index, 10));
                    series.DataPoints.AddDataPointForBarSeries(cells.GetCell(0, 2, 1 + index, 20));
                }
            }

            byte[] saved = Saved(presentation);
            string xml = Part(saved, "ppt/charts/chart1.xml");
            using Presentation reopened = Opened(saved);
            return (Occurrences(xml, "<c:ser>"), Occurrences(xml, "<c:cat>"), ((IChart)reopened.Slides[0].Shapes[0]).ChartData.Categories.Count);
        }
    }

    [LicensedFact]
    public void ChartAddedWithoutSampleData_FailsToSaveASeriesWithoutANameCell()
    {
        using var fixture = new SlidesEngineFixture();
        Exception? literal = Save(sample: false, literalName: true);
        Exception? unnamed = Save(sample: false, literalName: false);
        Exception? withSample = Save(sample: true, literalName: true);

        KnownIssue.Reproduces(
            "SLIDES-CHART-NO-SAMPLE-NAME",
            literal is NullReferenceException && unnamed is NullReferenceException && withSample is null,
            $"without sample data, a series named by a literal saves with {Name(literal)} and an unnamed one with {Name(unnamed)}; with sample data, the literal name saves with {Name(withSample)}");

        static Exception? Save(bool sample, bool literalName)
        {
            using var presentation = new Presentation();
            IChart chart = presentation.Slides[0].Shapes.AddChart(ChartType.ClusteredColumn, 10, 10, 300, 200, sample);
            chart.ChartData.Series.Clear();
            IChartSeries series = chart.ChartData.Series.Add(ChartType.ClusteredColumn);
            if (literalName)
            {
                series.Name.DataSourceType = DataSourceType.StringLiterals;
                series.Name.AsLiteralString = "N";
            }

            series.DataPoints.DataSourceTypeForValues = DataSourceType.DoubleLiterals;
            series.DataPoints.AddDataPointForBarSeries(5.0);
            return Record.Exception(() => presentation.Save(new MemoryStream(), SaveFormat.Pptx));
        }

        static string Name(Exception? failure) => failure?.GetType().Name ?? "no exception";
    }

    [LicensedFact]
    public void ChartCellOfAMissingWorksheet_CreatesTheWorksheetUnderAnyName()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChartDataWorkbook cells = SampleChart(presentation).ChartData.ChartDataWorkbook;
        _ = cells.GetCell(1, 0, 0);
        _ = cells.GetCell("sheet1", 0, 0);
        _ = cells.GetCell((string?)null, 0, 0);
        string?[] names = [.. cells.Worksheets.Select(static sheet => sheet.Name)];
        string[] saved = [.. XDocument.Parse(WorkbookPart(Saved(presentation), "xl/workbook.xml")).Descendants()
            .Where(static element => element.Name.LocalName == "sheet")
            .Select(static element => (string)element.Attribute("name")!)];

        KnownIssue.Reproduces(
            "SLIDES-CHART-SHEET-CREATED",
            names.SequenceEqual(["Sheet1", "Sheet1", "sheet1", null]) && saved.SequenceEqual(["Sheet1", "Sheet1", "sheet1", ""]),
            $"GetCell of sheet 1, of \"sheet1\" and of a null name leaves the sheets [{string.Join(", ", names.Select(static name => name is null ? "null" : $"\"{name}\""))}], "
                + $"saved as [{string.Join(", ", saved.Select(static name => $"\"{name}\""))}]");
    }

    [LicensedFact]
    public void ChartCellPastTheWorksheetLimits_IsSavedUnderAnInvalidReference()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChartDataWorkbook cells = SampleChart(presentation).ChartData.ChartDataWorkbook;
        _ = cells.GetCell(0, -1, 0, "above");
        _ = cells.GetCell(0, 1048576, 0, "below");
        _ = cells.GetCell(0, 0, 16384, "right");
        string[] references = [.. XDocument.Parse(WorkbookPart(Saved(presentation), "xl/worksheets/sheet1.xml")).Descendants()
            .Where(static element => element.Name.LocalName == "c")
            .Select(static element => (string)element.Attribute("r")!)];

        KnownIssue.Reproduces(
            "SLIDES-CHART-CELL-RANGE",
            references.Contains("A0") && references.Contains("A1048577") && references.Contains("XFE1"),
            $"cells at row -1, row 1048576 and column 16384 are saved as [{string.Join(", ", references.Where(static reference => reference is "A0" or "A1048577" or "XFE1"))}]");
    }

    [LicensedFact]
    public void ReadingAnEmptyChartCell_ChangesTheSavedWorkbook()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChart chart = SampleChart(presentation);
        const string Sheet = "xl/worksheets/sheet1.xml";
        string first = WorkbookPart(Saved(presentation), Sheet);
        string second = WorkbookPart(Saved(presentation), Sheet);
        object? value = chart.ChartData.ChartDataWorkbook.GetCell(0, 300, 70).Value;
        string afterRead = WorkbookPart(Saved(presentation), Sheet);

        KnownIssue.Reproduces(
            "SLIDES-CHART-CELL-READ",
            first == second && value is null && afterRead != first && afterRead.Contains("r=\"BS301\"", StringComparison.Ordinal),
            $"two saves write the same sheet: {first == second}; after reading the empty cell BS301 ({value ?? "null"}) the sheet changed: {afterRead != first}");
    }

    /// <summary>
    /// A point removed from a sample chart before any point value is read leaves the count, but
    /// a save keeps it and the first value read brings it back; after a value is read, the same
    /// removal takes it out.
    /// </summary>
    [LicensedFact]
    public void ChartPointRemovedBeforeAnyValueIsRead_StaysInTheSeries()
    {
        using var fixture = new SlidesEngineFixture();
        (int Count, int Saved, string AfterRead)[] unread = [Remove("RemoveAt", readFirst: false), Remove("Remove", readFirst: false), Remove("IChartDataPoint.Remove", readFirst: false)];
        (int Count, int Saved, string AfterRead) read = Remove("RemoveAt", readFirst: true);

        KnownIssue.Reproduces(
            "SLIDES-CHART-POINT-REMOVE",
            unread.All(static removal => removal == (3, 4, "4 [0, 1, 3, 3]")) && read == (3, 3, "3 [0, 1, 3]"),
            $"(count, count reopened, points once a value is read) after removing point 2 of 4 before any value is read: "
                + $"[{string.Join("; ", unread)}] (RemoveAt, Remove, IChartDataPoint.Remove); after a value is read: {read}");

        static (int Count, int Saved, string AfterRead) Remove(string call, bool readFirst)
        {
            using var presentation = new Presentation();
            IChartDataPointCollection points = SampleChart(presentation).ChartData.Series[0].DataPoints;
            if (readFirst)
            {
                _ = points[0].Value.Data;
            }

            IChartDataPoint middle = points[2];
            switch (call)
            {
                case "RemoveAt":
                    points.RemoveAt(2);
                    break;
                case "Remove":
                    points.Remove(middle);
                    break;
                default:
                    middle.Remove();
                    break;
            }

            int count = points.Count;
            using Presentation reopened = Opened(Saved(presentation));
            int saved = ((IChart)reopened.Slides[0].Shapes[0]).ChartData.Series[0].DataPoints.Count;
            _ = points[0].Value.Data;
            return (count, saved, $"{points.Count} [{string.Join(", ", points.Select(static point => point.Index))}]");
        }
    }

    [LicensedFact]
    public void ChartPointsCreatedUpToAnIndex_AreDroppedBySaving()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChartDataPointCollection points = SampleChart(presentation).ChartData.Series[0].DataPoints;
        IChartDataPoint created = points.GetOrCreateDataPointByIdx(10);
        int inMemory = points.Count;
        using Presentation reopened = Opened(Saved(presentation));
        int saved = ((IChart)reopened.Slides[0].Shapes[0]).ChartData.Series[0].DataPoints.Count;

        KnownIssue.Reproduces(
            "SLIDES-CHART-POINT-BY-INDEX",
            created.Index == 10 && inMemory == 11 && saved == 4,
            $"a series of 4 points with point {created.Index} created has {inMemory} points, and {saved} once saved and reopened");
    }

    [LicensedFact]
    public void ChartOfMixedSeriesTypes_ReordersItsSeriesBySaving()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChart chart = SampleChart(presentation);
        chart.ChartData.Series.Add("Pie", ChartType.Pie);
        string before = Names(chart);
        using Presentation reopened = Opened(Saved(presentation));
        string after = Names((IChart)reopened.Slides[0].Shapes[0]);

        KnownIssue.Reproduces(
            "SLIDES-CHART-MIXED-ORDER",
            before == "Series 1, Series 2, Series 3, Pie" && after == "Pie, Series 1, Series 2, Series 3",
            $"series [{before}] reopen as [{after}]");

        static string Names(IChart chart) => string.Join(", ", chart.ChartData.Series.Select(static series => series.Name.ToString()));
    }

    /// <summary>
    /// The same <see cref="IBaseChartValue.Data"/> setter writes a string into a point value's
    /// cell, refuses it for a point value given as a literal, and refuses any series name read
    /// from a cell.
    /// </summary>
    [LicensedFact]
    public void ChartValueDataSetter_TakesAStringOnlyForAPointValueFromACell()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChart chart = SampleChart(presentation);
        IDoubleChartValue fromCell = chart.ChartData.Series[0].DataPoints[0].Value;
        Exception? cellPoint = Record.Exception(() => { fromCell.Data = "text"; });
        IChartDataPointCollection literals = chart.ChartData.Series[1].DataPoints;
        literals.Clear();
        literals.DataSourceTypeForValues = DataSourceType.DoubleLiterals;
        literals.AddDataPointForBarSeries(10.5);
        Exception? literalPoint = Record.Exception(() => { literals[0].Value.Data = "text"; });
        IStringChartValue name = chart.ChartData.Series[2].Name;
        Exception? cellName = Record.Exception(() => { name.Data = "Name"; });

        KnownIssue.Reproduces(
            "SLIDES-CHART-DATA-SETTER",
            cellPoint is null && fromCell.AsCell.Value is "text" && literalPoint is ArgumentException && cellName is ArgumentException,
            $"Data = \"text\" on a point value from a cell: {Outcome(cellPoint)}; from a literal: {Outcome(literalPoint)}; Data = \"Name\" on a series name from a cell: {Outcome(cellName)}");

        static string Outcome(Exception? failure) => failure is null ? "set" : $"{failure.GetType().Name}: {failure.Message}";
    }

    [LicensedFact]
    public void ChartValueTheChartTypeDoesNotUse_ThrowsNullReferenceWhenSet()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IStringOrDoubleChartValue x = SampleChart(presentation).ChartData.Series[0].DataPoints[0].XValue;
        bool noCell = x.AsCell is null;
        object? before = x.Data;
        Exception? failure = Record.Exception(() => { x.Data = 5.0; });

        KnownIssue.Reproduces(
            "SLIDES-CHART-UNUSED-VALUE",
            noCell && before is null && failure is NullReferenceException,
            $"XValue of a column chart's point has no cell: {noCell}, Data {before ?? "null"}; setting Data threw {failure?.GetType().Name ?? "nothing"}");
    }

    [LicensedFact]
    public void DateCategoryAxis_ReadsAsTextOnceSaved()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IChart chart = SampleChart(presentation);
        chart.Axes.HorizontalAxis.CategoryAxisType = CategoryAxisType.Date;
        CategoryAxisType inMemory = chart.Axes.HorizontalAxis.CategoryAxisType;
        byte[] saved = Saved(presentation);
        string xml = Part(saved, "ppt/charts/chart1.xml");
        using Presentation reopened = Opened(saved);
        CategoryAxisType afterSave = ((IChart)reopened.Slides[0].Shapes[0]).Axes.HorizontalAxis.CategoryAxisType;

        KnownIssue.Reproduces(
            "SLIDES-CATEGORY-AXIS-DATE",
            inMemory == CategoryAxisType.Date && afterSave == CategoryAxisType.Text
                && xml.Contains("<c:catAx>", StringComparison.Ordinal) && !xml.Contains("<c:dateAx>", StringComparison.Ordinal),
            $"a category axis set to {inMemory} is saved as c:dateAx: {xml.Contains("<c:dateAx>", StringComparison.Ordinal)}, and reopens as {afterSave}");
    }

    [LicensedFact]
    public void SlideNumberSetter_MovesTheSlideAsIfNumberingStartedAtOne()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        presentation.FirstSlideNumber = 5;
        int last = presentation.Slides[2].SlideNumber;
        Exception? toLast = Record.Exception(() => { presentation.Slides[0].SlideNumber = last; });
        int firstAfterLast = presentation.FirstSlideNumber;
        Exception? belowFirst = Record.Exception(() => { presentation.Slides[0].SlideNumber = 4; });
        int firstAfterBelow = presentation.FirstSlideNumber;

        KnownIssue.Reproduces(
            "SLIDES-SLIDE-NUMBER-SETTER",
            last == 7 && toLast is PptxEditException && firstAfterLast == 5 && belowFirst is PptxEditException && firstAfterBelow == 4,
            $"slides numbered from 5: setting the first one's number to the last number {last} threw {toLast?.GetType().Name ?? "nothing"}; "
                + $"setting it to 4 threw {belowFirst?.GetType().Name ?? "nothing"} and left FirstSlideNumber {firstAfterBelow}");
    }

    [LicensedFact]
    public void InsertCloneOutOfRange_NamesARangeWithoutTheEnd()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        ISlide source = presentation.Slides[0];
        int end = presentation.Slides.IndexOf(presentation.Slides.InsertClone(presentation.Slides.Count, source));
        Exception? refused = Record.Exception(() => presentation.Slides.InsertClone(presentation.Slides.Count + 1, source));

        KnownIssue.Reproduces(
            "SLIDES-INSERT-CLONE-RANGE",
            end == 1 && refused is PptxEditException && refused.Message.Contains("[0..Count-1]", StringComparison.Ordinal),
            $"InsertClone(Count) put the copy at {end}; InsertClone(Count + 1) threw {refused?.GetType().Name}: {refused?.Message}");
    }

    [LicensedFact]
    public void AddEmptySectionAtTheEnd_IsRefused()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        presentation.Sections.AddSection("First", presentation.Slides[0]);
        presentation.Sections.AddSection("Second", presentation.Slides[1]);
        Exception? section = Record.Exception(() => presentation.Sections.AddEmptySection("End", presentation.Sections.Count));
        int slide = presentation.Slides.IndexOf(presentation.Slides.InsertEmptySlide(presentation.Slides.Count, presentation.LayoutSlides[0]));

        KnownIssue.Reproduces(
            "SLIDES-SECTION-INSERT-END",
            section is PptxEditException && slide == 2,
            $"AddEmptySection at Count threw {section?.GetType().Name ?? "nothing"}: {section?.Message}; InsertEmptySlide at Count put the slide at {slide}");
    }

    [LicensedFact]
    public void TableIndexOutOfRange_NamesDifferentUpperBoundsForColumnsAndRows()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        ITable table = presentation.Slides[0].Shapes.AddTable(10, 10, [50, 50, 50], [20, 20]);
        Exception? column = Record.Exception(() => table[3, 0]);
        Exception? row = Record.Exception(() => table[0, 2]);

        KnownIssue.Reproduces(
            "SLIDES-TABLE-INDEX-MESSAGE",
            column?.Message == "value of index = 3 is out of range 0..3" && row?.Message == "value of index = 2 is out of range 0..1",
            $"a table of 3 columns and 2 rows refuses column 3 with \"{column?.Message}\" and row 2 with \"{row?.Message}\"");
    }

    [LicensedFact]
    public void FooterVisibility_DoesNothingWhenTheLayoutHasNoPlaceholder()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        PlaceholderType[] kinds = [PlaceholderType.Footer, PlaceholderType.SlideNumber, PlaceholderType.DateAndTime];
        ILayoutSlide layout = presentation.LayoutSlides.GetByType(SlideLayoutType.Blank);
        foreach (IShape placeholder in layout.Shapes.Where(shape => shape.Placeholder is { } kind && kinds.Contains(kind.Type)).ToList())
        {
            layout.Shapes.Remove(placeholder);
        }

        ISlide slide = presentation.Slides.AddEmptySlide(layout);
        ISlideHeaderFooterManager footer = slide.HeaderFooterManager;
        footer.SetFooterVisibility(true);
        footer.SetSlideNumberVisibility(true);
        footer.SetDateTimeVisibility(true);
        int onMaster = presentation.Masters[0].Shapes.Count(shape => shape.Placeholder is { } kind && kinds.Contains(kind.Type));

        KnownIssue.Reproduces(
            "SLIDES-FOOTER-LAYOUT-ONLY",
            onMaster == 3 && !footer.IsFooterVisible && !footer.IsSlideNumberVisible && !footer.IsDateTimeVisible && slide.Shapes.Count == 0,
            $"with the placeholders on the master ({onMaster}) and not the layout, setting them visible leaves footer {footer.IsFooterVisible}, "
                + $"slide number {footer.IsSlideNumberVisible}, date {footer.IsDateTimeVisible} and {slide.Shapes.Count} shapes");
    }

    [LicensedFact]
    public void UnsetBulletCharacter_ReadsAsANoncharacter()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IAutoShape shape = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 200, 50);
        shape.TextFrame.Text = "x";
        IParagraphFormat format = shape.TextFrame.Paragraphs[0].ParagraphFormat;
        BulletType type = format.Bullet.Type;
        char unset = format.Bullet.Char;
        char effective = format.GetEffective().Bullet.Char;

        KnownIssue.Reproduces(
            "SLIDES-BULLET-CHAR-UNSET",
            type == BulletType.NotDefined && unset == '￿' && effective == '•',
            $"a paragraph whose bullet is {type} reads Bullet.Char U+{(int)unset:X4}, effective U+{(int)effective:X4}");
    }

    [LicensedFact]
    public void TopLevelShape_HasTheSlidesRootGroupAsParent()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        IShape shape = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 50, 50);
        IGroupShape? parent = shape.ParentGroup;

        KnownIssue.Reproduces(
            "SLIDES-PARENT-GROUP-ROOT",
            !shape.IsGrouped && parent is not null && parent.OfficeInteropShapeId == 1 && !presentation.Slides[0].Shapes.Contains(parent),
            $"a shape with IsGrouped {shape.IsGrouped} has ParentGroup {(parent is null ? "null" : $"of id {parent.OfficeInteropShapeId}")}");
    }

    /// <summary>
    /// Reading one shape's <see cref="IShape.UniqueId"/> makes every later save write a tag part
    /// for each shape, master and layout shapes included.
    /// </summary>
    [LicensedFact]
    public void ReadingAUniqueId_MakesSavesTagEveryShape()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 50, 50);
        IShape second = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Ellipse, 100, 10, 50, 50);
        int unread = UniqueIdTags(Saved(presentation));
        _ = second.UniqueId;
        byte[] saved = Saved(presentation);
        int read = UniqueIdTags(saved);
        using Presentation reopened = Opened(saved);
        int resaved = UniqueIdTags(Saved(reopened));

        KnownIssue.Reproduces(
            "SLIDES-UNIQUEID-TAGS",
            unread == 0 && read > presentation.Slides[0].Shapes.Count && resaved == read,
            $"a slide of {presentation.Slides[0].Shapes.Count} shapes saves {unread} AS_UNIQUEID tag parts, {read} once one UniqueId is read, and {resaved} when saved again");

        static int UniqueIdTags(byte[] package)
        {
            using var archive = new ZipArchive(new MemoryStream(package));
            return archive.Entries.Count(static entry => entry.FullName.StartsWith("ppt/tags/", StringComparison.Ordinal)
                && Read(entry).Contains("\"AS_UNIQUEID\"", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// A file PowerPoint wrote has no tags, yet it loads with the engine's five, and a save writes
    /// them even after one is removed.
    /// </summary>
    [LicensedFact]
    public void EveryPresentation_CarriesTheEnginesEnvironmentTags()
    {
        using var fixture = new SlidesEngineFixture();
        using (ZipArchive package = ZipFile.OpenRead(ImplicitTitleDeck))
        {
            Assert.DoesNotContain(package.Entries, static entry => entry.FullName.StartsWith("ppt/tags/", StringComparison.Ordinal));
        }

        using var presentation = new Presentation(ImplicitTitleDeck);
        string[] loaded = Names(presentation);
        presentation.CustomData.Tags.Remove("AS_OS");
        using Presentation reopened = Opened(Saved(presentation));
        string[] saved = Names(reopened);
        string[] expected = ["AS_NET", "AS_OS", "AS_RELEASE_DATE", "AS_TITLE", "AS_VERSION"];

        KnownIssue.Reproduces(
            "SLIDES-ENVIRONMENT-TAGS",
            loaded.Order(StringComparer.Ordinal).SequenceEqual(expected) && saved.Order(StringComparer.Ordinal).SequenceEqual(expected),
            $"a file without tags loads with [{string.Join(", ", loaded)}] and, with AS_OS removed, saves [{string.Join(", ", saved)}]");

        static string[] Names(IPresentation presentation)
        {
            ITagCollection tags = presentation.CustomData.Tags;
            return [.. Enumerable.Range(0, tags.Count).Select(tags.GetNameByIndex)];
        }
    }

    [LicensedFact]
    public void DuplicateShapeId_IsRenumberedAtLoadWithoutAWarning()
    {
        using var fixture = new SlidesEngineFixture();
        byte[] saved;
        using (var presentation = new Presentation())
        {
            for (int index = 0; index < 3; index++)
            {
                presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10 + (60 * index), 10, 50, 50);
            }

            saved = Saved(presentation);
        }

        string[] written = [];
        byte[] duplicated = Rewritten(saved, "ppt/slides/slide1.xml", bytes =>
        {
            XDocument slide = XDocument.Load(new MemoryStream(bytes));
            XElement[] ids = [.. slide.Descendants().Where(static element => element.Name.LocalName == "cNvPr")];
            ids.Single(static element => (string?)element.Attribute("id") == "3").SetAttributeValue("id", "2");
            written = [.. ids.Select(static element => (string)element.Attribute("id")!)];
            return Encoding.UTF8.GetBytes(slide.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        });
        var warnings = new LoadWarnings();
        using Presentation loaded = Opened(duplicated, warnings);
        string read = string.Join(", ", loaded.Slides[0].Shapes.Select(static shape => shape.OfficeInteropShapeId));

        KnownIssue.Reproduces(
            "SLIDES-DUPLICATE-SHAPE-ID",
            string.Join(", ", written) == "1, 2, 2, 4" && read == "2, 5, 4" && warnings.Received.Count == 0,
            $"shape ids [{string.Join(", ", written)}] load as [{read}] with {warnings.Received.Count} warnings");
    }

    [LicensedFact]
    public void DamagedImagePart_LoadsWithoutAWarning()
    {
        using var fixture = new SlidesEngineFixture();
        byte[] saved;
        using (var presentation = new Presentation())
        {
            IPPImage image = presentation.Images.AddImage(SlidesEngineFixture.Png(48));
            presentation.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, 10, 10, 48, 48, image);
            saved = Saved(presentation);
        }

        string media = MediaPart(saved);
        string truncated = Load(Rewritten(saved, media, static bytes => bytes[..(bytes.Length / 2)]));
        string empty = Load(Rewritten(saved, media, static _ => []));
        string missing = Load(Rewritten(saved, media, static _ => null));

        KnownIssue.Reproduces(
            "SLIDES-IMAGE-PART-WARNING",
            truncated == string.Empty && empty == string.Empty && missing == "DataLoss: Empty image part.",
            $"warnings of an image part cut in half: [{truncated}]; empty: [{empty}]; missing: [{missing}]");

        static string Load(byte[] package)
        {
            var warnings = new LoadWarnings();
            using Presentation loaded = Opened(package, warnings);
            return string.Join("; ", warnings.Received);
        }
    }

    [LicensedFact]
    public void EmptyImagePart_ThrowsWhenTheImageSizeIsRead()
    {
        using var fixture = new SlidesEngineFixture();
        byte[] saved;
        using (var presentation = new Presentation())
        {
            IPPImage image = presentation.Images.AddImage(SlidesEngineFixture.Png(48));
            presentation.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, 10, 10, 48, 48, image);
            saved = Saved(presentation);
        }

        using Presentation loaded = Opened(Rewritten(saved, MediaPart(saved), static _ => []));
        IPPImage empty = loaded.Images[0];
        Exception? width = Record.Exception(() => empty.Width);
        Exception? height = Record.Exception(() => empty.Height);

        KnownIssue.Reproduces(
            "SLIDES-IMAGE-EMPTY-PART",
            empty.BinaryData.Length == 0 && width is EndOfStreamException && height is EndOfStreamException,
            $"an image part of 0 bytes opens as an image of {empty.BinaryData.Length} bytes whose Width threw {width?.GetType().Name ?? "nothing"} and Height {height?.GetType().Name ?? "nothing"}");
    }

    [LicensedFact]
    public void ActiveXControlWithoutClassOrProperties_FailsTheSave()
    {
        using var fixture = new SlidesEngineFixture();
        byte[] saved;
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Controls.AddControl(ControlType.WindowsMediaPlayer, 10, 10, 200, 150);
            saved = Saved(presentation);
        }

        const string Bare = "<ax:ocx xmlns:ax=\"http://schemas.microsoft.com/office/2006/activeX\" ax:classid=\"{00000000-0000-0000-0000-000000000000}\" ax:persistence=\"persistPropertyBag\"/>";
        const string WithProperty = "<ax:ocx xmlns:ax=\"http://schemas.microsoft.com/office/2006/activeX\" ax:classid=\"{00000000-0000-0000-0000-000000000000}\" ax:persistence=\"persistPropertyBag\"><ax:ocxPr ax:name=\"a\" ax:value=\"1\"/></ax:ocx>";
        (int bareControls, Exception? bare) = Resave(Bare);
        (_, Exception? withProperty) = Resave(WithProperty);

        KnownIssue.Reproduces(
            "SLIDES-ACTIVEX-EMPTY-SAVE",
            bareControls == 1 && bare is NullReferenceException && withProperty is null,
            $"a control with an all-zero class id loads as {bareControls} control and saves with {bare?.GetType().Name ?? "no exception"}; with one property it saves with {withProperty?.GetType().Name ?? "no exception"}");

        (int Controls, Exception? Failure) Resave(string control)
        {
            using Presentation loaded = Opened(Rewritten(saved, "ppt/activeX/activeX1.xml", _ => Encoding.UTF8.GetBytes(control)));
            return (loaded.Slides[0].Controls.Count, Record.Exception(() => loaded.Save(new MemoryStream(), SaveFormat.Pptx)));
        }
    }

    /// <summary>
    /// SVG bytes given to <see cref="IImageCollection.AddImage(byte[])"/> become an image of
    /// unknown type that draws nothing, while the engine draws the same SVG as an
    /// <see cref="ISvgImage"/>.
    /// </summary>
    [LicensedFact]
    public void SvgBytesAddedAsAnImage_DrawNothing()
    {
        using var fixture = new SlidesEngineFixture();
        const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><rect width=\"100\" height=\"100\" fill=\"#ff0000\"/></svg>";
        using var presentation = new Presentation();
        presentation.SlideSize.SetSize(100, 100, SlideSizeScaleType.DoNotScale);
        IPPImage image = presentation.Images.AddImage(Encoding.UTF8.GetBytes(Svg));
        presentation.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, 0, 0, 100, 100, image);
        (byte Red, byte Green, byte Blue) bytesDrawn = CenterPixel(presentation.Slides[0]);
        byte[] saved = Saved(presentation);
        string types = Part(saved, "[Content_Types].xml");
        string[] media;
        using (var archive = new ZipArchive(new MemoryStream(saved)))
        {
            media = [.. archive.Entries.Select(static entry => entry.FullName).Where(static name => name.StartsWith("ppt/media/", StringComparison.Ordinal))];
        }

        using var reference = new Presentation();
        reference.SlideSize.SetSize(100, 100, SlideSizeScaleType.DoNotScale);
        reference.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, 0, 0, 100, 100, reference.Images.AddImage(new SvgImage(Svg)));
        (byte Red, byte Green, byte Blue) svgDrawn = CenterPixel(reference.Slides[0]);

        KnownIssue.Reproduces(
            "SLIDES-IMAGE-UNKNOWN-BYTES",
            image.ContentType == "image/unknown" && image.Width == 0 && image.Height == 0 && bytesDrawn == (255, 255, 255) && svgDrawn == (255, 0, 0)
                && media.SequenceEqual(["ppt/media/image1.image"]) && types.Contains("Extension=\"image\" ContentType=\"image/unknown\"", StringComparison.Ordinal),
            $"SVG bytes become an image of type {image.ContentType}, {image.Width}x{image.Height}, drawn as {bytesDrawn} (as an SvgImage: {svgDrawn}) and saved as [{string.Join(", ", media)}]");
    }

    [LicensedFact]
    public void PresentationWithoutSlides_ReopensFromOdpWithOne()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        presentation.Slides.RemoveAt(0);
        using Presentation odp = Opened(Saved(presentation, SaveFormat.Odp));
        using Presentation pptx = Opened(Saved(presentation));

        KnownIssue.Reproduces(
            "SLIDES-ODP-EMPTY-DECK",
            odp.Slides.Count == 1 && pptx.Slides.Count == 0,
            $"a presentation without slides reopens from ODP with {odp.Slides.Count} and from PPTX with {pptx.Slides.Count}");
    }

    /// <summary>
    /// An ODP save writes lengths in centimetres to three decimals, so shape positions reopen
    /// changed; a PPTX save keeps them.
    /// </summary>
    [LicensedFact]
    public void OdpSave_RoundsShapePositions()
    {
        using var fixture = new SlidesEngineFixture();
        float[] positions = [205.5f, 41.5f, 312f, 100.25f];
        using var presentation = new Presentation();
        foreach (float x in positions)
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, x, 10, 50, 20);
        }

        byte[] odp = Saved(presentation, SaveFormat.Odp);
        XNamespace svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
        string[] written = [.. XDocument.Parse(Part(odp, "content.xml")).Descendants()
            .Select(element => (string?)element.Attribute(svg + "x"))
            .OfType<string>()];
        using Presentation fromOdp = Opened(odp);
        using Presentation fromPptx = Opened(Saved(presentation));
        float[] odpX = [.. fromOdp.Slides[0].Shapes.Select(static shape => shape.X)];
        float[] pptxX = [.. fromPptx.Slides[0].Shapes.Select(static shape => shape.X)];

        KnownIssue.Reproduces(
            "SLIDES-ODP-LENGTH-PRECISION",
            written.SequenceEqual(["7.249cm", "1.464cm", "11.006cm", "3.536cm"])
                && odpX.SequenceEqual([205.4962f, 41.50179f, 312.00046f, 100.23929f]) && pptxX.SequenceEqual(positions),
            $"shapes at X [{string.Join(", ", positions)}] are written to ODP as [{string.Join(", ", written)}] and reopen at "
                + $"[{string.Join(", ", odpX.Select(static x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}]; from PPTX at [{string.Join(", ", pptxX)}]");
    }

    /// <summary>
    /// OpenDocument packages start with an uncompressed <c>mimetype</c> entry; the engine's ODP
    /// save writes it compressed and after other entries.
    /// </summary>
    [LicensedFact]
    public void OdpSave_WritesTheMimetypeEntryCompressedAndNotFirst()
    {
        using var fixture = new SlidesEngineFixture();
        using var presentation = new Presentation();
        byte[] odp = Saved(presentation, SaveFormat.Odp);
        Assert.Equal(0x04034B50u, BitConverter.ToUInt32(odp, 0));
        string first = Encoding.ASCII.GetString(odp, 30, BitConverter.ToUInt16(odp, 26));
        using var archive = new ZipArchive(new MemoryStream(odp));
        ZipArchiveEntry mimetype = archive.GetEntry("mimetype")!;

        KnownIssue.Reproduces(
            "SLIDES-ODP-MIMETYPE",
            first != "mimetype" && mimetype.CompressedLength != mimetype.Length,
            $"the package starts with {first}; mimetype is entry {archive.Entries.ToList().IndexOf(mimetype)}, {mimetype.CompressedLength} of {mimetype.Length} bytes stored");
    }

    private static IChart SampleChart(Presentation presentation) =>
        presentation.Slides[0].Shapes.AddChart(ChartType.ClusteredColumn, 10, 10, 400, 300);

    private static byte[] Saved(IPresentation presentation, SaveFormat format = SaveFormat.Pptx)
    {
        using var stream = new MemoryStream();
        presentation.Save(stream, format);
        return stream.ToArray();
    }

    private static Presentation Opened(byte[] package, IWarningCallback? warnings = null) =>
        new(new MemoryStream(package), new Aspose.Slides.LoadOptions { WarningCallback = warnings });

    private static string Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string Part(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        return Read(archive.GetEntry(name)!);
    }

    /// <summary>The one media part of a package, such as the image of its only picture.</summary>
    private static string MediaPart(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        return archive.Entries.Single(static entry => entry.FullName.StartsWith("ppt/media/", StringComparison.Ordinal)).FullName;
    }

    /// <summary>A part of the workbook a chart embeds.</summary>
    private static string WorkbookPart(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        using var workbook = new MemoryStream();
        using (Stream embedded = archive.Entries.Single(static entry => entry.FullName.StartsWith("ppt/embeddings/", StringComparison.Ordinal)).Open())
        {
            embedded.CopyTo(workbook);
        }

        workbook.Position = 0;
        using var inner = new ZipArchive(workbook);
        return Read(inner.GetEntry(name)!);
    }

    /// <summary>The package with part <paramref name="name"/> rewritten, or left out when <paramref name="rewrite"/> returns null.</summary>
    private static byte[] Rewritten(byte[] package, string name, Func<byte[], byte[]?> rewrite)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(package)))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in source.Entries)
            {
                using var content = new MemoryStream();
                using (Stream read = entry.Open())
                {
                    read.CopyTo(content);
                }

                byte[]? bytes = entry.FullName == name ? rewrite(content.ToArray()) : content.ToArray();
                if (bytes is null)
                {
                    continue;
                }

                using Stream write = target.CreateEntry(entry.FullName).Open();
                write.Write(bytes);
            }
        }

        return output.ToArray();
    }

    private static int Occurrences(string text, string value) => text.Split(value).Length - 1;

    /// <summary>The colour of the pixel at the centre of the slide, drawn at its size.</summary>
    private static (byte Red, byte Green, byte Blue) CenterPixel(ISlide slide)
    {
        using IImage drawn = slide.GetImage(1f, 1f);
        using var stream = new MemoryStream();
        drawn.Save(stream, ImageFormat.Bmp);
        byte[] bitmap = stream.ToArray();
        int pixels = BitConverter.ToInt32(bitmap, 10);
        int width = BitConverter.ToInt32(bitmap, 18);
        int height = BitConverter.ToInt32(bitmap, 22);
        int bytesPerPixel = BitConverter.ToInt16(bitmap, 28) / 8;
        int stride = ((width * bytesPerPixel) + 3) / 4 * 4;
        int row = height > 0 ? height - 1 - (height / 2) : -height / 2;
        int at = pixels + (row * stride) + (width / 2 * bytesPerPixel);
        return (bitmap[at + 2], bitmap[at + 1], bitmap[at]);
    }

    /// <summary>Collects the warnings Aspose.Slides reports while it loads a presentation.</summary>
    private sealed class LoadWarnings : IWarningCallback
    {
        public List<string> Received { get; } = [];

        public ReturnAction Warning(IWarningInfo warning)
        {
            Received.Add($"{warning.WarningType}: {warning.Description}");
            return ReturnAction.Continue;
        }
    }

    /// <summary>A Fonts key in a new application hive file, which never touches the user's registry.</summary>
    [SupportedOSPlatform("windows")]
    private static RegistryKey PrivateFontsKey(string hiveFile)
    {
        int status = RegLoadAppKey(hiveFile, out IntPtr hive, 0xF003F, 0, 0);
        Assert.True(status == 0, $"RegLoadAppKey failed with {status}.");
        using RegistryKey root = RegistryKey.FromHandle(new SafeRegistryHandle(hive, ownsHandle: true));
        return root.CreateSubKey(FontsKey);
    }

    private static readonly IntPtr CurrentUser = unchecked((int)0x80000001);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegLoadAppKey(string file, out IntPtr key, int access, int options, int reserved);

    [DllImport("advapi32.dll")]
    private static extern int RegOverridePredefKey(IntPtr predefined, IntPtr replacement);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr key);
}
