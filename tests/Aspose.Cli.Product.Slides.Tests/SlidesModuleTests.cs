using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Applies the shared product autonomy contract to Slides.</summary>
public sealed class SlidesModuleTests
    : ProductContractTests<SlidesModule>
{
    protected override IReadOnlyList<Aspose.Cli.Sdk.Contracts.ResultEnvelope>
        CanonicalResults => SlidesContractSamples.Results;

    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs =>
        SlidesContractSamples.Inputs;

    protected override IReadOnlyDictionary<string, string> Homonyms { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kind"] = "An extracted item's kind names what was extracted, not a shape, chart or transition kind.",
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
            SlidesContractSamples.SlidesOpsBatch.Ops
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
