using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Serialization;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

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
        ProductDefinition definition = ExtProduct.Define<ITestPort>(new ProductManifest
            {
                Id = "alpha",
                DisplayName = "alpha",
                Operations = [],
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
            .View(new TestProductViewAdapter<ITestPort>())
            .Output<TestResult>(static (_, _) => { })
            .Commands(static _ =>
            {
                var read = new Command("read");
                read.Options.Add(new Option<int>("--max-items"));
                var root = new Command("alpha");
                root.Subcommands.Add(read);
                return root;
            })
            .Activator(static _ =>
                throw new InvalidOperationException("Budget validation must not activate product ports."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestPort;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : ResultEnvelope("test/budget", 1);
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }
}
