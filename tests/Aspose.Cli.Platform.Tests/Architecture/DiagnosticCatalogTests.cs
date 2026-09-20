using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Serialization;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Architecture.Tests;

public sealed class DiagnosticCatalogTests
{
    [Fact]
    public void SingleProductCatalog_ContainsOnlyCommonAndThatProductDiagnostics()
    {
        DiagnosticDescriptor productDiagnostic = DiagnosticDescriptor.Error(
            new ErrorCode("ALPHA_FAILURE", ExitCode.ValidationError),
            "alpha",
            "validation");
        ProductCatalog catalog = Build("alpha", [productDiagnostic]);

        Assert.All(catalog.Diagnostics.All, descriptor =>
            Assert.Contains(descriptor.Owner, new[] { "common", "alpha" }));
        Assert.Contains(
            catalog.Diagnostics.All,
            static descriptor => descriptor.Code == "ALPHA_FAILURE");
        Assert.DoesNotContain(
            catalog.Diagnostics.All,
            static descriptor => descriptor.Owner == "beta");
    }

    [Fact]
    public void Build_RejectsDuplicateOwnerSchemaExitAndHintViolations()
    {
        DiagnosticDescriptor valid = DiagnosticDescriptor.Error(
            new ErrorCode("ALPHA_FAILURE", ExitCode.ValidationError),
            "alpha",
            "validation");

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid, valid]));
        Assert.Contains("multiple owners", duplicate.Message, StringComparison.Ordinal);

        DiagnosticDescriptor crossSeverity = DiagnosticDescriptor.Warning(
            valid.Code,
            "alpha",
            "warning");
        InvalidOperationException collision = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid, crossSeverity]));
        Assert.Contains(
            "reused across severities",
            collision.Message,
            StringComparison.Ordinal);

        InvalidOperationException owner = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid with { Owner = "beta" }]));
        Assert.Contains("does not match", owner.Message, StringComparison.Ordinal);

        InvalidOperationException schema = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid with { DetailsSchemaId = "v2/alpha/missing" }]));
        Assert.Contains("unknown details schema", schema.Message, StringComparison.Ordinal);

        InvalidOperationException exit = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid with { ExitCode = ExitCode.Success }]));
        Assert.Contains("invalid exit code", exit.Message, StringComparison.Ordinal);

        InvalidOperationException hint = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid with { HintTemplateId = string.Empty }]));
        Assert.Contains("incomplete", hint.Message, StringComparison.Ordinal);

    }

    private static ProductCatalog Build(
        string id,
        IReadOnlyList<DiagnosticDescriptor> diagnostics)
    {
        ProductDefinition definition = ExtProduct.Define<ITestPort>(new ProductManifest
            {
                Id = id,
                DisplayName = id,
                Operations = [],
                Engine = new Aspose.Cli.Sdk.Contracts.ProductEngineCapabilities
                {
                    Id = "aspose",
                    Sdk = "Aspose.Test",
                    SdkVersion = "1.0.0",
                    LicenseApplicable = true,
                    LicenseRequired = false,
                    SupportsFontDiagnostics = true,
                },
                AvailableEngines = ["aspose"],
            })
            .Formats(
            [
                FormatDescriptor.Input(
                    "alpha",
                    0,
                    RouteOwnership.Explicit,
                    ".alpha"),
            ])
            .Diagnostics(diagnostics)
            .Json(new ProductJsonDefinition(id, SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ITestPort>())
            .Output<TestResult>(static (_, _) => { })
            .Commands(_ => new Command(id))
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Diagnostic tests must not activate product ports."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestPort;

    private sealed record TestResult() : ResultEnvelope("test/diagnostic", 1);

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }
}
