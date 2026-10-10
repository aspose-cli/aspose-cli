using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Serialization;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;
using Aspose.Cli.Platform.Tests;

namespace Aspose.Cli.Architecture.Tests;

public sealed class DiagnosticCatalogTests
{
    [Fact]
    public void SingleProductCatalog_ContainsOnlyCommonAndThatProductDiagnostics()
    {
        DiagnosticDescriptor productDiagnostic = DiagnosticDescriptor.Error(
            new ErrorCode("ALPHA_FAILURE", ExitCode.ValidationError),
            "alpha");
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
            "alpha");

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(
            () => Build("alpha", [valid, valid]));
        Assert.Contains("multiple owners", duplicate.Message, StringComparison.Ordinal);

        DiagnosticDescriptor crossSeverity = DiagnosticDescriptor.Warning(
            new WarningCode(valid.Code),
            "alpha");
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
    }

    [Fact]
    public void With_ChecksAnotherOwnersDiagnosticsAgainstTheCatalog()
    {
        DiagnosticCatalog catalog = Build("alpha", [DiagnosticDescriptor.Error(
            new ErrorCode("ALPHA_FAILURE", ExitCode.ValidationError),
            "alpha")]).Diagnostics;
        DiagnosticDescriptor added = DiagnosticDescriptor.Warning(new WarningCode("HOST_NOTICE"), "host");

        DiagnosticCatalog combined = catalog.With([added], "host");
        Assert.Equal(
            [.. catalog.All.Append(added).Select(static descriptor => descriptor.Code).Order(StringComparer.Ordinal)],
            combined.All.Select(static descriptor => descriptor.Code));

        Assert.Contains(
            "reused across severities",
            Assert.Throws<InvalidOperationException>(() => catalog.With(
                [DiagnosticDescriptor.Warning(new WarningCode("ALPHA_FAILURE"), "host")], "host")).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "multiple owners",
            Assert.Throws<InvalidOperationException>(() => catalog.With(
                [DiagnosticDescriptor.Error(new ErrorCode("ALPHA_FAILURE", ExitCode.ValidationError), "host")], "host")).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not match",
            Assert.Throws<InvalidOperationException>(() => catalog.With([added], "alpha")).Message,
            StringComparison.Ordinal);
    }

    private static ProductCatalog Build(
        string id,
        IReadOnlyList<DiagnosticDescriptor> diagnostics)
    {
        ProductDefinition definition = ExtProduct.Define<ITestSession>(new ProductManifest
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
            .View(new TestProductViewAdapter<ITestSession>())
            .WithCommand<ITestSession, TestResult>()
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Diagnostic tests must not activate a product session."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestSession;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : TestResultEnvelope("diagnostic");
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }
}
