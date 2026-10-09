using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Applies the shared product autonomy contract to PDF.</summary>
public sealed class PdfModuleTests
    : ProductContractTests<PdfModule>
{
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs => [new(PdfOp.Catalog.SchemaId, CanonicalOps)];

    protected override IReadOnlyDictionary<string, string> Homonyms { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rotation"] = "A page's clockwise rotation, while add_watermark_text.rotation is the watermark's counterclockwise angle.",
        };

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            Parse("""{"ops":null}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void OperationObjects_RejectUnknownNestedFields() =>
        AssertOperationObjectIsStrict<PdfOp>(
            """{"op":"crop_pages","pages":"1","rect":{"x":0,"y":0,"width":10,"height":10}}""", "rect");

    [Fact]
    public Task EveryDefaultInputFormatHasPositiveRoutingEvidence() =>
        ProductRoutingContract.AssertPositiveRoutesAsync<PdfModule>(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["pdf"] = ProductRoutingContract.Utf8("%PDF-1.7\n"),
            });

    [Fact]
    public void CanonicalOps_CoverEveryRegisteredOperation() =>
        Assert.Equal(
            PdfOp.Catalog.Names.Order(StringComparer.Ordinal),
            CanonicalOps.Ops
                .Select(PdfOp.Catalog.NameOf)
                .Order(StringComparer.Ordinal));

    [Theory]
    [InlineData("""{"op":"insert_blank_page","at":1}""", """{"size":"A4"}""")]
    [InlineData("""{"op":"crop_pages","pages":"1","rect":{"x":0,"y":0,"width":10,"height":10}}""", """{"box":"crop"}""")]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT"}""", """{"size":48,"color":"#808080","opacity":0.35,"rotation":45,"layer":"over"}""")]
    [InlineData("""{"op":"add_watermark_image","path":"image.png"}""", """{"opacity":0.35,"scale":0.5,"layer":"over"}""")]
    [InlineData("""{"op":"add_page_numbers"}""", """{"format":"Page {n} of {N}","position":"bottom-center","start":1}""")]
    [InlineData("""{"op":"add_header_text","text":"Header"}""", """{"position":"top-center"}""")]
    [InlineData("""{"op":"add_footer_text","text":"Footer"}""", """{"position":"bottom-center"}""")]
    [InlineData("""{"op":"redact_text","pattern":"hidden"}""", """{"fillColor":"#000000"}""")]
    [InlineData("""{"op":"redact_area","page":1,"rect":{"x":0,"y":0,"width":10,"height":10}}""", """{"fillColor":"#000000"}""")]
    [InlineData("""{"op":"remove_metadata"}""", """{"xmp":true,"documentInfo":true}""")]
    [InlineData("""{"op":"set_page_labels","ranges":[{"startPage":1}]}""", """{"ranges":[{"startPage":1,"style":"arabic","startingValue":1}]}""")]
    [InlineData("""{"op":"encrypt","ownerPasswordEnv":"OWNER"}""", """{"permissions":{"print":false,"copy":false,"modify":false,"annotate":false,"fillForms":false,"extractAccessibility":false,"assemble":false,"printHighResolution":false}}""")]
    [InlineData("""{"op":"optimize"}""", """{"removeUnusedObjects":true,"compressStreams":true}""")]
    [InlineData("""{"op":"optimize","removeUnusedObjects":false,"compressStreams":false}""", """{"removeUnusedObjects":false,"compressStreams":false}""")]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT","size":0,"rotation":0,"opacity":0}""", """{"size":0,"rotation":0,"opacity":0,"color":"#808080"}""")]
    [InlineData("""{"op":"encrypt","ownerPasswordEnv":"OWNER","permissions":{"copy":true,"print":false}}""", """{"permissions":{"print":false,"copy":true,"modify":false,"annotate":false,"fillForms":false,"extractAccessibility":false,"assemble":false,"printHighResolution":false}}""")]
    public void OperationFields_PreserveDefaultsAndExplicitValues(string input, string expected) =>
        AssertOperationDefaults<PdfOp>(input, expected);

    [Theory]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT","pages":null}""")]
    [InlineData("""{"op":"add_page_numbers","format":null}""")]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT","color":null}""")]
    [InlineData("""{"op":"add_bookmark","title":null,"page":1}""")]
    public void Fields_RejectExplicitNullAsInvalidOperations(string operation)
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            Parse($$"""{"ops":[{{operation}}]}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void EncryptionPermissions_RejectExplicitNull()
    {
        Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            Parse(
                """{"ops":[{"op":"encrypt","ownerPasswordEnv":"OWNER","permissions":null}]}"""));
    }

    private static PdfOpsBatch CanonicalOps { get; } = new()
    {
        Schema = PdfOp.Catalog.SchemaId,
        SchemaVersion = 2,
        Ops =
        [
            new RotatePagesOp { Pages = "1", Angle = 90 },
            new DeletePagesOp { Pages = "3" },
            new MovePagesOp { Pages = "2", To = 1 },
            new InsertPagesFromOp { Path = "D:/data/append.pdf", Pages = "1", At = 2, PasswordEnv = "PDF_PASSWORD" },
            new InsertBlankPageOp { At = 2, Size = "A4" },
            new CropPagesOp { Pages = "1", Box = "crop", Rect = new PdfRectInput { X = 10, Y = 20, Width = 500, Height = 700 } },
            new SetPageSizeOp { Pages = "1", Size = "Letter", ScaleContent = true },
            new AddWatermarkTextOp { Pages = "1-", Text = "DRAFT" },
            new AddWatermarkImageOp { Path = "D:/data/logo.png" },
            new AddPageNumbersOp(),
            new AddHeaderTextOp { Text = "Quarterly report" },
            new AddFooterTextOp { Text = "Confidential" },
            new AddStampImageOp { Page = 1, Path = "D:/data/sign.png", Rect = new PdfRectInput { X = 20, Y = 30, Width = 120, Height = 60 } },
            new AddLinkOp { Page = 1, Rect = new PdfRectInput { X = 20, Y = 100, Width = 160, Height = 20 }, Url = "https://example.com" },
            new RedactTextOp { Pattern = "secret", Regex = false },
            new RedactAreaOp { Page = 1, Rect = new PdfRectInput { X = 20, Y = 140, Width = 160, Height = 20 } },
            new SetMetadataOp { Title = "Quarterly report", Custom = new SortedDictionary<string, string> { ["Department"] = "Finance" } },
            new RemoveMetadataOp(),
            new AddBookmarkOp { Title = "Overview", Page = 1 },
            new DeleteBookmarksOp { All = true },
            new Aspose.Cli.Product.Pdf.Contracts.AddAttachmentOp { Path = "D:/data/source.csv", Name = "source.csv" },
            new Aspose.Cli.Product.Pdf.Contracts.RemoveAttachmentOp { Name = "old.csv" },
            new SetPageLabelsOp { Ranges = [new PdfPageLabelRange { StartPage = 1, Style = "roman-lower" }] },
            new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
            new FlattenFormsOp(),
            new EncryptPdfOp { OwnerPasswordEnv = "PDF_OWNER_PASSWORD" },
            new DecryptPdfOp(),
            new OptimizePdfOp { DownsampleImagesDpi = 150, ImageQuality = 75 },
        ],
    };

    private static PdfOpsBatch Parse(string json) =>
        PdfOp.Catalog.Parse<PdfOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
