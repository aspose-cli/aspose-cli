using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// What an operation writes, <c>cells inspect</c> reads back under the operation's own field
/// names and in its value forms (C-B02, C-R21): ranges without the sheet they sit on, numbers
/// without the <c>=</c> Excel stores, a chart's orientation from its series.
/// </summary>
public sealed class CellsReadBackTests : IClassFixture<CellsFixture>
{
    private readonly CellsFixture _fixture;

    public CellsReadBackTests(CellsFixture fixture) => _fixture = fixture;

    private CellsWorkbookSummary EditAndInspect(string operations, params string[] details)
    {
        string path = _fixture.CreateSalesWorkbook($"read-back-{Guid.NewGuid():N}.xlsx");
        CellsEditResult edited = CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>(operations, Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"read-back-{Guid.NewGuid():N}.xlsx")),
        });
        return CellsInfo.Run(_fixture.Session, new CellsInspectRequest { Input = edited.Output!.Path, Details = details }).Workbook;
    }

    [Fact]
    public void AChart_ReadsBackItsDataTitleLegendAxesColorsAndLabels()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[
              {"op":"insert_chart","sheet":"Data","kind":"column","dataRange":"A1:C2","at":"E2:K12","title":"Quarters",
               "legend":{"visible":true,"position":"right"},"axisTitles":{"category":"Region","value":"Sales"},
               "seriesColors":["#FF0000","#00B050"],"dataLabels":{"visible":true,"numberFormat":"0.0"}},
              {"op":"insert_chart","sheet":"Second","kind":"line","dataRange":"Data!A1:C2","at":"C2:H12","seriesInRows":true}
            ]}
            """, CellsInspectDetails.Charts);

        CellsChartInfo columns = workbook.Charts!.Single(static chart => chart.Sheet == "Data");
        Assert.Equal(("Quarters", "A1:C2", false), (columns.Title, columns.DataRange, columns.SeriesInRows));
        Assert.Equal(new CellsChartLegend { Visible = true, Position = CellsLegendPositions.Right }, columns.Legend);
        Assert.Equal(new CellsChartAxisTitles { Category = "Region", Value = "Sales" }, columns.AxisTitles);
        Assert.Equal(["#FF0000", "#00B050"], columns.SeriesColors);
        Assert.Equal(new CellsChartDataLabels { Visible = true, NumberFormat = "0.0" }, columns.DataLabels);

        CellsChartInfo rows = workbook.Charts!.Single(static chart => chart.Sheet == "Second");
        Assert.Equal((null, "Data!A1:C2", true), (rows.Title, rows.DataRange, rows.SeriesInRows));
    }

    [Fact]
    public void APivot_ReadsBackItsSourceFieldsValuesAndCaptions()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[{"op":"insert_pivot","sheet":"Second","sourceRange":"Data!A1:C2","at":"C2","name":"ByRegion","rows":["Region"],
              "values":[{"field":"Q1"},{"field":"Q2","function":"max","numberFormat":"0.0","label":"Best Q2"}],"captions":"zh"}]}
            """, CellsInspectDetails.Pivots);

        CellsPivotInfo pivot = Assert.Single(workbook.Pivots!);
        Assert.Equal(("Data!A1:C2", "zh"), (pivot.SourceRange, pivot.Captions));
        Assert.Equal(["Region"], pivot.Rows);
        Assert.Null(pivot.Columns);
        Assert.Equal(
            [("Q1", "sum", null, "求和项:Q1"), ("Q2", "max", "0.0", "Best Q2")],
            pivot.Values!.Select(static value => (value.Field, value.Function, value.NumberFormat, value.Label)));
    }

    [Fact]
    public void AValidation_ReadsBackItsBoundsItemsSourceAndMessages()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[
              {"op":"set_validation","sheet":"Data","range":"B2:C2","kind":"decimal","operator":"between","value1":"0","value2":"9999.5",
               "inputMessage":"Amount","errorMessage":"Too large","allowBlank":false},
              {"op":"set_validation","sheet":"Data","range":"D2","kind":"list","listItems":["Open","Closed"]},
              {"op":"set_validation","sheet":"Data","range":"E2","kind":"list","listSource":"Second!A1:A3"},
              {"op":"set_validation","sheet":"Data","range":"F2","kind":"custom","value1":"=F2>B2"}
            ]}
            """, CellsInspectDetails.Validation);

        CellsValidationInfo bounds = workbook.Validations!.Single(static item => item.Range == "B2:C2");
        Assert.Equal(("between", "0", "9999.5", "Amount", "Too large", false),
            (bounds.Operator, bounds.Value1, bounds.Value2, bounds.InputMessage, bounds.ErrorMessage, bounds.AllowBlank));
        Assert.Equal(["Open", "Closed"], workbook.Validations!.Single(static item => item.Range == "D2").ListItems);
        Assert.Equal("Second!A1:A3", workbook.Validations!.Single(static item => item.Range == "E2").ListSource);
        CellsValidationInfo custom = workbook.Validations!.Single(static item => item.Range == "F2");
        Assert.Equal(("=F2>B2", null), (custom.Value1, custom.Operator));
    }

    [Fact]
    public void ConditionalFormatsAndSparklines_ReadBackTheirRulesStylesAndRanges()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[
              {"op":"add_conditional_format","sheet":"Data","range":"B2:C2","rule":{"kind":"cell-value","operator":"greater-than","value1":"1000"},
               "style":{"bold":true,"color":"#C00000","fillColor":"#FFFF00"}},
              {"op":"add_conditional_format","sheet":"Data","range":"B2:C2","rule":{"kind":"color-scale","minColor":"#FFFFFF","maxColor":"#00B050"}},
              {"op":"add_sparkline","sheet":"Data","dataRange":"B2:C2","location":"D2","kind":"winloss","color":"#7030A0"},
              {"op":"add_sparkline","sheet":"Second","dataRange":"Data!B2:C3","location":"B2:B3"}
            ]}
            """, CellsInspectDetails.ConditionalFormats, CellsInspectDetails.Sparklines);

        CellsConditionalFormatInfo value = workbook.ConditionalFormats!.Single(static item => item.Rule.Kind == "cell-value");
        Assert.Equal(("B2:C2", "greater-than", "1000"), (value.Range, value.Rule.Operator, value.Rule.Value1));
        Assert.Equal(new CellsConditionalStyle { Bold = true, Color = "#C00000", FillColor = "#FFFF00" }, value.Style);
        CellsConditionalFormatInfo scale = workbook.ConditionalFormats!.Single(static item => item.Rule.Kind == "color-scale");
        Assert.Equal(("#FFFFFF", null, "#00B050"), (scale.Rule.MinColor, scale.Rule.MidColor, scale.Rule.MaxColor));
        Assert.Null(scale.Style);

        CellsSparklineInfo own = workbook.Sparklines!.Single(static group => group.Sheet == "Data");
        Assert.Equal(("winloss", "#7030A0", "B2:C2", "D2"), (own.Kind, own.Color, own.DataRange, own.Location));
        CellsSparklineInfo other = workbook.Sparklines!.Single(static group => group.Sheet == "Second");
        Assert.Equal(("line", "Data!B2:C3", "B2:B3"), (other.Kind, other.DataRange, other.Location));
    }

    [Fact]
    public void CommentsHyperlinksImagesAndTables_ReadBackWhatTheirOperationsWrote()
    {
        string image = _fixture.Temp.File($"pixel-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(image, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg=="));
        CellsWorkbookSummary workbook = EditAndInspect($$"""
            {"ops":[
              {"op":"add_comment","sheet":"Data","cell":"B2","text":"Checked","author":"Ann"},
              {"op":"set_hyperlink","sheet":"Data","cell":"A2","url":"https://example.com/east","display":"East"},
              {"op":"set_hyperlink","sheet":"Data","cell":"A3","target":"Second!A1"},
              {"op":"set_hyperlink","sheet":"Data","cell":"A12","target":"Second!A1","display":"查看明细 →"},
              {"op":"insert_image","sheet":"Data","path":{{System.Text.Json.JsonSerializer.Serialize(image)}},"at":"E5","width":120,"height":90},
              {"op":"define_table","sheet":"Second","range":"A1:A1","name":"Markers","tableStyle":"TableStyleLight9"}
            ]}
            """, CellsInspectDetails.Comments, CellsInspectDetails.Hyperlinks, CellsInspectDetails.Images, CellsInspectDetails.Tables);

        CellsCommentInfo comment = Assert.Single(workbook.Comments!);
        Assert.Equal(("Data", "B2", "Checked", "Ann"), (comment.Sheet, comment.Cell, comment.Text, comment.Author));
        Assert.Equal(
            [("A2", "https://example.com/east", null, "East"), ("A3", null, "Second!A1", "Second!A1"), ("A12", null, "Second!A1", "查看明细 →")],
            workbook.Hyperlinks!.Select(static link => (link.Range, link.Url, link.Target, link.Display)));
        CellsImageInfo picture = Assert.Single(workbook.Images!);
        Assert.Equal(("E5", 120d, 90d), (picture.At, picture.Width, picture.Height));
        CellsTableInfo table = Assert.Single(workbook.Tables!);
        Assert.Equal(("TableStyleLight9", false), (table.TableStyle, table.TotalsRow));
    }

    [Fact]
    public void TheSheetLayout_ReadsBackViewTabSizesProtectionAndDefaultFont()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[
              {"op":"set_sheet_view","sheet":"Data","gridlines":false,"zoom":125,"headings":false},
              {"op":"set_tab_color","sheet":"Data","color":"#FF0000"},
              {"op":"resize_columns","sheet":"Data","first":"B","last":"C","widthChars":18},
              {"op":"resize_rows","sheet":"Data","first":2,"last":3,"height":24},
              {"op":"protect_sheet","sheet":"Data","allow":["format-cells","autofilter"]},
              {"op":"set_default_font","font":"Georgia","fontSize":12}
            ]}
            """, CellsInspectDetails.Layout);

        CellsSheetLayoutInfo layout = workbook.Layouts!.Single(static item => item.Sheet == "Data");
        Assert.Equal(new CellsSheetViewInfo { Gridlines = false, Zoom = 125, Headings = false }, layout.SheetView);
        Assert.Equal("#FF0000", layout.TabColor?.Color);
        Assert.Equal([("B", "C", 18d)], layout.ColumnWidths!.Select(static run => (run.First, run.Last, run.WidthChars)));
        Assert.Equal([(2, 3, 24d)], layout.RowHeights!.Select(static run => (run.First, run.Last, run.Height)));
        Assert.Equal(["format-cells", "autofilter"], layout.Protection!.Allow);
        Assert.Equal(("Georgia", 12d), (workbook.DefaultFont.Font, workbook.DefaultFont.FontSize));
        Assert.Null(workbook.Layouts!.Single(static item => item.Sheet == "Second").TabColor);
    }

    [Fact]
    public void ThePageSetup_ReadsBackTheLeftAndRightHeaderAndFooterSections()
    {
        string path = _fixture.CreateSalesWorkbook($"sections-{Guid.NewGuid():N}.xlsx");
        using (var book = new Workbook(path))
        {
            PageSetup page = book.Worksheets["Data"].PageSetup;
            page.SetHeader(0, "Left head");
            page.SetHeader(2, "Right head");
            page.SetFooter(0, "Left foot");
            page.SetFooter(2, "&P");
            book.Save(path, SaveFormat.Xlsx);
        }

        CellsPageSetupInfo setup = CellsInfo.Run(_fixture.Session, new CellsInspectRequest { Input = path, Details = [CellsInspectDetails.Layout] })
            .Workbook.Layouts!.Single(static item => item.Sheet == "Data").PageSetup;

        Assert.Equal(("Left head", null, "Right head"), (setup.HeaderLeft, setup.Header, setup.HeaderRight));
        Assert.Equal(("Left foot", null, "&P"), (setup.FooterLeft, setup.Footer, setup.FooterRight));
    }
}
