using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Applies the shared product autonomy contract to PDF.</summary>
public sealed class PdfModuleTests
    : ProductContractTests<PdfModule>
{
    protected override IReadOnlyList<Aspose.Cli.Sdk.Contracts.ResultEnvelope>
        CanonicalResults => PdfContractSamples.Results;

    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs =>
        PdfContractSamples.Inputs;

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            PdfOpsParser.Parse("""{"ops":null}"""));
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
            PdfOps.Names.Order(StringComparer.Ordinal),
            PdfContractSamples.PdfOpsBatch.Ops
                .Select(static operation => operation.OpName)
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
    [InlineData("""{"op":"flatten_forms"}""", """{"all":true}""")]
    [InlineData("""{"op":"set_page_labels","ranges":[{"startPage":1}]}""", """{"ranges":[{"startPage":1,"style":"arabic","startingValue":1}]}""")]
    [InlineData("""{"op":"encrypt","ownerPasswordEnv":"OWNER"}""", """{"permissions":{"print":false,"copy":false,"modify":false,"annotate":false,"fillForms":false,"extractAccessibility":false,"assemble":false,"printHighResolution":false}}""")]
    [InlineData("""{"op":"optimize"}""", """{"removeUnusedObjects":true,"compressStreams":true}""")]
    [InlineData("""{"op":"optimize","removeUnusedObjects":false,"compressStreams":false}""", """{"removeUnusedObjects":false,"compressStreams":false}""")]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT","size":0,"rotation":0,"opacity":0}""", """{"size":0,"rotation":0,"opacity":0,"color":"#808080"}""")]
    [InlineData("""{"op":"encrypt","ownerPasswordEnv":"OWNER","permissions":{"copy":true,"print":false}}""", """{"permissions":{"print":false,"copy":true,"modify":false,"annotate":false,"fillForms":false,"extractAccessibility":false,"assemble":false,"printHighResolution":false}}""")]
    public void OperationFields_PreserveDefaultsAndExplicitValues(string input, string expected) =>
        AssertOperationDefaults<PdfOp>(input, expected);

    [Theory]
    [InlineData("""{"op":"add_page_numbers","format":null}""")]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT","color":null}""")]
    [InlineData("""{"op":"add_bookmark","title":null,"page":1}""")]
    public void NonNullableFields_RejectExplicitNullAsInvalidOperations(string operation)
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            PdfOpsParser.Parse($$"""{"ops":[{{operation}}]}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void EncryptionPermissions_RejectExplicitNull()
    {
        Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            PdfOpsParser.Parse(
                """{"ops":[{"op":"encrypt","ownerPasswordEnv":"OWNER","permissions":null}]}"""));
    }
}
