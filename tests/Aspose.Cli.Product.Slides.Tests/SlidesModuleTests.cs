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
            SlidesOps.Names.Order(StringComparer.Ordinal),
            SlidesContractSamples.SlidesOpsBatch.Ops
                .Select(static operation => operation.OpName)
                .Order(StringComparer.Ordinal));

    private static byte[] OpenDocument() => ProductRoutingContract.ZipMarker(
        "application/vnd.oasis.opendocument.presentation");

    [Theory]
    [InlineData("""{"op":"append_presentation","path":"other.pptx"}""", """{"masterPolicy":"keep-source"}""")]
    [InlineData("""{"op":"replace_text","find":"a","replace":"b"}""", """{"scope":"all"}""")]
    [InlineData("""{"op":"set_slide_size","size":"800x600pt"}""", """{"scaleContent":true}""")]
    [InlineData("""{"op":"set_slide_size","size":"800x600pt","scaleContent":false}""", """{"scaleContent":false}""")]
    [InlineData("""{"op":"replace_text","find":"a","replace":"b","scope":null}""", """{"scope":null}""")]
    public void OperationFields_PreserveDefaultsAndExplicitValues(string input, string expected) =>
        AssertOperationDefaults<SlidesOp>(input, expected);

    [Theory]
    [InlineData("""{"op":"set_shape_style","slide":1,"shape":1}""")]
    [InlineData("""{"op":"set_shape_style","slide":1,"shape":1,"style":null}""")]
    public void ShapeStyle_RequiresTheStyleObject(string input)
    {
        Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            SlidesOpsParser.Parse("{\"ops\":[" + input + "]}"));
    }
}
