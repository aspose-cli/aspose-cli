using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Applies the shared product autonomy contract to Slides.</summary>
public sealed class SlidesModuleTests
    : ProductContractTests<SlidesModule>
{
    /// <summary>Slides reads no product-owned input document: its edit document is the object table's.</summary>
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } = [];

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

    private static byte[] OpenDocument() => ProductRoutingContract.ZipMarker(
        "application/vnd.oasis.opendocument.presentation");
}
