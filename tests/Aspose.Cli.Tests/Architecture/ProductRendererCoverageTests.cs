using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Tests;

/// <summary>
/// Table and text output falls back to JSON for a result type without a renderer, which the
/// compiler cannot catch. Every result a product declares has a renderer in its definition;
/// the SDK and Host results are checked by <c>TableRendererCoverageTests</c>.
/// </summary>
public sealed class ProductRendererCoverageTests
{
    [Fact]
    public void EveryProductResult_HasARegisteredRenderer()
    {
        ProductCatalog catalog = CompiledProductCatalog.Instance;
        Assert.NotEmpty(catalog.Products);

        var missing = new List<string>();
        foreach (ProductDefinition product in catalog.Products)
        {
            string id = product.Manifest.Id;
            Type[] results = catalog.Resources.GetProduct(id).ResourceAssembly.GetTypes()
                .Where(static type => type is { IsAbstract: false, IsClass: true } && type.IsAssignableTo(typeof(ResultEnvelope)))
                .ToArray();
            Assert.NotEmpty(results);

            var rendered = product.Outputs.Select(static output => output.ResultType).ToHashSet();
            missing.AddRange(results
                .Where(type => !rendered.Contains(type))
                .Select(type => $"{id}: {type.FullName}"));
        }

        Assert.True(
            missing.Count == 0,
            "These product results render as JSON in table and text mode because their product registers no renderer:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }
}
