using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductJsonContextGeneratorTests
{
    [Fact]
    public void ProductJsonRoot_WithContextMetadata_GeneratesWithoutDiagnostic()
    {
        GeneratorDriverRunResult result = Run(
            """
            using System.Text.Json.Serialization;
            using Aspose.Cli.Sdk.Serialization;
            namespace Sample.Product;
            [ProductJsonRoot]
            public sealed class Batch;
            [JsonSerializable(typeof(Batch))]
            public partial class ProductJsonContext : JsonSerializerContext;
            """);

        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Id == "APCLI003");
        Assert.Empty(Assert.Single(result.Results).GeneratedSources);
    }

    [Fact]
    public void ProductJsonRoot_WithoutContextMetadata_IsACompileTimeError()
    {
        GeneratorDriverRunResult result = Run(
            """
            using Aspose.Cli.Sdk.Serialization;
            namespace Sample.Product;
            [ProductJsonRoot]
            public sealed class Batch;
            """);

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static item => item.Id == "APCLI003");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(
            "Sample.Product.Batch",
            diagnostic.GetMessage(),
            StringComparison.Ordinal);
    }

    private static GeneratorDriverRunResult Run(string source)
    {
        IEnumerable<MetadataReference> references =
            RoslynTestSupport.PlatformReferences()
            .Append(MetadataReference.CreateFromFile(
                typeof(ProductJsonRootAttribute).Assembly.Location));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Sample.Product",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ProductJsonContextGenerator());

        return driver.RunGenerators(compilation).GetRunResult();
    }

}
