using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Applies the shared product autonomy contract to PDF.</summary>
public sealed class PdfModuleTests
    : ProductContractTests<PdfModule>
{
    /// <summary>PDF reads no product-owned input document: its edit document is the object table's.</summary>
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } = [];

    [Fact]
    public Task EveryDefaultInputFormatHasPositiveRoutingEvidence() =>
        ProductRoutingContract.AssertPositiveRoutesAsync<PdfModule>(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["pdf"] = ProductRoutingContract.Utf8("%PDF-1.7\n"),
            });
}
