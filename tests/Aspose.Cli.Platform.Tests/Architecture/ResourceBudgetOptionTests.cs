using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Serialization;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;
using Aspose.Cli.Platform.Tests;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ResourceBudgetOptionTests
{
    [Fact]
    public void Build_AcceptsABudgetRaisedByARegisteredOption()
    {
        ProductCatalog catalog = Build("--max-items");

        Assert.Equal("--max-items", Assert.Single(
            catalog.GetCapabilities(Assert.Single(catalog.Products)).ResourceBudgets).Option);
    }

    [Fact]
    public void Build_RejectsABudgetNamingAnOptionNoCommandRegisters()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Build("--max-pixels"));

        Assert.Contains("'--max-pixels'", error.Message, StringComparison.Ordinal);
    }

    private static ProductCatalog Build(string budgetOption)
    {
        ProductDefinition definition = ExtProduct.Define<ITestSession>(new ProductManifest
            {
                Id = "alpha",
                DisplayName = "alpha",
                ResourceBudgets =
                [
                    ResourceBudgetCapabilities.Domain("alpha.items", 10, 100, "items", "projection", budgetOption),
                ],
                Engine = ProductEngineCapabilities.LicenseAware("aspose", "Aspose.Test", "1.0.0"),
                AvailableEngines = ["aspose"],
            })
            .Formats([FormatDescriptor.Input("alpha", 0, RouteOwnership.Explicit, ".alpha")])
            .Diagnostics([])
            .Json(new ProductJsonDefinition("alpha", SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ITestSession>())
            .WithCommand<ITestSession, TestResult>("read", new Option<int>("--max-items"))
            .Activator(static _ =>
                throw new InvalidOperationException("Budget validation must not activate a product session."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestSession;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : TestResultEnvelope("budget");
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }
}
