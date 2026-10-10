using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.Platform.Tests;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// Every invocation builds the product catalog before it parses its command line, so the catalog
/// defers what most commands never read: it checks that a diagnostic's details schema is
/// published without writing the schema, which is written when a command reads it, and a
/// pattern constraint compiles its expression on its first check.
/// </summary>
public sealed class StartupCatalogTests
{
    private const string DetailsSchemaId = "v2/alpha/unwritable";

    [Fact]
    public void Build_ChecksADiagnosticDetailsSchemaWithoutWritingIt()
    {
        DiagnosticDescriptor diagnostic = DiagnosticDescriptor.Error(
            new ErrorCode("ALPHA_FAILURE", ExitCode.ValidationError),
            "alpha") with { DetailsSchemaId = DetailsSchemaId };

        // The details schema's record names a base no generator described, so writing it fails.
        ProductCatalog catalog = Build(diagnostic);

        Assert.Contains(catalog.Diagnostics.All, static descriptor => descriptor.Code == "ALPHA_FAILURE");
        Assert.Contains(DetailsSchemaId, catalog.Resources.SchemaIds);
        Assert.Contains(
            nameof(UndescribedBase),
            Assert.Throws<InvalidOperationException>(() => catalog.Resources.Read(DetailsSchemaId)).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A pattern compiles on its first check rather than when the catalog starts, so every
    /// pattern this build declares is checked once here: a malformed one still fails a test.
    /// </summary>
    [Fact]
    public void EveryDeclaredPattern_Compiles()
    {
        Assembly[] assemblies =
        [
            typeof(PatternAttribute).Assembly,
            typeof(CliHost).Assembly,
            .. ActualCommandTree.Host.Catalog.Resources.Products.Select(static product => product.ResourceAssembly),
        ];
        var failures = new List<string>();
        int checkedPatterns = 0;
        foreach (PropertyInfo property in assemblies.SelectMany(static assembly => assembly.GetTypes())
            .SelectMany(static type => type.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
        {
            foreach (PatternAttribute pattern in property.GetCustomAttributes<PatternAttribute>())
            {
                checkedPatterns++;
                try
                {
                    _ = pattern.Check(string.Empty);
                }
                catch (ArgumentException error)
                {
                    failures.Add($"{property.DeclaringType}.{property.Name} [Pattern(\"{pattern.Pattern}\")]: {error.Message}");
                }
            }
        }

        Assert.True(checkedPatterns > 0, "No declared pattern was found.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static ProductCatalog Build(DiagnosticDescriptor diagnostic)
    {
        ProductDefinition definition = ExtProduct.Define<IStartupSession>(new ProductManifest
            {
                Id = "alpha",
                DisplayName = "alpha",
                Engine = new ProductEngineCapabilities
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
            .Formats([FormatDescriptor.Input("alpha", 0, RouteOwnership.Explicit, ".alpha")])
            .Diagnostics([diagnostic])
            .Json(new Aspose.Cli.Sdk.Serialization.ProductJsonDefinition("alpha", new UnwritableSchemaSource()))
            .View(new TestProductViewAdapter<IStartupSession>())
            .WithCommand<IStartupSession, StartupResult>()
            .Activator(static _ =>
                throw new InvalidOperationException("Startup tests must not activate a product session."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface IStartupSession;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record StartupResult() : TestResultEnvelope("startup");
#pragma warning restore APCLI003

    private sealed record UnwritableDetails;

    private sealed record UndescribedBase;

    /// <summary>Publishes one details schema whose record cannot be written.</summary>
    private sealed class UnwritableSchemaSource : IJsonTypeInfoResolver, IResultSchemaSource
    {
        public IReadOnlyList<ResultRecord> ResultRecords { get; } =
        [
            new ResultRecord
            {
                Type = typeof(UnwritableDetails),
                Base = typeof(UndescribedBase),
                SchemaId = "unwritable",
            },
        ];

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null;
    }

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }
}
