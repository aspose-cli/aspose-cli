using Aspose.Cli.TestKit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Applies the shared product autonomy contract to PDF.</summary>
public sealed class PdfModuleTests
    : ProductContractTests<PdfModule>
{
    /// <summary>PDF reads no product-owned input document: its edit document is the object table's.</summary>
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } = [];
}
