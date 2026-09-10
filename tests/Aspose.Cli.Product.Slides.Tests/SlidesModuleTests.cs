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
}
