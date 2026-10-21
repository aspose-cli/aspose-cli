using System.Collections.Immutable;
using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Extensibility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Positive and negative compilation tests for the security boundary rules, which production
/// code enables as errors through the <c>[src/**.cs]</c> section of <c>.editorconfig</c>.
/// </summary>
public sealed class SecurityBoundaryAnalyzerTests
{
    private const string ProductHeader =
        """
        using Aspose.Cli.Sdk.Extensibility;
        [assembly: ProductModule("demo", typeof(Demo.DemoModule))]
        namespace Demo;
        public sealed class DemoModule : IProductModule
        {
            public ProductDefinition Define() =>
                throw new System.NotSupportedException();
        }
        """;

    [Theory]
    [InlineData("public static object Open() => new System.Net.Http.HttpClient();")]
    [InlineData("public static object Open() => System.Net.WebRequest.Create(\"https://example.com/\");")]
    [InlineData("public static void Open(System.Net.WebClient client) => client.Dispose();")]
    [InlineData("public static object Open() => new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);")]
    [InlineData("public static object Open() => new System.Net.Sockets.TcpClient();")]
    [InlineData("public static object Open() => new System.Net.Sockets.UdpClient();")]
    [InlineData("public static object Open() => new System.Net.Http.HttpMessageInvoker(new System.Net.Http.SocketsHttpHandler());")]
    [InlineData("public static object Open() => new System.Net.WebSockets.ClientWebSocket();")]
    public async Task Apcli015_ReportsANetworkClientInAProduct(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            ProductHeader + "\npublic static class Fetcher { " + member + " }");

        Assert.Contains(diagnostics, static item => item.Id == "APCLI015");
    }

    [Fact]
    public async Task Apcli015_AllowsANetworkClientOutsideAProduct()
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            """
            namespace Demo.Host;
            public static class Updater
            {
                public static object Open() => new System.Net.Http.HttpClient();
                public static object Connect() => new System.Net.Sockets.TcpClient();
            }
            """);

        Assert.DoesNotContain(diagnostics, static item => item.Id == "APCLI015");
    }

    [Fact]
    public async Task Apcli015_AllowsAProductWithoutANetworkClient()
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            ProductHeader + "\npublic static class Links { public static bool IsWeb(System.Uri uri) => uri.Scheme == \"https\"; "
                + "public static string Names() => System.Net.Http.HttpMethod.Get.Method + System.Net.IPAddress.Loopback; }");

        Assert.DoesNotContain(diagnostics, static item => item.Id == "APCLI015");
    }

    [Theory]
    [InlineData("public static string Root() => System.IO.Path.GetTempPath();")]
    [InlineData("public static System.Func<string> Root() => System.IO.Path.GetTempPath;")]
    [InlineData("public static string Root() => System.IO.Path.GetTempFileName();")]
    [InlineData("public static string? Root() => System.Environment.GetEnvironmentVariable(\"TEMP\");")]
    [InlineData("public static string? Root() => System.Environment.GetEnvironmentVariable(\"tmp\", System.EnvironmentVariableTarget.User);")]
    public async Task Apcli016_ReportsTheTemporaryPathOutsideUserStorage(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            "namespace Demo; public static class Scratch { " + member + " }");

        Assert.Single(diagnostics, static item => item.Id == "APCLI016");
    }

    [Fact]
    public async Task Apcli016_AllowsTheTemporaryPathInUserStorage()
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            """
            namespace Aspose.Cli.Sdk.IO.Paths;
            public static class UserStorage
            {
                public static string Root() => System.IO.Path.GetTempPath();
                public static string File() => System.IO.Path.GetTempFileName();
                public static string? Variable() => System.Environment.GetEnvironmentVariable("TEMP");
            }
            """);

        Assert.DoesNotContain(diagnostics, static item => item.Id == "APCLI016");
    }

    [Fact]
    public async Task Apcli016_AllowsOtherEnvironmentVariablesOutsideUserStorage()
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            "namespace Demo; public static class Settings { public static string? Search() => System.Environment.GetEnvironmentVariable(\"PATH\"); }");

        Assert.DoesNotContain(diagnostics, static item => item.Id == "APCLI016");
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        // Every analyzer of the assembly, enabled as .editorconfig enables them for src/.
        ImmutableArray<DiagnosticAnalyzer> analyzers = [.. typeof(ProductContractAnalyzer).Assembly.GetTypes()
            .Where(static type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .Select(static type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)];
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Demo.Assembly",
            [CSharpSyntaxTree.ParseText(source)],
            RoslynTestSupport.PlatformReferences()
                .Append(MetadataReference.CreateFromFile(typeof(IProductModule).Assembly.Location)),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    ["APCLI015"] = ReportDiagnostic.Error,
                    ["APCLI016"] = ReportDiagnostic.Error,
                }));
        ImmutableArray<Diagnostic> compilerErrors = [.. compilation.GetDiagnostics()
            .Where(static item => item.Severity == DiagnosticSeverity.Error)];
        Assert.True(compilerErrors.IsEmpty, string.Join(Environment.NewLine, compilerErrors));

        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }
}
