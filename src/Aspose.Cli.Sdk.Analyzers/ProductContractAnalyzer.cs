using System.Collections.Concurrent;
namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>Enforces the compile-time contract of an autonomous product.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProductContractAnalyzer : DiagnosticAnalyzer
{
    private const string ProductModuleAttribute =
        "Aspose.Cli.Sdk.Extensibility.ProductModuleAttribute";

    private static readonly ImmutableHashSet<string> HostAliases =
        ImmutableHashSet.Create(StringComparer.Ordinal, Aspose.Cli.Sdk.Extensibility.Commanding.GlobalOptionNames.Reserved);

    private static readonly ImmutableHashSet<string> TemplateAliases =
        ImmutableHashSet.Create(StringComparer.Ordinal, Aspose.Cli.Sdk.Extensibility.Commanding.StandardOptionNames.Reserved);

    private static readonly ImmutableHashSet<string> ProductLayers =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Contracts", "Ports", "Commands", "Engine", "Output");

    private static readonly ImmutableHashSet<string> ImplementationLayers =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Commands", "Engine", "Output");

    private static readonly ImmutableDictionary<string, ImmutableHashSet<string>>
        AllowedLayerDependencies =
            new Dictionary<string, ImmutableHashSet<string>>(StringComparer.Ordinal)
            {
                ["Contracts"] = ImmutableHashSet.Create(StringComparer.Ordinal, "Contracts"),
                ["Ports"] = ImmutableHashSet.Create(StringComparer.Ordinal, "Contracts", "Ports"),
                ["Commands"] = ImmutableHashSet.Create(
                    StringComparer.Ordinal,
                    "Contracts", "Ports", "Commands"),
                ["Engine"] = ImmutableHashSet.Create(
                    StringComparer.Ordinal,
                    "Contracts", "Ports", "Engine"),
                ["Output"] = ImmutableHashSet.Create(StringComparer.Ordinal, "Contracts", "Output"),
            }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly DiagnosticDescriptor ApiIsolation = Rule(
        "APCLI006",
        "Aspose SDK type crosses a product boundary",
        "Product symbol '{0}' exposes Aspose SDK type '{1}'; keep SDK types "
            + "inside the matching product engine");

    internal static readonly DiagnosticDescriptor DefinitionPurity = Rule(
        "APCLI007",
        "Product definition performs runtime work",
        "Product module Define() reaches '{0}' through {1}; definitions must "
            + "use only explicitly pure APIs and must not inspect files, "
            + "environment, time, randomness, processes, networks, threads, "
            + "services, dynamic assemblies, or initialize an Aspose SDK");

    private static readonly DiagnosticDescriptor OptionAlias = Rule(
        "APCLI008",
        "Product option alias is not allowed",
        "Product option alias '{0}' {1}");

    private static readonly DiagnosticDescriptor LayerDependency = Rule(
        "APCLI009",
        "Product layer dependency is not allowed",
        "Product layer '{0}' references '{1}' in layer '{2}'; keep the product "
            + "dependency direction Contracts <- Ports <- Commands and "
            + "Contracts/Ports <- Engine, with Output isolated");

    private static readonly DiagnosticDescriptor ImplementationVisibility = Rule(
        "APCLI010",
        "Product implementation type is publicly visible",
        "Implementation type '{0}' is public in product layer '{1}'; expose "
            + "wire contracts through Contracts, ports through Ports, and "
            + "composition through the product module only");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            ApiIsolation,
            DefinitionPurity,
            OptionAlias,
            LayerDependency,
            ImplementationVisibility,
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        AttributeData? export = context.Compilation.Assembly.GetAttributes()
            .FirstOrDefault(static attribute =>
                attribute.AttributeClass?.ToDisplayString()
                    == ProductModuleAttribute);
        if (export?.ConstructorArguments.FirstOrDefault().Value
            is not string)
        {
            return;
        }

        context.RegisterSymbolAction(
            AnalyzeApi,
            SymbolKind.NamedType,
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.Field,
            SymbolKind.Event);
        context.RegisterSymbolAction(
            AnalyzeLayerSymbol,
            SymbolKind.NamedType,
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.Field,
            SymbolKind.Event);

        var blocks =
            new ConcurrentDictionary<ISymbol, ImmutableArray<IOperation>>(
                SymbolEqualityComparer.Default);
        context.RegisterOperationBlockAction(operation =>
        {
            blocks.AddOrUpdate(
                operation.OwningSymbol,
                operation.OperationBlocks,
                (_, current) =>
                    current.AddRange(operation.OperationBlocks));
            AnalyzeLayerOperations(operation);
        });
        context.RegisterCompilationEndAction(end =>
            DefinitionPurityWalker.Analyze(end, blocks));

        context.RegisterOperationAction(
            AnalyzeCreation,
            OperationKind.ObjectCreation);
    }

    private static void AnalyzeApi(SymbolAnalysisContext context)
    {
        ISymbol symbol = context.Symbol;
        if (symbol.IsImplicitlyDeclared
            || !IsBoundary(symbol))
        {
            return;
        }

        ITypeSymbol? leaked = ExposedTypes(symbol)
            .Select(FindAsposeSdkType)
            .FirstOrDefault(static type => type is not null);
        if (leaked is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ApiIsolation,
                symbol.Locations.FirstOrDefault(static item =>
                    item.IsInSource) ?? Location.None,
                symbol.ToDisplayString(),
                leaked.ToDisplayString()));
        }
    }

    private static void AnalyzeCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (IsCommandLineOption(creation.Type))
        {
            foreach (IArgumentOperation argument in creation.Arguments
                .Where(static argument =>
                    IsAliasParameter(argument.Parameter?.Type)))
            {
                string[]? aliases = Constants(argument.Value);
                if (aliases is null)
                {
                    ReportAlias(
                        context,
                        argument,
                        "<non-constant>",
                        "must be a compile-time constant");
                    continue;
                }
                foreach (string alias in aliases)
                {
                    if (HostAliases.Contains(alias))
                    {
                        ReportAlias(context, argument, alias, "is reserved by the CLI host");
                    }
                    else if (TemplateAliases.Contains(alias))
                    {
                        ReportAlias(context, argument, alias, "is owned by the command template; declare it through CommandTraits");
                    }
                }
            }
        }

    }

    private static void AnalyzeLayerSymbol(SymbolAnalysisContext context)
    {
        ISymbol symbol = context.Symbol;
        if (symbol.IsImplicitlyDeclared
            || ProductLayer.TryCreate(symbol.ContainingNamespace) is not { } source)
        {
            return;
        }

        if (symbol is INamedTypeSymbol type
            && ImplementationLayers.Contains(source.Layer)
            && IsExternallyVisible(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ImplementationVisibility,
                SourceLocation(symbol),
                symbol.ToDisplayString(),
                source.Layer));
        }

        foreach (ITypeSymbol exposed in ExposedTypes(symbol))
        {
            ReportForbiddenLayer(
                context.ReportDiagnostic,
                SourceLocation(symbol),
                source,
                context.Compilation.Assembly,
                exposed);
        }
    }

    private static void AnalyzeLayerOperations(
        OperationBlockAnalysisContext context)
    {
        if (ProductLayer.TryCreate(context.OwningSymbol.ContainingNamespace)
            is not { } source)
        {
            return;
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (IOperation root in context.OperationBlocks)
        {
            foreach (IOperation operation in root.DescendantsAndSelf())
            {
                foreach (ITypeSymbol type in ReferencedTypes(operation).SelectMany(Flatten))
                {
                    ProductLayer? target = ProductLayer.TryCreate(type.ContainingNamespace);
                    if (target is null
                        || !IsRelevantTarget(type, context.Compilation.Assembly, target)
                        || !IsForbidden(source, target)
                        || !reported.Add(target.Root + ":" + target.Layer))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        LayerDependency,
                        operation.Syntax.GetLocation(),
                        source.Layer,
                        type.ToDisplayString(),
                        target.Layer));
                }
            }
        }
    }

    private static void ReportForbiddenLayer(
        Action<Diagnostic> report,
        Location location,
        ProductLayer source,
        IAssemblySymbol productAssembly,
        ITypeSymbol type)
    {
        foreach (ITypeSymbol referenced in Flatten(type))
        {
            ProductLayer? target = ProductLayer.TryCreate(referenced.ContainingNamespace);
            if (target is null
                || !IsRelevantTarget(referenced, productAssembly, target)
                || !IsForbidden(source, target))
            {
                continue;
            }

            report(Diagnostic.Create(
                LayerDependency,
                location,
                source.Layer,
                referenced.ToDisplayString(),
                target.Layer));
            return;
        }
    }

    private static IEnumerable<ITypeSymbol> ReferencedTypes(IOperation operation)
    {
        if (operation.Type is not null)
        {
            yield return operation.Type;
        }

        ITypeSymbol? declaringType = operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod.ContainingType,
            IObjectCreationOperation creation => creation.Constructor?.ContainingType,
            IFieldReferenceOperation field => field.Field.ContainingType,
            IPropertyReferenceOperation property => property.Property.ContainingType,
            IEventReferenceOperation eventReference => eventReference.Event.ContainingType,
            IMethodReferenceOperation method => method.Method.ContainingType,
            ITypeOfOperation typeOf => typeOf.TypeOperand,
            IIsTypeOperation isType => isType.TypeOperand,
            IVariableDeclaratorOperation variable => variable.Symbol.Type,
            IDeclarationPatternOperation pattern => pattern.MatchedType,
            _ => null,
        };
        if (declaringType is not null)
        {
            yield return declaringType;
        }
    }

    private static IEnumerable<ITypeSymbol> Flatten(ITypeSymbol type)
    {
        yield return type;
        if (type is IArrayTypeSymbol array)
        {
            foreach (ITypeSymbol nested in Flatten(array.ElementType))
            {
                yield return nested;
            }
        }
        else if (type is IPointerTypeSymbol pointer)
        {
            foreach (ITypeSymbol nested in Flatten(pointer.PointedAtType))
            {
                yield return nested;
            }
        }
        else if (type is INamedTypeSymbol named)
        {
            foreach (ITypeSymbol argument in named.TypeArguments)
            {
                foreach (ITypeSymbol nested in Flatten(argument))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool IsForbidden(ProductLayer source, ProductLayer target) =>
        !string.Equals(source.Root, target.Root, StringComparison.Ordinal)
        || !AllowedLayerDependencies[source.Layer].Contains(target.Layer);

    private static bool IsRelevantTarget(
        ITypeSymbol type,
        IAssemblySymbol productAssembly,
        ProductLayer target) =>
        SymbolEqualityComparer.Default.Equals(
            type.ContainingAssembly,
            productAssembly)
        || target.Root.StartsWith("Aspose.Cli.Product.", StringComparison.Ordinal);

    private static bool IsExternallyVisible(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type;
            current is not null;
            current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (
                Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }
        return true;
    }

    private static Location SourceLocation(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(static location => location.IsInSource)
        ?? Location.None;

    private static void ReportAlias(
        OperationAnalysisContext context,
        IArgumentOperation argument,
        string alias,
        string reason) =>
        context.ReportDiagnostic(Diagnostic.Create(
            OptionAlias,
            argument.Syntax.GetLocation(),
            alias,
            reason));

    internal static bool IsAsposeSdkType(ITypeSymbol type)
    {
        string assembly = type.ContainingAssembly?.Name ?? string.Empty;
        return assembly.StartsWith("Aspose.", StringComparison.Ordinal)
            && !assembly.StartsWith("Aspose.Cli.", StringComparison.Ordinal);
    }

    private static bool IsBoundary(ISymbol symbol)
    {
        bool visible = symbol.DeclaredAccessibility is
            Accessibility.Public
            or Accessibility.Protected
            or Accessibility.ProtectedOrInternal;
        for (INamedTypeSymbol? type = symbol.ContainingType;
            visible && type is not null;
            type = type.ContainingType)
        {
            visible = type.DeclaredAccessibility is
                Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal;
        }

        string name =
            symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return visible || name.Split('.')
            .Any(static part =>
                part is "Contracts" or "Ports" or "Commands");
    }

    private static IEnumerable<ITypeSymbol> ExposedTypes(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol type)
        {
            IEnumerable<ITypeSymbol> inherited = type.Interfaces
                .Concat(ConstraintTypes(type.TypeParameters));
            if (type.BaseType is not null)
            {
                inherited = inherited.Prepend(type.BaseType);
            }
            if (type.DelegateInvokeMethod is not { } invoke)
            {
                return inherited;
            }
            return inherited
                .Append(invoke.ReturnType)
                .Concat(invoke.Parameters.Select(static item => item.Type));
        }

        return symbol switch
        {
            IMethodSymbol method => method.Parameters
                .Select(static item => item.Type)
                .Prepend(method.ReturnType)
                .Concat(ConstraintTypes(method.TypeParameters)),
            IPropertySymbol property => [property.Type],
            IFieldSymbol field => [field.Type],
            IEventSymbol eventSymbol => [eventSymbol.Type],
            _ => [],
        };
    }

    private static IEnumerable<ITypeSymbol> ConstraintTypes(
        ImmutableArray<ITypeParameterSymbol> parameters) =>
        parameters.SelectMany(static parameter => parameter.ConstraintTypes);

    private static ITypeSymbol? FindAsposeSdkType(ITypeSymbol type)
    {
        if (IsAsposeSdkType(type))
        {
            return type;
        }
        if (type is IArrayTypeSymbol array)
        {
            return FindAsposeSdkType(array.ElementType);
        }
        if (type is IPointerTypeSymbol pointer)
        {
            return FindAsposeSdkType(pointer.PointedAtType);
        }
        return (type as INamedTypeSymbol)?.TypeArguments
            .Select(FindAsposeSdkType)
            .FirstOrDefault(static item => item is not null);
    }

    private static bool IsCommandLineOption(ITypeSymbol? type)
    {
        for (INamedTypeSymbol? current = type as INamedTypeSymbol;
            current is not null;
            current = current.BaseType)
        {
            if (current.Name == "Option"
                && current.ContainingNamespace.ToDisplayString()
                    == "System.CommandLine")
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAliasParameter(ITypeSymbol? type) =>
        type?.SpecialType == SpecialType.System_String
        || type is IArrayTypeSymbol
        {
            ElementType.SpecialType: SpecialType.System_String,
        };

    private static string[]? Constants(IOperation operation)
    {
        if (operation is IConversionOperation conversion)
        {
            return Constants(conversion.Operand);
        }
        if (operation.ConstantValue is
            {
                HasValue: true,
                Value: string value,
            })
        {
            return [value];
        }
        if (operation is not IArrayCreationOperation
            { Initializer.ElementValues: var elements })
        {
            return null;
        }

        var result = new List<string>();
        foreach (IOperation element in elements)
        {
            string[]? nested = Constants(element);
            if (nested is null)
            {
                return null;
            }
            result.AddRange(nested);
        }
        return result.ToArray();
    }

    private static DiagnosticDescriptor Rule(
        string id,
        string title,
        string message) =>
        new(
            id,
            title,
            message,
            "Aspose.Cli.ProductIsolation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    private sealed class ProductLayer
    {
        private ProductLayer(string root, string layer)
        {
            Root = root;
            Layer = layer;
        }

        public string Root { get; }

        public string Layer { get; }

        public static ProductLayer? TryCreate(INamespaceSymbol? productNamespace)
        {
            string name = productNamespace?.ToDisplayString() ?? string.Empty;
            string[] segments = name.Split('.');
            for (int index = 1; index < segments.Length; index++)
            {
                if (ProductLayers.Contains(segments[index]))
                {
                    return new ProductLayer(
                        string.Join(".", segments.Take(index)),
                        segments[index]);
                }
            }
            return null;
        }
    }
}
