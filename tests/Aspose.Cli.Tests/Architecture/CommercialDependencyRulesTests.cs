using System.Reflection;

namespace Aspose.Cli.Tests;

/// <summary>Enforces product and launcher boundaries for the Commercial distribution.</summary>
public sealed class CommercialDependencyRulesTests
{
    private static readonly Assembly CommercialLauncher = typeof(Program).Assembly;

    private static readonly IReadOnlyList<Assembly> Products =
        CompiledProductCatalog.Instance.Resources.Products
            .Select(static product => product.ResourceAssembly)
            .Distinct()
            .ToArray();

    [Fact]
    public void EveryProduct_DependsOnSdkAndNoOtherProduct()
    {
        foreach (Assembly product in Products)
        {
            string[] references = ReferenceNames(product);
            Assert.Contains("Aspose.Cli.Sdk", references);
            Assert.DoesNotContain(
                references,
                static reference => reference.StartsWith(
                    "Aspose.Cli.Product.",
                    StringComparison.Ordinal));
            Assert.Contains(
                references,
                static reference => reference.StartsWith(
                    "Aspose.",
                    StringComparison.Ordinal)
                    && !reference.StartsWith(
                        "Aspose.Cli.",
                        StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CommercialLauncher_ComposesHostSdkAndActiveProducts()
    {
        string[] references = ReferenceNames(CommercialLauncher);
        string[] productReferences = references
            .Where(static reference => reference.StartsWith(
                "Aspose.Cli.Product.",
                StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = Products
            .Select(static assembly => assembly.GetName().Name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, productReferences);
        Assert.Contains("Aspose.Cli.Host", references);
        Assert.Contains("Aspose.Cli.Sdk", references);
        Assert.DoesNotContain(
            references,
            static reference => reference.StartsWith(
                "Aspose.",
                StringComparison.Ordinal)
                && !reference.StartsWith(
                    "Aspose.Cli.",
                    StringComparison.Ordinal));
    }

    private static string[] ReferenceNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name!)
            .ToArray();
}
