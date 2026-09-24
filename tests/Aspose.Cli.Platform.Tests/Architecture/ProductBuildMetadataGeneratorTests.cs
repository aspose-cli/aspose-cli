using Aspose.Cli.Sdk.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductBuildMetadataGeneratorTests
{
    [Fact]
    public void CompilerVisibleProperties_GenerateProductLocalConstants()
    {
        GeneratorDriverRunResult result = Run(new Dictionary<string, string>
        {
            ["build_property.AsposeProductId"] = "sample",
            ["build_property.AsposeProductNamespace"] = "Aspose.Cli.Product.Sample",
            ["build_property.AsposeProductDisplayName"] = "Sample",
            ["build_property.AsposeProductEngineName"] = "Aspose.Sample",
            ["build_property.AsposeProductSdkPackageId"] = "Aspose.Sample",
            ["build_property.AsposeProductSdkVersion"] = "26.8.0",
            ["build_property.AsposeProductDisplayOrder"] = "2",
            ["build_property.AsposeProductDefaultCandidate"] = "true",
        });

        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        string generated = Assert.Single(Assert.Single(result.Results).GeneratedSources)
            .SourceText
            .ToString();
        Assert.Contains(
            "namespace Aspose.Cli.Product.Sample;",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProductId = \"sample\"",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "SdkVersion = \"26.8.0\"",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("DisplayOrder = 2;", generated, StringComparison.Ordinal);
        Assert.Contains("IsDefaultCandidate = true;", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSdkPackageId_GeneratesStableEngineMetadata()
    {
        GeneratorDriverRunResult result = Run(new Dictionary<string, string>
        {
            ["build_property.AsposeProductId"] = "sample",
            ["build_property.AsposeProductNamespace"] = "Aspose.Cli.Product.Sample",
            ["build_property.AsposeProductDisplayName"] = "Sample",
            ["build_property.AsposeProductEngineName"] = "Sample.SourceEngine",
            ["build_property.AsposeProductSdkVersion"] = "1.2.3",
            ["build_property.AsposeProductDisplayOrder"] = "1",
            ["build_property.AsposeProductDefaultCandidate"] = "false",
        });

        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        string generated = Assert.Single(Assert.Single(result.Results).GeneratedSources)
            .SourceText
            .ToString();
        Assert.Contains(
            "EngineName = \"Sample.SourceEngine\"",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "SdkPackageId = \"\"",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "SdkVersion = \"1.2.3\"",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MissingCompilerVisibleProperty_IsACompileTimeError()
    {
        GeneratorDriverRunResult result = Run(new Dictionary<string, string>
        {
            ["build_property.AsposeProductId"] = "sample",
            ["build_property.AsposeProductNamespace"] = "Aspose.Cli.Product.Sample",
        });

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static item => item.Id == "APCLI005");
        Assert.Contains(
            "AsposeProductDisplayName",
            diagnostic.GetMessage(),
            StringComparison.Ordinal);
    }

    private static GeneratorDriverRunResult Run(
        IReadOnlyDictionary<string, string> properties)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Aspose.Cli.Product.Sample",
            [CSharpSyntaxTree.ParseText(
                "namespace Aspose.Cli.Product.Sample; public sealed class Entry { }")],
            RoslynTestSupport.PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new ProductBuildMetadataGenerator().AsSourceGenerator()],
            optionsProvider: new TestOptionsProvider(properties));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private sealed class TestOptionsProvider(
        IReadOnlyDictionary<string, string> properties)
        : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _global =
            new DictionaryOptions(properties);
        private static readonly AnalyzerConfigOptions Empty =
            new DictionaryOptions(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            Empty;

        public override AnalyzerConfigOptions GetOptions(
            AdditionalText textFile) =>
            Empty;
    }

    private sealed class DictionaryOptions(
        IReadOnlyDictionary<string, string> properties)
        : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (properties.TryGetValue(key, out string? found))
            {
                value = found;
                return true;
            }
            value = string.Empty;
            return false;
        }
    }
}
