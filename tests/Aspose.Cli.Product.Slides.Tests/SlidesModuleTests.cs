using SlidesContracts = Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Applies the shared product autonomy contract to Slides.</summary>
public sealed class SlidesModuleTests
    : ProductContractTests<SlidesModule>
{
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } =
        [new(SlidesOp.SchemaUri, CanonicalOps)];

    protected override IReadOnlyDictionary<string, string> Homonyms { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kind"] = "An extracted item's kind names what was extracted, not a shape, chart or transition kind.",
            ["text"] = "A read slide's text lists the text of each of its shapes, while an operation's text is one shape's or paragraph's text.",
        };

    /// <summary>One operation document that uses every registered operation.</summary>
    private static SlidesOpsBatch CanonicalOps { get; } = new()
    {
        Schema = SlidesOp.SchemaUri,
        SchemaVersion = 2,
        Ops =
        [
            new AddSlideOp { Layout = "Title and Content", At = 2 },
            new DeleteSlidesOp { Slides = "4" },
            new MoveSlideOp { Slide = 2, To = 1 },
            new DuplicateSlideOp { SlideId = 256, At = 3 },
            new SetSlideHiddenOp { Slides = "3", Hidden = true },
            new ApplyLayoutOp { Slides = "2", Layout = "Title Only" },
            new SetBackgroundOp { Slides = "1-2", Color = "#F8FAFC" },
            new SlidesContracts.AddSectionOp { Name = "Results", StartSlide = 2 },
            new AppendPresentationOp { Path = "D:/data/append.pptx", MasterPolicy = "keep-source" },
            new SetTitleOp { Slide = 1, Text = "Q3 Review" },
            new SlidesContracts.SetBodyOp
            {
                Slide = 1,
                Paragraphs = [new SlidesParagraphInput { Text = "Revenue grew", Level = 0 }],
            },
            new SlidesContracts.SetTextOp { Slide = 1, ShapeId = 4, Text = "Updated" },
            new SlidesReplaceTextOp { Find = "Q3", Replace = "Q4", Scope = "all" },
            new SetNotesOp { Slide = 1, Text = "Open with the headline." },
            new SlidesInsertImageOp
            {
                Slide = 2,
                Path = "D:/data/chart.png",
                Rect = new SlidesRectInput { X = 72, Y = 120, Width = 300, Height = 180 },
                AltText = "Revenue by quarter",
            },
            new InsertShapeOp
            {
                Slide = 2,
                Kind = "rounded-rectangle",
                Rect = new SlidesRectInput { X = 400, Y = 120, Width = 200, Height = 80 },
                Text = "Key point",
                Style = new SlidesShapeStyleInput { Fill = "#E0F2FE", Color = "#0F172A", Bold = true },
            },
            new SlidesInsertTableOp
            {
                Slide = 2,
                Rect = new SlidesRectInput { X = 72, Y = 320, Width = 540, Height = 120 },
                RowCount = 2,
                ColumnCount = 2,
                Data = [new[] { "Metric", "Value" }, new[] { "ARR", "$2M" }],
            },
            new SlidesSetTableCellOp { Slide = 2, ShapeId = 8, Row = 2, Col = 2, Text = "$2.1M" },
            new InsertChartOp
            {
                Slide = 3,
                Kind = "column",
                Rect = new SlidesRectInput { X = 72, Y = 120, Width = 560, Height = 300 },
                Categories = ["Q1", "Q2"],
                Series = [new SlidesChartSeriesInput { Name = "Revenue", Values = [10, 12] }],
                Title = "Quarterly revenue",
            },
            new UpdateChartDataOp
            {
                Slide = 3,
                ShapeId = 9,
                Categories = ["Q1", "Q2"],
                Series = [new SlidesChartSeriesInput { Name = "Revenue", Values = [11, 14] }],
            },
            new DeleteShapeOp { Slide = 3, ShapeId = 10 },
            new SetShapeBoundsOp { Slide = 2, ShapeId = 7, X = 380, Width = 240 },
            new SetShapeStyleOp
            {
                Slide = 1,
                Placeholder = "title",
                Style = new SlidesShapeStyleInput { Font = "Aptos Display", Size = 30, Color = "#0F172A" },
            },
            new SlidesContracts.SetFooterOp { Slides = "1-3", Text = "Confidential", ShowNumber = true },
            new SetTransitionOp { Slides = "1-3", Kind = "fade", DurationMs = 500 },
            new SlidesSetPropertiesOp { Title = "Q3 Review", Author = "Finance", Company = "Aspose" },
            new SetSlideSizeOp { Size = "16x9", ScaleContent = true },
        ],
    };

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            ParseOps("""{"ops":null}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void OperationObjects_RejectUnknownNestedFields() =>
        AssertOperationObjectIsStrict<SlidesOp>(
            """{"op":"set_shape_style","slide":1,"shapeId":1,"style":{"bold":true}}""", "style");

    [Fact]
    public Task EveryDefaultInputFormatHasPositiveRoutingEvidence() =>
        ProductRoutingContract.AssertPositiveRoutesAsync<SlidesModule>(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["ppt"] = ProductRoutingContract.CompoundFile(),
                ["pptx"] = ProductRoutingContract.ZipMarker("ppt/presentation.xml"),
                ["pptm"] = ProductRoutingContract.ZipMarker("ppt/presentation.xml"),
                ["pps"] = ProductRoutingContract.CompoundFile(),
                ["ppsx"] = ProductRoutingContract.ZipMarker("ppt/presentation.xml"),
                ["ppsm"] = ProductRoutingContract.ZipMarker("ppt/presentation.xml"),
                ["pot"] = ProductRoutingContract.CompoundFile(),
                ["potx"] = ProductRoutingContract.ZipMarker("ppt/presentation.xml"),
                ["potm"] = ProductRoutingContract.ZipMarker("ppt/presentation.xml"),
                ["odp"] = OpenDocument(),
                ["otp"] = OpenDocument(),
                ["fodp"] = ProductRoutingContract.Utf8(
                    "<?xml version=\"1.0\"?><office:presentation/>"),
            });

    [Fact]
    public void CanonicalOps_CoverEveryRegisteredOperation() =>
        Assert.Equal(
            SlidesOp.Catalog.Names.Order(StringComparer.Ordinal),
            CanonicalOps.Ops
                .Select(SlidesOp.Catalog.NameOf)
                .Order(StringComparer.Ordinal));

    private static byte[] OpenDocument() => ProductRoutingContract.ZipMarker(
        "application/vnd.oasis.opendocument.presentation");

    [Theory]
    [InlineData("""{"op":"append_presentation","path":"other.pptx"}""", """{"masterPolicy":"keep-source"}""")]
    [InlineData("""{"op":"replace_text","find":"a","replace":"b"}""", """{"scope":"all"}""")]
    [InlineData("""{"op":"set_slide_size","size":"800x600pt"}""", """{"scaleContent":true}""")]
    [InlineData("""{"op":"set_slide_size","size":"800x600pt","scaleContent":false}""", """{"scaleContent":false}""")]
    public void OperationFields_PreserveDefaultsAndExplicitValues(string input, string expected) =>
        AssertOperationDefaults<SlidesOp>(input, expected);

    [Fact]
    public void ReplaceTextScope_RejectsExplicitNullAsAnInvalidOperation()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            ParseOps("""{"ops":[{"op":"replace_text","find":"a","replace":"b","scope":null}]}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Theory]
    [InlineData("""{"op":"set_shape_style","slide":1,"shapeId":1}""")]
    [InlineData("""{"op":"set_shape_style","slide":1,"shapeId":1,"style":null}""")]
    public void ShapeStyle_RequiresTheStyleObject(string input)
    {
        Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            ParseOps("{\"ops\":[" + input + "]}"));
    }

    [Theory]
    [InlineData("""{"op":"set_shape_style","slide":1,"shapeId":2}""")]
    [InlineData("""{"op":"set_shape_bounds","slide":1,"shapeId":2}""")]
    [InlineData("""{"op":"set_shape_bounds","slide":1,"shapeId":2,"width":0}""")]
    [InlineData("""{"op":"update_chart_data","slide":1,"shapeId":2,"series":[]}""")]
    [InlineData("""{"op":"set_text","slide":1,"placeholder":"object","text":"x"}""")]
    public void OperationRules_RejectIncompleteOrUnknownValues(string operation)
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() => ParseOps($$"""{"ops":[{{operation}}]}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    private static SlidesOpsBatch ParseOps(string json) =>
        SlidesOp.Catalog.Parse<SlidesOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
