using Aspose.Cli.TestKit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Applies the shared product autonomy contract to Slides.</summary>
public sealed class SlidesModuleTests
    : ProductContractTests<SlidesModule>
{
    /// <summary>Slides reads no product-owned input document: its edit document is the object table's.</summary>
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } = [];
}
