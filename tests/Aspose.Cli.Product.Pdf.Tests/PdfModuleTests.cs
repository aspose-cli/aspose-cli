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
}
