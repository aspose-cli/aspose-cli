using System.CommandLine;
using Aspose.Cli.Host;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Serialization;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Architecture.Tests;

public sealed class HostCatalogIsolationTests
{
    [Fact]
    public void RootCommandFactory_UsesOnlyItsInjectedCatalog()
    {
        ProductCatalog alpha = Catalog("alpha", ".alpha");
        ProductCatalog beta = Catalog("beta", ".beta");

        RootCommand alphaRoot = RootCommandFactory.Create(
            new HostContext(alpha),
            out _);
        RootCommand betaRoot = RootCommandFactory.Create(
            new HostContext(beta),
            out _);

        Assert.Contains(alphaRoot.Subcommands, static command => command.Name == "alpha");
        Assert.DoesNotContain(alphaRoot.Subcommands, static command => command.Name == "beta");
        Assert.Contains(betaRoot.Subcommands, static command => command.Name == "beta");
        Assert.DoesNotContain(betaRoot.Subcommands, static command => command.Name == "alpha");

        Command capabilities = alphaRoot.Subcommands.Single(
            static command => command.Name == "capabilities");
        Assert.True(capabilities.TryGetHelpMetadata(out CommandHelpMetadata? metadata));
        Assert.Equal(
        [
            "aspose-cli capabilities --output json",
            "aspose-cli capabilities <product> --output json",
            "aspose-cli capabilities <product> <verb> --output json",
        ],
            metadata!.Examples);

        Command review = alphaRoot.Subcommands.Single(
            static command => command.Name == "review");
        Assert.True(review.TryGetHelpMetadata(out metadata));
        Assert.Equal(
        [
            "aspose-cli review <file>",
            "aspose-cli review <file> --out evidence --max-items 50 --output json",
        ],
            metadata!.Examples);
    }

    private static ProductCatalog Catalog(string id, string extension)
    {
        ProductDefinition definition = ExtProduct.Define<ITestSession>(
                new ProductManifest
                {
                    Id = id,
                    DisplayName = id,
                    Operations = [],
                    Engine = new ProductEngineCapabilities
                    {
                        Id = "test",
                        Sdk = "Aspose.Test",
                        SdkVersion = "1.0.0",
                        LicenseApplicable = true,
                        LicenseRequired = false,
                        SupportsFontDiagnostics = true,
                    },
                    AvailableEngines = ["test"],
                })
            .Formats(
            [
                FormatDescriptor.Input(
                    id,
                    0,
                    RouteOwnership.Explicit,
                    extension),
            ])
            .Diagnostics([])
            .Json(new ProductJsonDefinition(id, SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ITestSession>())
            .WithCommand<ITestSession, TestResult>()
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Catalog isolation tests must not activate product ports."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestSession;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : ResultEnvelope("test/result", 1);
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }
}
