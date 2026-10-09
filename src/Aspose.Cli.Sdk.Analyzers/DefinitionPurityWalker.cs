namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>Walks one definition's local call graph and pure external APIs.</summary>
internal sealed class DefinitionPurityWalker : OperationWalker
{
    private const string ModuleInterface =
        "Aspose.Cli.Sdk.Extensibility.IProductModule";
    private const string DefinitionBuilder =
        "Aspose.Cli.Sdk.Extensibility.ProductDefinitionBuilder<TSession>";

    private static readonly ImmutableHashSet<string> RuntimeTypes =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "System.Console", "System.Environment", "System.Random",
            "System.IO.Directory", "System.IO.DirectoryInfo",
            "System.IO.File", "System.IO.FileInfo", "System.IO.FileStream",
            "System.IO.FileSystemWatcher", "System.Diagnostics.Process",
            "System.Runtime.InteropServices.NativeLibrary");

    private readonly Compilation _compilation;
    private readonly IReadOnlyDictionary<
        ISymbol,
        ImmutableArray<IOperation>> _blocks;
    private readonly System.Threading.CancellationToken _cancellation;
    private readonly Action<Diagnostic> _report;
    private readonly HashSet<ISymbol> _visited =
        new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _initialized =
        new(SymbolEqualityComparer.Default);
    private readonly HashSet<(SyntaxTree? Tree, int Start)> _reported = [];
    private readonly List<string> _path = [];

    private DefinitionPurityWalker(
        Compilation compilation,
        IReadOnlyDictionary<ISymbol, ImmutableArray<IOperation>> blocks,
        System.Threading.CancellationToken cancellation,
        Action<Diagnostic> report)
    {
        _compilation = compilation;
        _blocks = blocks;
        _cancellation = cancellation;
        _report = report;
    }

    internal static void Analyze(
        CompilationAnalysisContext context,
        IReadOnlyDictionary<ISymbol, ImmutableArray<IOperation>> blocks)
    {
        foreach (INamedTypeSymbol type in AnalyzerTypes.Declared(
            context.Compilation))
        {
            if (!type.AllInterfaces.Any(static contract =>
                    contract.ToDisplayString() == ModuleInterface))
            {
                continue;
            }

            foreach (IMethodSymbol root in type.GetMembers("Define")
                .OfType<IMethodSymbol>()
                .Where(static method =>
                    method.MethodKind == MethodKind.Ordinary
                    && method.Parameters.Length == 0))
            {
                new DefinitionPurityWalker(
                    context.Compilation,
                    blocks,
                    context.CancellationToken,
                    context.ReportDiagnostic)
                    .Walk(root);
            }
        }
    }

    private void Walk(IMethodSymbol root)
    {
        _visited.Add(Original(root));
        _path.Add(Display(root));
        Initialize(root.ContainingType, staticMembers: true);
        Initialize(root.ContainingType, staticMembers: false);
        foreach (IMethodSymbol constructor
            in root.ContainingType.InstanceConstructors
                .Where(static item => item.Parameters.Length == 0))
        {
            Follow(constructor);
        }
        VisitBlocks(root);
        _path.Clear();
    }

    public override void VisitInvocation(IInvocationOperation operation)
    {
        Check(operation, operation.TargetMethod);
        Visit(operation.Instance);
        VisitArguments(operation.Arguments, operation.TargetMethod);
        Initialize(operation.TargetMethod.ContainingType, staticMembers: true);
        Follow(operation.TargetMethod);
    }

    public override void VisitObjectCreation(
        IObjectCreationOperation operation)
    {
        Check(operation, operation.Constructor);
        VisitArguments(operation.Arguments);
        if (operation.Type is INamedTypeSymbol type)
        {
            Initialize(type, staticMembers: true);
            Initialize(type, staticMembers: false);
        }
        Follow(operation.Constructor);
    }

    public override void VisitPropertyReference(
        IPropertyReferenceOperation operation)
    {
        Check(operation, operation.Property);
        Visit(operation.Instance);
        VisitArguments(operation.Arguments);
        Initialize(operation.Property.ContainingType, staticMembers: true);
        Follow(operation.Property.GetMethod);
        Follow(operation.Property);
    }

    public override void VisitFieldReference(
        IFieldReferenceOperation operation)
    {
        Check(operation, operation.Field);
        Visit(operation.Instance);
        Initialize(operation.Field.ContainingType, staticMembers: true);
        Follow(operation.Field);
    }

    private void VisitArguments(
        ImmutableArray<IArgumentOperation> arguments,
        IMethodSymbol? deferredBy = null)
    {
        foreach (IArgumentOperation argument in arguments)
        {
            if (deferredBy is null || !IsDeferred(deferredBy, argument))
            {
                Visit(argument.Value);
            }
        }
    }

    private void Follow(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return;
        }
        symbol = Original(symbol);
        if (!IsLocal(symbol) || !_visited.Add(symbol))
        {
            return;
        }

        _path.Add(Display(symbol));
        VisitBlocks(symbol);
        _path.RemoveAt(_path.Count - 1);
    }

    private void VisitBlocks(ISymbol symbol)
    {
        _cancellation.ThrowIfCancellationRequested();
        symbol = Original(symbol);
        if (_blocks.TryGetValue(symbol, out ImmutableArray<IOperation> blocks))
        {
            foreach (IOperation operation in blocks)
            {
                Visit(operation);
            }
        }
    }

    private void Initialize(
        INamedTypeSymbol? type,
        bool staticMembers)
    {
        if (type is null
            || !IsLocal(type)
            || staticMembers && !_initialized.Add(type))
        {
            return;
        }

        foreach (ISymbol member in type.GetMembers().Where(static member =>
            member is IFieldSymbol or IPropertySymbol))
        {
            if (member.IsStatic == staticMembers
                && !member.IsImplicitlyDeclared)
            {
                Follow(member);
            }
        }
        if (staticMembers)
        {
            foreach (IMethodSymbol constructor in type.StaticConstructors)
            {
                Follow(constructor);
            }
        }
    }

    private void Check(IOperation operation, ISymbol? symbol)
    {
        if (symbol is null
            || IsLocal(symbol)
            || IsPureExternal(symbol)
            || !_reported.Add((
                operation.Syntax.SyntaxTree,
                operation.Syntax.SpanStart)))
        {
            return;
        }

        string accessed = Display(symbol);
        _report(Diagnostic.Create(
            ProductContractAnalyzer.DefinitionPurity,
            operation.Syntax.GetLocation(),
            accessed,
            string.Join(" -> ", _path.Concat([accessed]))));
    }

    private bool IsLocal(ISymbol symbol) =>
        SymbolEqualityComparer.Default.Equals(
            symbol.ContainingAssembly,
            _compilation.Assembly);

    private static bool IsPureExternal(ISymbol symbol)
    {
        INamedTypeSymbol? type = symbol.ContainingType;
        if (type is null
            || IsRuntime(symbol, type)
            || ProductContractAnalyzer.IsAsposeSdkType(type))
        {
            return false;
        }
        if (IsIntrinsic(type)
            || IsExceptionConstructor(symbol, type))
        {
            return true;
        }

        string typeName = type.OriginalDefinition.ToDisplayString();
        if (type.ContainingAssembly?.Name == "Aspose.Cli.Sdk")
        {
            return IsPureSdk(symbol, typeName);
        }
        if (typeName == "System.Type"
            && symbol is IPropertySymbol { Name: "Assembly" })
        {
            return true;
        }

        string ns = type.ContainingNamespace.ToDisplayString();
        return ns.StartsWith("System.Collections", StringComparison.Ordinal)
            || typeName == "System.Linq.Enumerable"
            || typeName is
                "System.Array"
                or "System.Convert"
                or "System.Math"
                or "System.StringComparer";
    }

    private static bool IsIntrinsic(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Enum
        || type.SpecialType != SpecialType.None
        || type.OriginalDefinition.SpecialType
            == SpecialType.System_Nullable_T;

    private static bool IsExceptionConstructor(
        ISymbol symbol,
        INamedTypeSymbol type)
        => symbol is IMethodSymbol
            {
                MethodKind: MethodKind.Constructor,
            }
        && AnalyzerTypes.Inherits(type, "System.Exception");

    private static bool IsPureSdk(
        ISymbol symbol,
        string typeName)
    {
        string member = symbol.Name;
        if (typeName.StartsWith(
                "Aspose.Cli.Sdk.Extensibility.ProductDefinitionBuilder<",
                StringComparison.Ordinal))
        {
            return member is
                "Activator" or "Build" or "Describe" or "DetectFormat"
                or "Diagnostics" or "Formats" or "Guard" or "Json" or "View";
        }
        if (typeName.StartsWith(
                "Aspose.Cli.Sdk.Extensibility.Commanding.CommandMenu<",
                StringComparison.Ordinal)
            || typeName == "Aspose.Cli.Sdk.Extensibility.Commanding.ProductMenu")
        {
            return member is "Command" or "Group";
        }
        return typeName switch
        {
            "Aspose.Cli.Sdk.Extensibility.Product" => member == "Define",
            "Aspose.Cli.Sdk.IO.FormatDescriptor" =>
                member is ".ctor" or "Declare" or "Input" or "Output" or "Render"
                    or "Routed"
                    || IsDataMember(symbol),
            "Aspose.Cli.Sdk.IO.FormatDescriptorExtensions" =>
                member is "IdsFor" or "ExtensionFor",
            "Aspose.Cli.Sdk.IO.FileFormatRecognition" =>
                member is "AttachTo" or "Match" or "FirstOf",
            "Aspose.Cli.Sdk.IO.FileProbePattern" => true,
            "Aspose.Cli.Sdk.Contracts.ResourceBudgetCapabilities" =>
                member == "Domain",
            "Aspose.Cli.Sdk.Errors.ErrorCode" =>
                member == "NotFound" || IsDataMember(symbol),
            "Aspose.Cli.Sdk.Diagnostics.DiagnosticDescriptor" =>
                member is "Error" or "Warning" or "Verification" || IsDataMember(symbol),
            "Aspose.Cli.Sdk.Views.ViewPartKinds" =>
                symbol is IFieldSymbol { IsConst: true },
            _ => IsDefinitionValue(typeName) && IsDataMember(symbol),
        };
    }

    private static bool IsRuntime(ISymbol symbol, INamedTypeSymbol type)
    {
        string typeName = type.ToDisplayString();
        string member = symbol.Name;
        bool known = typeName switch
        {
            "System.DateTime" or "System.DateTimeOffset" =>
                member is "Now" or "UtcNow" or "Today",
            "System.Guid" => member == "NewGuid",
            "System.Diagnostics.Stopwatch" =>
                member is "GetTimestamp" or "StartNew",
            "System.Reflection.Assembly" => symbol is IMethodSymbol,
            "System.Globalization.CultureInfo" => member is
                "CurrentCulture"
                or "CurrentUICulture"
                or "InstalledUICulture",
            "System.IServiceProvider" => member == "GetService",
            _ => false,
        };
        if (known)
        {
            return true;
        }

        string ns = type.ContainingNamespace.ToDisplayString();
        return RuntimeTypes.Contains(typeName)
            || ns.StartsWith("System.Threading", StringComparison.Ordinal)
            || ns.StartsWith("System.Net", StringComparison.Ordinal)
            || ns.StartsWith(
                "System.Security.Cryptography",
                StringComparison.Ordinal)
                && type.Name.IndexOf("Random", StringComparison.Ordinal) >= 0
            || ns.StartsWith(
                "Microsoft.Extensions.DependencyInjection",
                StringComparison.Ordinal)
                && (type.Name.IndexOf(
                        "ServiceProvider",
                        StringComparison.Ordinal) >= 0
                    || type.Name == "ActivatorUtilities");
    }

    // A deferred argument runs when a command or a view runs, never in Define(): the activator,
    // the product guard and the product help. Menu lines are not deferred: they take method
    // groups, which the walker does not follow, and a group's lambda runs in Define() and is checked.
    private static bool IsDeferred(
        IMethodSymbol method,
        IArgumentOperation argument)
        => method.ContainingType.OriginalDefinition.ToDisplayString()
            == DefinitionBuilder
        && (argument.Parameter?.Ordinal, method.Name) is
            (0, "Activator" or "Guard")
            or (1, "Describe");

    private static bool IsDefinitionValue(string typeName) =>
        typeName is
            "Aspose.Cli.Sdk.Extensibility.ProductManifest"
            or "Aspose.Cli.Sdk.IO.RouteOwnership"
            or "Aspose.Cli.Sdk.Views.ProductView"
            or "Aspose.Cli.Sdk.Serialization.ProductJsonDefinition";

    private static bool IsDataMember(ISymbol symbol) =>
        symbol is IMethodSymbol
        {
            MethodKind: MethodKind.Constructor
                or MethodKind.PropertyGet
                or MethodKind.PropertySet,
        }
        || symbol is IPropertySymbol or IFieldSymbol;

    private static string Display(ISymbol symbol) =>
        symbol.ToDisplayString(
            SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static ISymbol Original(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method =>
                method.ReducedFrom?.OriginalDefinition
                ?? method.OriginalDefinition,
            IPropertySymbol property => property.OriginalDefinition,
            IFieldSymbol field => field.OriginalDefinition,
            _ => symbol,
        };
}
