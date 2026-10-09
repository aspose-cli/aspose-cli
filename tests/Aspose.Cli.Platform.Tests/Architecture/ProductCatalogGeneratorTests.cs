using System.Text;
using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Extensibility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductCatalogGeneratorTests
{
    [Fact]
    public void ReferencedModules_AreGeneratedInDeterministicProductIdOrder()
    {
        MetadataReference zeta = CompileModule("Zeta.Product", "zeta", "ZetaModule");
        MetadataReference alpha = CompileModule("Alpha.Product", "alpha", "AlphaModule");

        GeneratorDriverRunResult result = RunGenerator(zeta, alpha);

        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        string generated = Assert.Single(Assert.Single(result.Results).GeneratedSources)
            .SourceText
            .ToString();
        int alphaIndex = generated.IndexOf(
            "new global::Alpha.Product.AlphaModule()",
            StringComparison.Ordinal);
        int zetaIndex = generated.IndexOf(
            "new global::Zeta.Product.ZetaModule()",
            StringComparison.Ordinal);
        Assert.True(alphaIndex >= 0);
        Assert.True(zetaIndex > alphaIndex);
    }

    [Fact]
    public void DuplicateProductId_IsACompileTimeError()
    {
        MetadataReference official = CompileModule(
            "Official.Cells",
            "cells",
            "OfficialCellsModule");
        MetadataReference replacement = CompileModule(
            "Replacement.Cells",
            "cells",
            "ReplacementCellsModule");

        GeneratorDriverRunResult result = RunGenerator(official, replacement);

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static item => item.Id == "APCLI001");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("cells", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("OfficialCellsModule", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("ReplacementCellsModule", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacementProductAlone_IsRegisteredWithoutHostChanges()
    {
        MetadataReference replacement = CompileModule(
            "Replacement.Cells",
            "cells",
            "ReplacementCellsModule");

        GeneratorDriverRunResult result = RunGenerator(replacement);

        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        string generated = Assert.Single(Assert.Single(result.Results).GeneratedSources)
            .SourceText
            .ToString();
        Assert.Contains(
            "new global::Replacement.Cells.ReplacementCellsModule()",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedCatalog_ContainsOneVersionedCatalogInstance()
    {
        MetadataReference sample = CompileModule(
            "Sample.Product",
            "sample",
            "SampleModule");
        GeneratorDriverRunResult result = RunGenerator(sample);

        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        string generated = Assert.Single(Assert.Single(result.Results).GeneratedSources)
            .SourceText
            .ToString();
        Assert.Contains("HostSdkVersion", generated, StringComparison.Ordinal);
        Assert.Contains("ProductCatalog Instance", generated, StringComparison.Ordinal);
        Assert.Contains(
            "new(\"sample\", new global::Sample.Product.SampleModule(),",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Create()", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatOwners", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void CompilationWithoutProducts_GetsNoCatalog()
    {
        GeneratorDriverRunResult result = RunGenerator();

        Assert.Empty(result.Diagnostics);
        Assert.Empty(Assert.Single(result.Results).GeneratedSources);
    }

    [Fact]
    public void AbstractModule_IsACompileTimeError()
    {
        MetadataReference invalid = CompileModule(
            "Invalid.Product",
            "invalid",
            "InvalidModule",
            isAbstract: true);

        GeneratorDriverRunResult result = RunGenerator(invalid);

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static item => item.Id == "APCLI002");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("InvalidModule", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleWithoutPublicParameterlessConstructor_IsACompileTimeError()
    {
        MetadataReference invalid = CompileModule(
            "Invalid.Product",
            "invalid",
            "InvalidModule",
            privateConstructor: true);

        GeneratorDriverRunResult result = RunGenerator(invalid);

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static item => item.Id == "APCLI002");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("InvalidModule", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductAssemblyWithoutAModuleAttribute_IsACompileTimeError()
    {
        GeneratorDriverRunResult result = RunGeneratorWithSource(
            "namespace Aspose.Cli.Product.Empty; public static class Entry { }",
            "Aspose.Cli.Product.Empty");

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static item => item.Id == "APCLI004");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(
            "exports 0 ProductModule attributes",
            diagnostic.GetMessage(),
            StringComparison.Ordinal);
    }

    private static GeneratorDriverRunResult RunGenerator(
        params MetadataReference[] productReferences)
        => RunGeneratorWithSource(
            "namespace Generator.Host; public static class Entry { }",
            productReferences);

    private static GeneratorDriverRunResult RunGeneratorWithSource(
        string hostSource,
        params MetadataReference[] productReferences)
        => RunGeneratorWithSource(
            hostSource,
            "Generator.Host",
            productReferences);

    private static GeneratorDriverRunResult RunGeneratorWithSource(
        string hostSource,
        string assemblyName,
        params MetadataReference[] productReferences)
    {
        IEnumerable<MetadataReference> references =
            RoslynTestSupport.PlatformReferences()
            .Append(MetadataReference.CreateFromFile(typeof(IProductModule).Assembly.Location))
            .Concat(productReferences);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(hostSource)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ProductCatalogGenerator());

        driver = driver.RunGenerators(compilation);

        return driver.GetRunResult();
    }

    private static MetadataReference CompileModule(
        string productNamespace,
        string productId,
        string moduleName,
        bool isAbstract = false,
        bool privateConstructor = false)
    {
        string abstractModifier = isAbstract ? "abstract " : string.Empty;
        string constructor = privateConstructor
            ? $"private {moduleName}() {{ }}"
            : string.Empty;
        string source = $$"""
            using System;
            using Aspose.Cli.Sdk.Extensibility;
            [assembly: ProductModule("{{productId}}", typeof({{productNamespace}}.{{moduleName}}))]
            namespace {{productNamespace}};
            public {{abstractModifier}}class {{moduleName}} : IProductModule
            {
                {{constructor}}
                public ProductDefinition Define() => throw new NotSupportedException();
            }
            """;
        IEnumerable<MetadataReference> references =
            RoslynTestSupport.PlatformReferences()
            .Append(MetadataReference.CreateFromFile(typeof(IProductModule).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(
                typeof(Aspose.Cli.Sdk.Contracts.ProductCapabilities).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(
                typeof(Aspose.Cli.Sdk.IO.SafeFileWriter).Assembly.Location));
        CSharpCompilation compilation = CSharpCompilation.Create(
            productNamespace,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        EmitResult result = compilation.Emit(stream);
        Assert.True(
            result.Success,
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

}
