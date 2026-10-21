using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// Keeps the security boundary's single owners: a product reaches no network client or socket of
/// its own, and only <c>UserStorage</c> chooses where per-user state lives below the temporary path. The
/// rules are disabled by default and enabled as errors for production code by the
/// <c>[src/**.cs]</c> section of <c>.editorconfig</c>, so tests stay free to use both.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SecurityBoundaryAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "Aspose.Cli.SecurityBoundary";

    private const string UserStorage = "Aspose.Cli.Sdk.IO.Paths.UserStorage";

    private static readonly ImmutableHashSet<string> NetworkClients =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "System.Net.WebRequest",
            "System.Net.Http.HttpMessageInvoker",
            "System.Net.WebClient",
            "System.Net.Sockets.Socket",
            "System.Net.Sockets.TcpClient",
            "System.Net.Sockets.UdpClient",
            "System.Net.WebSockets.ClientWebSocket");

    private static readonly DiagnosticDescriptor ProductNetworkClient = AnalyzerTypes.SourceRule(
        Category,
        "APCLI015",
        "Product code uses a network client",
        "Product code references '{0}'; network access for documents is owned by the SDK's "
            + "process-level network gate, and a product makes no request of its own");

    private static readonly DiagnosticDescriptor TemporaryPath = AnalyzerTypes.SourceRule(
        Category,
        "APCLI016",
        "Code outside UserStorage reads the temporary path",
        "'{0}' reads the temporary path through {1}; per-user state has one owner, so take a "
            + "directory from Aspose.Cli.Sdk.IO.Paths.UserStorage");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [ProductNetworkClient, TemporaryPath];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        bool product = context.Compilation.Assembly.GetAttributes()
            .Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString()
                    == SdkMetadataNames.ProductModuleAttribute);
        if (product)
        {
            context.RegisterSyntaxNodeAction(
                AnalyzeNetworkClient,
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName);
        }
        context.RegisterOperationAction(
            AnalyzeTemporaryPath,
            OperationKind.Invocation,
            OperationKind.MethodReference);
    }

    private static void AnalyzeNetworkClient(SyntaxNodeAnalysisContext context)
    {
        var name = (SimpleNameSyntax)context.Node;
        SemanticModel model = context.SemanticModel;
        ISymbol? symbol = model.GetSymbolInfo(name, context.CancellationToken).Symbol;
        ITypeSymbol? client =
            NetworkClient(symbol as ITypeSymbol)
            ?? NetworkClient(symbol?.ContainingType)
            ?? NetworkClient(model.GetTypeInfo(name, context.CancellationToken).Type);
        if (client is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ProductNetworkClient,
                name.GetLocation(),
                client.ToDisplayString()));
        }
    }

    private static ITypeSymbol? NetworkClient(ITypeSymbol? type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (NetworkClients.Contains(current.OriginalDefinition.ToDisplayString()))
            {
                return current;
            }
        }
        return null;
    }

    private static void AnalyzeTemporaryPath(OperationAnalysisContext context)
    {
        IMethodSymbol method = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation reference => reference.Method,
            _ => throw new InvalidOperationException(),
        };
        string? reader = TemporaryPathReader(method, context.Operation);
        if (reader is null || IsUserStorage(context.ContainingSymbol))
        {
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(
            TemporaryPath,
            context.Operation.Syntax.GetLocation(),
            context.ContainingSymbol.ToDisplayString(),
            reader));
    }

    // Path.GetTempPath, Path.GetTempFileName, or the TEMP or TMP variable read by name.
    private static string? TemporaryPathReader(IMethodSymbol method, IOperation operation)
    {
        string? type = method.ContainingType?.ToDisplayString();
        if (type == "System.IO.Path" && method.Name is "GetTempPath" or "GetTempFileName")
        {
            return "Path." + method.Name;
        }
        if (type == "System.Environment"
            && method.Name == "GetEnvironmentVariable"
            && operation is IInvocationOperation invocation
            && invocation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Ordinal == 0)?.Value.ConstantValue is { HasValue: true, Value: string name }
            && (string.Equals(name, "TEMP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "TMP", StringComparison.OrdinalIgnoreCase)))
        {
            return $"Environment.GetEnvironmentVariable(\"{name}\")";
        }
        return null;
    }

    private static bool IsUserStorage(ISymbol symbol)
    {
        for (INamedTypeSymbol? type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            type is not null;
            type = type.ContainingType)
        {
            if (type.ToDisplayString() == UserStorage)
            {
                return true;
            }
        }
        return false;
    }
}
