using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
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
               "matchStyle":{"bold":true,"color":"#C00000","fillColor":"#FFFF00"}},
              {"op":"add_conditional_format","sheet":"Data","range":"B2:C2","rule":{"kind":"color-scale","minColor":"#FFFFFF","maxColor":"#00B050"}},
              {"op":"add_sparkline","sheet":"Data","dataRange":"B2:C2","at":"D2","kind":"winloss","color":"#7030A0"},
              {"op":"add_sparkline","sheet":"Second","dataRange":"Data!B2:C3","at":"B2:B3"}
            ]}
            """, CellsInspectDetails.ConditionalFormats, CellsInspectDetails.Sparklines);

        CellsConditionalFormatInfo value = workbook.ConditionalFormats!.Single(static item => item.Rule.Kind == "cell-value");
        Assert.Equal(("B2:C2", "greater-than", "1000"), (value.Range, value.Rule.Operator, value.Rule.Value1));
        Assert.Equal(new CellsConditionalStyle { Bold = true, Color = "#C00000", FillColor = "#FFFF00" }, value.MatchStyle);
        CellsConditionalFormatInfo scale = workbook.ConditionalFormats!.Single(static item => item.Rule.Kind == "color-scale");
        Assert.Equal(("#FFFFFF", null, "#00B050"), (scale.Rule.MinColor, scale.Rule.MidColor, scale.Rule.MaxColor));
        Assert.Null(scale.MatchStyle);

        CellsSparklineInfo own = workbook.Sparklines!.Single(static group => group.Sheet == "Data");
        Assert.Equal(("winloss", "#7030A0", "B2:C2", "D2"), (own.Kind, own.Color, own.DataRange, own.At));
        CellsSparklineInfo other = workbook.Sparklines!.Single(static group => group.Sheet == "Second");
        Assert.Equal(("line", "Data!B2:C3", "B2:B3"), (other.Kind, other.DataRange, other.At));
    }

    private CellsEditResult Edit(string path, string operations) =>
        CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>(operations, Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"read-back-{Guid.NewGuid():N}.xlsx")),
        });

    private CellsWorkbookSummary Inspect(string path, params string[] details) =>
        CellsInfo.Run(_fixture.Session, new CellsInspectRequest { Input = path, Details = details }).Workbook;

    /// <summary>A read record as the operation of the same fields: its JSON with the op's name added and nulls left out.</summary>
    private static string AsOperation(string op, object read)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        System.Text.Json.Nodes.JsonObject fields = System.Text.Json.JsonSerializer.SerializeToNode(read, read.GetType(), options)!.AsObject();
        fields.Insert(0, "op", op);
        return $$"""{"ops":[{{fields.ToJsonString()}}]}""";
    }

    [Theory]
    [InlineData("""{"op":"delete_rows","sheet":"Data","at":10}""", "C2:C9,E5:F5")]
    [InlineData("""{"op":"delete_rows","sheet":"Data","at":9,"count":3}""", "C2:C8,E5:F5")]
    [InlineData("""{"op":"delete_rows","sheet":"Data","at":5}""", "C2:C9")]
    [InlineData("""{"op":"delete_columns","sheet":"Data","at":"F"}""", "C2:C10,E5")]
    [InlineData("""{"op":"delete_columns","sheet":"Data","at":"E","count":2}""", "C2:C10")]
    public void DeletingTheEndOfAValidation_ShrinksItAsExcelDoes(string delete, string range)
    {
        CellsWorkbookSummary workbook = EditAndInspect($$"""
            {"ops":[
              {"op":"set_validation","sheet":"Data","range":"C2:C10,E5:F5","kind":"whole-number","operator":"between","value1":"0","value2":"10"},
              {{delete}}
            ]}
            """, CellsInspectDetails.Validation);

        Assert.Equal(range, Assert.Single(workbook.Validations!).Range);
    }

    [Fact]
    public void AValidationOverSeveralAreas_ReadsBackAsOneAndWritesBackAsTheSame()
    {
        // Validating the middle of a validated block splits the block's validation in two areas.
        string split = Edit(_fixture.CreateSalesWorkbook($"read-back-{Guid.NewGuid():N}.xlsx"), """
            {"ops":[
              {"op":"set_validation","sheet":"Data","range":"B5:D7","kind":"list","listItems":["a","b"],"errorMessage":"Pick one"},
              {"op":"set_validation","sheet":"Data","range":"C5:C7","kind":"whole-number","operator":"greater-than","value1":"0"}
            ]}
            """).Output!.Path;
        CellsValidationInfo read = Assert.Single(Inspect(split, CellsInspectDetails.Validation).Validations!, static item => item.Kind == "list");
        Assert.Equal("B5:B7,D5:D7", read.Range);

        string written = Edit(_fixture.CreateSalesWorkbook($"read-back-{Guid.NewGuid():N}.xlsx"), AsOperation("set_validation", read)).Output!.Path;
        CellsEditResult again = Edit(written, AsOperation("set_validation", read));

        Assert.Equivalent(read, Assert.Single(Inspect(written, CellsInspectDetails.Validation).Validations!), strict: true);
        Assert.Equal(0, Assert.Single(again.Applied).ItemsAffected);
    }

    [Fact]
    public void AConditionalFormatOverSeveralAreas_WritesBackAsTheSame()
    {
        // Clearing the middle of a formatted block leaves the rule over the two sides.
        string split = Edit(_fixture.CreateSalesWorkbook($"read-back-{Guid.NewGuid():N}.xlsx"), """
            {"ops":[
              {"op":"add_conditional_format","sheet":"Data","range":"B5:D7","rule":{"kind":"cell-value","operator":"greater-than","value1":"1"},"matchStyle":{"bold":true}},
              {"op":"clear_conditional_formats","sheet":"Data","range":"C5:C7"}
            ]}
            """).Output!.Path;
        CellsConditionalFormatInfo read = Assert.Single(Inspect(split, CellsInspectDetails.ConditionalFormats).ConditionalFormats!);
        Assert.Contains(",", read.Range, StringComparison.Ordinal);

        string written = Edit(_fixture.CreateSalesWorkbook($"read-back-{Guid.NewGuid():N}.xlsx"), AsOperation("add_conditional_format", read)).Output!.Path;

        Assert.Equivalent(read, Assert.Single(Inspect(written, CellsInspectDetails.ConditionalFormats).ConditionalFormats!), strict: true);
    }

    [Fact]
    public void APrintAreaOfSeveralAreas_WritesBackAsTheSame()
    {
        string path = _fixture.Temp.File($"print-areas-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Data";
            workbook.Worksheets[0].PageSetup.PrintArea = "A1:B5,D1:E5";
            workbook.Save(path, SaveFormat.Xlsx);
        }

        CellsPrintAreaInfo read = Assert.Single(Inspect(path, CellsInspectDetails.Layout).Layouts!).PrintArea!;
        Assert.Equal("A1:B5,D1:E5", read.Range);

        string written = Edit(_fixture.CreateSalesWorkbook($"read-back-{Guid.NewGuid():N}.xlsx"),
            $$"""{"ops":[{"op":"set_print_area","sheet":"Data","range":"{{read.Range}}"}]}""").Output!.Path;

        Assert.Equivalent(read, Inspect(written, CellsInspectDetails.Layout).Layouts!.Single(static layout => layout.Sheet == "Data").PrintArea, strict: true);
    }

    [Theory]
    [InlineData("Data", "B5:C5,B7:C7,B8:C8")]
    [InlineData("Second", "Data!B5:C5,Data!B7:C7,Data!B8:C8")]
    public void ASparklineGroupReadBackAsLists_WritesBackAsTheSameGroup(string sheet, string dataRange)
    {
        // A line inserted inside a group splits its cells: the read gives them as lists.
        CellsSparklineInfo split = EditAndInspect($$"""
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"B5","values":[[1,2],[3,4],[5,6]]},
              {"op":"add_sparkline","sheet":"{{sheet}}","dataRange":"{{(sheet == "Data" ? "B5:C7" : "Data!B5:C7")}}","at":"D5:D7","kind":"column","color":"#7030A0"},
              {"op":"insert_rows","sheet":"{{sheet}}","at":6}{{(sheet == "Data" ? "" : ",{\"op\":\"insert_rows\",\"sheet\":\"Data\",\"at\":6}")}}
            ]}
            """, CellsInspectDetails.Sparklines).Sparklines!.Single();
        Assert.Equal((dataRange, "D5,D7,D8"), (split.DataRange, split.At));

        CellsSparklineInfo written = EditAndInspect($$"""
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"B5","values":[[1,2],[null,null],[3,4],[5,6]]},
              {"op":"add_sparkline","sheet":"{{split.Sheet}}","dataRange":"{{split.DataRange}}","at":"{{split.At}}","kind":"{{split.Kind}}","color":"{{split.Color}}"}
            ]}
            """, CellsInspectDetails.Sparklines).Sparklines!.Single();

        Assert.Equal(split, written);
    }

    [Theory]
    [InlineData("B5:C5,B7:C7", "D5,D7,D8", "'dataRange' lists 2 ranges but 'at' has 3 cells")]
    [InlineData("B5:C5,B7:C7,B8:C8", "D5:D6", "'dataRange' lists 3 ranges but 'at' has 2 cells")]
    [InlineData("B5:C6,B7:C7", "D5,D7", "'B5:C6' is neither")]
    [InlineData("B5:D7", "F5,F7", "'at' has 2 cells but 'dataRange' has 3 rows and 3 columns")]
    public void ASparklineListThatDoesNotPairUp_IsRefused(string dataRange, string at, string reason)
    {
        CliException refused = Assert.Throws<CliException>(() => EditAndInspect($$"""
            {"ops":[{"op":"add_sparkline","sheet":"Data","dataRange":"{{dataRange}}","at":"{{at}}"}]}
            """, CellsInspectDetails.Sparklines));

        Assert.Equal("OPS_INVALID", refused.Code.Name);
        Assert.Contains(reason, refused.Message, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("\"width\":120", 120d, 60d)]
    [InlineData("\"height\":45", 90d, 45d)]
    public void AnImageGivenOneSide_KeepsItsProportions(string side, double width, double height)
    {
        // A 40 x 20 pixel image, 30 x 15 points.
        string image = _fixture.Temp.File($"wide-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(image, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAACgAAAAUCAIAAABwJOjsAAAAJElEQVR4nO3NMQ0AAAACIPuX1hg+bPykycVnFYvFYrFYLO4jHn36HQ4oCLQGAAAAAElFTkSuQmCC"));

        CellsWorkbookSummary workbook = EditAndInspect($$"""
            {"ops":[{"op":"insert_image","sheet":"Data","path":{{System.Text.Json.JsonSerializer.Serialize(image)}},"at":"E5",{{side}}}]}
            """, CellsInspectDetails.Images);

        CellsImageInfo picture = Assert.Single(workbook.Images!);
        Assert.Equal((width, height), (picture.Width, picture.Height));
    }

    [Fact]
    public void ProtectingAProtectedSheetAgain_AllowsExactlyTheListedActions()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[
              {"op":"protect_sheet","sheet":"Data","allow":["format-cells","sort"]},
              {"op":"protect_sheet","sheet":"Data","allow":["insert-rows","sort"]}
            ]}
            """, CellsInspectDetails.Layout);

        Assert.Equal(["insert-rows", "sort"], workbook.Layouts!.Single(static item => item.Sheet == "Data").Protection!.Allow);
    }

    [Fact]
    public void TheSheetLayout_ReadsBackViewTabSizesProtectionAndDefaultFont()
    {
        CellsWorkbookSummary workbook = EditAndInspect("""
            {"ops":[
              {"op":"set_sheet_view","sheet":"Data","gridlines":false,"zoomPercent":125,"headings":false},
              {"op":"set_tab_color","sheet":"Data","color":"#FF0000"},
              {"op":"resize_columns","sheet":"Data","first":"B","last":"C","widthChars":18},
              {"op":"resize_rows","sheet":"Data","first":2,"last":3,"height":24},
              {"op":"protect_sheet","sheet":"Data","allow":["format-cells","autofilter"]},
              {"op":"set_default_font","font":"Georgia","fontSize":12}
            ]}
            """, CellsInspectDetails.Layout);

        CellsSheetLayoutInfo layout = workbook.Layouts!.Single(static item => item.Sheet == "Data");
        Assert.Equal(new CellsSheetViewInfo { Gridlines = false, ZoomPercent = 125, Headings = false }, layout.SheetView);
        Assert.Equal("#FF0000", layout.TabColor?.Color);
        Assert.Equal([("B", "C", 18d)], layout.ColumnWidths!.Select(static run => (run.First, run.Last, run.WidthChars)));
        Assert.Equal([(2, 3, 24d)], layout.RowHeights!.Select(static run => (run.First, run.Last, run.Height)));
        Assert.Equal(["format-cells", "autofilter"], layout.Protection!.Allow);
        Assert.Equal(("Georgia", 12d), (workbook.DefaultFont.Font, workbook.DefaultFont.FontSize));
        Assert.Null(workbook.Layouts!.Single(static item => item.Sheet == "Second").TabColor);
    }

    [Fact]
    public void BordersFromFormatRangeAndSetBorders_ReadBackPerSideInTheCellStyle()
    {
        string path = _fixture.CreateSalesWorkbook($"borders-{Guid.NewGuid():N}.xlsx");
        CellsEditResult edited = CellsEdit.Run(_fixture.Session, new CellsEditRequest
        {
            Input = path,
            Batch = CellsOp.Catalog.Parse<CellsOpsBatch>("""
                {"ops":[
                  {"op":"format_range","sheet":"Data","range":"E5","style":{"borders":[{"side":"top","lineStyle":"thin","color":"#000000"},{"side":"left","lineStyle":"double","color":"#0070C0"}]}},
                  {"op":"set_borders","sheet":"Data","range":"E7:F8","edges":["outline"],"lineStyle":"medium","color":"#FF0000"}
                ]}
                """, Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(_fixture.Temp.File($"borders-{Guid.NewGuid():N}.xlsx")),
        });

        CellsReadResult read = CellsRead.Run(_fixture.Session, new CellsReadRequest
        {
            Input = edited.Output!.Path,
            SheetName = "Data",
            Range = A1.ParseRange("E5:F8").Range,
            Content = CellsReadContents.Styles,
        });

        IReadOnlyList<CellsBorderData>? Borders(int row, int column) =>
            read.Sheet.Cells![row][column].StyleId is { } id ? read.Styles![id].Borders : null;
        Assert.Equal(
            [("top", "thin", "#000000"), ("left", "double", "#0070C0")],
            Borders(0, 0)!.Select(static border => (border.Side, border.LineStyle, border.Color)));
        Assert.Equal(
            [("top", "medium", "#FF0000"), ("left", "medium", "#FF0000")],
            Borders(2, 0)!.Select(static border => (border.Side, border.LineStyle, border.Color)));
        Assert.Equal(["bottom", "right"], Borders(3, 1)!.Select(static border => border.Side));
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

        Assert.Equal(("Left head", null, "Right head"), (setup.HeaderLeft, setup.HeaderCenter, setup.HeaderRight));
        Assert.Equal(("Left foot", null, "&P"), (setup.FooterLeft, setup.FooterCenter, setup.FooterRight));
    }
}
