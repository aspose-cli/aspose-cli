using System.Collections.Immutable;
using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Extensibility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>Positive and negative compilation tests for every isolation rule.</summary>
public sealed class ProductContractAnalyzerTests
{
    [Fact]
    public async Task Apcli006_ReportsAsposeTypeInPublicApi()
    {
        MetadataReference fakeSdk = CompileReference(
            "Aspose.Fake",
            "namespace Aspose.Fake; public sealed class Document { }");
        string source = ProductSource(
            """
            public sealed class PublicApi
            {
                public Aspose.Fake.Document Open() => new();
            }
            """);

        ImmutableArray<Diagnostic> diagnostics =
            await Analyze(source, fakeSdk);

        Assert.Single(diagnostics, static item => item.Id == "APCLI006");
    }

    [Fact]
    public async Task Apcli006_ReportsAsposeTypesInTypeAndMethodConstraints()
    {
        MetadataReference fakeSdk = CompileReference(
            "Aspose.Fake",
            "namespace Aspose.Fake; public class Document { }");
        string source = ProductSource(
            """
            public interface IMarker<T> { }

            public sealed class PublicApi<T>
                where T : Aspose.Fake.Document
            {
                public void Open<TValue>()
                    where TValue : IMarker<Aspose.Fake.Document>
                {
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics =
            await Analyze(source, fakeSdk);

        Assert.Equal(
            2,
            diagnostics.Count(static item => item.Id == "APCLI006"));
    }

    [Fact]
    public async Task Apcli006_AllowsFrameworkAndSelfReferentialConstraints()
    {
        string source = ProductSource(
            """
            public sealed class PublicApi<T>
                where T : System.IComparable<T>
            {
                public void Open<TValue>()
                    where TValue : System.Collections.Generic.IEnumerable<T>
                {
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI006");
    }

    [Fact]
    public async Task Apcli007_ReportsFileAccessFromDefine()
    {
        string source = ProductSource(
            """
            public sealed class ExtraModule : IProductModule
            {
                public ProductDefinition Define()
                {
                    _ = System.IO.File.Exists("input.txt");
                    throw new System.NotSupportedException();
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.Single(diagnostics, static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_ReportsIndirectHelperAndIncludesCallPath()
    {
        string source = ProductSource(
            """
            public sealed class ExtraModule : IProductModule
            {
                public ProductDefinition Define() => Build();

                private static ProductDefinition Build()
                {
                    _ = System.IO.File.Exists("input.txt");
                    throw new System.NotSupportedException();
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            await Analyze(source),
            static item => item.Id == "APCLI007");

        Assert.Contains(
            "ExtraModule.Define() -> Demo.ExtraModule.Build()",
            diagnostic.GetMessage(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "private static string Value => System.IO.File.ReadAllText(\"input.txt\");",
        "_ = Value;")]
    [InlineData(
        "private static readonly string Value = System.IO.File.ReadAllText(\"input.txt\");",
        "_ = Value;")]
    [InlineData(
        "static ExtraModule() { _ = System.Environment.CurrentDirectory; }",
        "")]
    [InlineData(
        "public ExtraModule() { _ = System.Environment.CurrentDirectory; }",
        "")]
    public async Task Apcli007_ReportsInitializationAndPropertyBypasses(
        string member,
        string defineStatement)
    {
        string source = ProductSource(
            $$"""
            public sealed class ExtraModule : IProductModule
            {
                {{member}}

                public ProductDefinition Define()
                {
                    {{defineStatement}}
                    throw new System.NotSupportedException();
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.Contains(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Theory]
    [InlineData("_ = System.DateTime.UtcNow;")]
    [InlineData("_ = System.Random.Shared.Next();")]
    [InlineData("_ = System.Diagnostics.Process.GetCurrentProcess();")]
    [InlineData("_ = System.Net.Dns.GetHostName();")]
    [InlineData("_ = new System.Threading.Thread(() => { });")]
    [InlineData("_ = System.Threading.Tasks.Task.Run(() => { });")]
    [InlineData("_ = System.Reflection.Assembly.Load(\"dynamic\");")]
    [InlineData("_ = System.Type.GetType(\"Demo.PublicApi\");")]
    public async Task Apcli007_ReportsEveryRuntimeStateFamily(
        string statement)
    {
        string source = ProductSource(
            $$"""
            public sealed class ExtraModule : IProductModule
            {
                public ProductDefinition Define()
                {
                    {{statement}}
                    throw new System.NotSupportedException();
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.Contains(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_ReportsIndirectAsposeInitialization()
    {
        MetadataReference fakeSdk = CompileReference(
            "Aspose.Fake",
            "namespace Aspose.Fake; public sealed class Document { }");
        string source = ProductSource(
            """
            public sealed class ExtraModule : IProductModule
            {
                public ProductDefinition Define() => Build();

                private static ProductDefinition Build()
                {
                    _ = new Aspose.Fake.Document();
                    throw new System.NotSupportedException();
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics =
            await Analyze(source, fakeSdk);

        Assert.Contains(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_AllowsDeterministicFormatProjection()
    {
        string source = ProductSource(
            """
            public sealed class ExtraModule : IProductModule
            {
                public ProductDefinition Define()
                {
                    _ = FormatDescriptorExtensions.IdsFor(
                        System.Array.Empty<FormatDescriptor>(), FormatUse.Input);
                    throw new System.NotSupportedException();
                }
            }
            """);
        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);
        Assert.DoesNotContain(diagnostics, static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_DoesNotTrustAnUnknownSdkHelper()
    {
        MetadataReference helper = CompileReference(
            "Aspose.Cli.Sdk.Fake",
            """
            namespace Aspose.Cli.Sdk.Extensibility;
            public static class RuntimeHelper
            {
                public static string Read() => "hidden runtime access";
            }
            """);
        string source = ProductSource(
            """
            public sealed class ExtraModule : IProductModule
            {
                public ProductDefinition Define()
                {
                    _ = Aspose.Cli.Sdk.Extensibility.RuntimeHelper.Read();
                    throw new System.NotSupportedException();
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics =
            await Analyze(source, helper);

        Assert.Contains(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_AllowsRuntimeLogicInsideDeferredFactory()
    {
        string source = ProductSource(
            """
            public interface IPort { }

            public sealed class DeferredModule : IProductModule
            {
                public ProductDefinition Define() =>
                    Product.Define<IPort>(new ProductManifest
                    {
                        Id = "deferred",
                        DisplayName = "Deferred",
                        Operations = System.Array.Empty<Aspose.Cli.Sdk.Extensibility.ProductOperationCommand>(),
                        Engine = new Aspose.Cli.Sdk.Contracts.ProductEngineCapabilities
                        {
                            Id = "aspose",
                            Sdk = "Aspose.Test",
                            SdkVersion = "1.0.0",
                            LicenseApplicable = true,
                            LicenseRequired = false,
                            SupportsFontDiagnostics = true,
                        },
                        AvailableEngines = new[] { "aspose" },
                    })
                    .Activator(context =>
                    {
                        _ = System.IO.File.Exists("runtime.txt");
                        throw new System.NotSupportedException();
                    })
                    .Build();
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_AllowsTypedViewAdapterRegistration()
    {
        string source = ProductSource(
            """
            public interface IPort { }

            public sealed class ViewAdapter : IProductViewAdapter<IPort>
            {
                public System.Collections.Generic.IReadOnlyList<Aspose.Cli.Sdk.Views.ProductView> Views =>
                    new[]
                    {
                        new Aspose.Cli.Sdk.Views.ProductView(
                            "document",
                            "Document",
                            Aspose.Cli.Sdk.Views.ViewPartKinds.Image),
                    };
                public string ReviewView => "document";
                public string LiveView => "document";
                public bool VisualInspectionRequired => true;
                public System.Collections.Generic.IReadOnlyList<Aspose.Cli.Sdk.Contracts.ReviewCheck> Checks => [];
                public Aspose.Cli.Sdk.Views.ViewManifest Render(
                    IPort port,
                    string filePath,
                    Aspose.Cli.Sdk.Views.ViewRenderRequest request,
                    Aspose.Cli.Sdk.Views.IViewArtifactSink artifacts) =>
                    new Aspose.Cli.Sdk.Views.ViewManifest
                    {
                        View = "document",
                        SourceFormat = "test",
                        SourceSizeBytes = 0,
                        SourceEncrypted = false,
                        TotalPartCount = 0,
                        Parts = System.Array.Empty<Aspose.Cli.Sdk.Views.ViewPart>(),
                    };
                public ProductReviewAssessment Assess(
                    IPort port,
                    string filePath,
                    Aspose.Cli.Sdk.Views.ViewRenderRequest request,
                    Aspose.Cli.Sdk.Views.ViewManifest rendered) =>
                    new ProductReviewAssessment();
            }

            public sealed class ReviewModule : IProductModule
            {
                public ProductDefinition Define() =>
                    Product.Define<IPort>(new ProductManifest
                    {
                        Id = "review",
                        DisplayName = "Review",
                        Operations = System.Array.Empty<Aspose.Cli.Sdk.Extensibility.ProductOperationCommand>(),
                        Engine = new Aspose.Cli.Sdk.Contracts.ProductEngineCapabilities
                        {
                            Id = "aspose",
                            Sdk = "Aspose.Test",
                            SdkVersion = "1.0.0",
                            LicenseApplicable = true,
                            LicenseRequired = false,
                            SupportsFontDiagnostics = true,
                        },
                        AvailableEngines = new[] { "aspose" },
                    })
                    .View(new ViewAdapter())
                    .Build();
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli007_AllowsPureDeferredDefinition()
    {
        string source = ProductSource(
            """
            public sealed class PureDefinition
            {
                public static string Name => "demo";
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI007");
    }

    [Fact]
    public async Task Apcli008_ReportsReservedGlobalAlias()
    {
        string source = ProductSource(
            """
            public sealed class Commands
            {
                public System.CommandLine.Option<string> Create() =>
                    new("--output");
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.Single(diagnostics, static item => item.Id == "APCLI008");
    }

    [Theory]
    [InlineData("--out-dir")]
    [InlineData("-o")]
    [InlineData("--password-env")]
    [InlineData("--font-dir")]
    public async Task Apcli008_ReportsAnOptionTheCommandTemplateOwns(string name)
    {
        string source = ProductSource(
            $$"""
            public sealed class Commands
            {
                public System.CommandLine.Option<string> Create() =>
                    new("--input", "{{name}}");
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Diagnostic diagnostic = Assert.Single(diagnostics, static item => item.Id == "APCLI008");
        Assert.Contains("command template", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apcli008_ReservesEveryNameTheRootCommandAlreadyAccepts()
    {
        var root = new System.CommandLine.RootCommand();
        new Aspose.Cli.Host.Invocation.GlobalOptions(licensingApplicable: true).AddTo(root);

        string[] accepted = root.Options
            .SelectMany(static option => option.Aliases.Prepend(option.Name))
            .ToArray();

        Assert.Equal(
            accepted.Order(StringComparer.Ordinal),
            Aspose.Cli.Sdk.Extensibility.Commanding.GlobalOptionNames.Reserved.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Apcli008_ReportsDynamicAlias()
    {
        string source = ProductSource(
            """
            public sealed class Commands
            {
                public System.CommandLine.Option<string> Create(string alias) =>
                    new(alias);
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.Single(diagnostics, static item => item.Id == "APCLI008");
    }

    [Fact]
    public async Task Apcli008_AllowsStaticProductAlias()
    {
        string source = ProductSource(
            """
            public sealed class Commands
            {
                public System.CommandLine.Option<string> Create() =>
                    new("--input");
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI008");
    }

    [Fact]
    public async Task Apcli009_ReportsCommandDependencyOnEngineImplementation()
    {
        string source = LayeredProductSource(
            """
            namespace Demo.Engine
            {
                internal sealed class Workbook { }
            }

            namespace Demo.Commands
            {
                internal sealed class EditCommand
                {
                    internal object Run() => new Demo.Engine.Workbook();
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            await Analyze(source),
            static item => item.Id == "APCLI009");

        Assert.Contains("Commands", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Engine", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apcli009_AllowsCommandsToDependOnContractsAndPorts()
    {
        string source = LayeredProductSource(
            """
            namespace Demo.Contracts
            {
                public sealed record EditRequest(string Value);
            }

            namespace Demo.Ports
            {
                public interface IEditor
                {
                    Demo.Contracts.EditRequest Read();
                }
            }

            namespace Demo.Commands
            {
                internal sealed class EditCommand
                {
                    internal Demo.Contracts.EditRequest Run(Demo.Ports.IEditor editor) =>
                        editor.Read();
                }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI009");
    }

    [Fact]
    public async Task Apcli010_ReportsPublicImplementationType()
    {
        string source = LayeredProductSource(
            """
            namespace Demo.Engine
            {
                public sealed class PublicEngine { }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            await Analyze(source),
            static item => item.Id == "APCLI010");

        Assert.Contains("PublicEngine", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apcli010_AllowsPublicContractsAndPorts()
    {
        string source = LayeredProductSource(
            """
            namespace Demo.Contracts
            {
                public sealed record EditRequest(string Value);
            }

            namespace Demo.Ports
            {
                public interface IEditor { }
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(
            diagnostics,
            static item => item.Id == "APCLI010");
    }

    [Fact]
    public async Task Apcli011_ReportsAProductThatBuildsACommandThroughTheHostSeam()
    {
        string source = ProductSource(
            """
            public sealed class Commands
            {
                private Aspose.Cli.Sdk.Extensibility.Commanding.StandardOptions? _options;

                public System.CommandLine.Command Create() =>
                    new Aspose.Cli.Sdk.Extensibility.Commanding.StandardOptions(
                        new Aspose.Cli.Sdk.Extensibility.Commanding.CommandTraits())
                        .CreateCommand("run", "Runs.", []);

                public bool Built => _options is not null;
            }
            """);

        Diagnostic[] diagnostics = [.. (await Analyze(source)).Where(static item => item.Id == "APCLI011")];

        Assert.True(diagnostics.Length >= 2, string.Join(Environment.NewLine, diagnostics));
        Assert.All(diagnostics, static diagnostic =>
            Assert.Contains("StandardOptions", diagnostic.GetMessage(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Apcli011_AllowsTheCommandTraits()
    {
        string source = ProductSource(
            """
            public sealed class Commands
            {
                public Aspose.Cli.Sdk.Extensibility.Commanding.CommandTraits Traits() => new() { UsesFonts = true };
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await Analyze(source);

        Assert.DoesNotContain(diagnostics, static item => item.Id == "APCLI011");
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(
        string source,
        params MetadataReference[] additionalReferences)
    {
        IEnumerable<MetadataReference> references =
            RoslynTestSupport.PlatformReferences()
            .Append(MetadataReference.CreateFromFile(
                typeof(IProductModule).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(
                typeof(System.CommandLine.Option<>).Assembly.Location))
            .Concat(additionalReferences);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Demo.Product",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        ImmutableArray<Diagnostic> compilerErrors = compilation.GetDiagnostics()
            .Where(static item => item.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        Assert.True(
            compilerErrors.IsEmpty,
            string.Join(Environment.NewLine, compilerErrors));

        return await compilation
            .WithAnalyzers([new ProductContractAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }

    private static string ProductSource(string body) =>
        $$"""
        using Aspose.Cli.Sdk.Extensibility;
        [assembly: ProductModule("demo", typeof(Demo.DemoModule))]
        namespace Demo;
        public sealed class DemoModule : IProductModule
        {
            public ProductDefinition Define() =>
                throw new System.NotSupportedException();
        }
        {{body}}
        """;

    private static string LayeredProductSource(string body) =>
        $$"""
        using Aspose.Cli.Sdk.Extensibility;
        [assembly: ProductModule("demo", typeof(Demo.DemoModule))]
        namespace Demo
        {
            public sealed class DemoModule : IProductModule
            {
                public ProductDefinition Define() =>
                    throw new System.NotSupportedException();
            }
        }
        {{body}}
        """;

    private static MetadataReference CompileReference(
        string assemblyName,
        string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            RoslynTestSupport.PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        EmitResult result = compilation.Emit(stream);
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

}
