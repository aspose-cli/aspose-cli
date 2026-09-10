namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>Verifies product-owned runtime JSON metadata completeness.</summary>
[Generator]
public sealed class ProductJsonContextGenerator : IIncrementalGenerator
{
    private const string ResultEnvelope =
        "Aspose.Cli.Sdk.Contracts.ResultEnvelope";
    private const string RootAttribute =
        "Aspose.Cli.Sdk.Serialization.ProductJsonRootAttribute";
    private const string SerializableAttribute =
        "System.Text.Json.Serialization.JsonSerializableAttribute";
    private const string SerializerContext =
        "System.Text.Json.Serialization.JsonSerializerContext";

    private static readonly DiagnosticDescriptor MissingMetadata = new(
        "APCLI003",
        "Product JSON root is missing source-generated metadata",
        "JSON root '{0}' is missing from the product JsonSerializerContext",
        "Aspose.Cli.ProductSerialization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "Every product result and deserialization root must be declared "
            + "with JsonSerializableAttribute on a product-owned context.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var missing = context.CompilationProvider.Select(
            static (compilation, _) => FindMissingMetadata(compilation));
        context.RegisterSourceOutput(
            missing,
            static (production, roots) =>
            {
                foreach (INamedTypeSymbol root in roots)
                {
                    production.ReportDiagnostic(Diagnostic.Create(
                        MissingMetadata,
                        root.Locations[0],
                        root.ToDisplayString()));
                }
            });
    }

    private static ImmutableArray<INamedTypeSymbol> FindMissingMetadata(
            Compilation compilation)
    {
        INamedTypeSymbol[] types =
            AnalyzerTypes.Declared(compilation).ToArray();
        ImmutableArray<ITypeSymbol> metadata = types
            .Where(static type => DerivesFrom(type, SerializerContext))
            .SelectMany(static type => type.GetAttributes())
            .Where(static attribute =>
                attribute.AttributeClass?.ToDisplayString()
                    == SerializableAttribute
                && attribute.ConstructorArguments.Length > 0)
            .Select(static attribute =>
                attribute.ConstructorArguments[0].Value)
            .OfType<ITypeSymbol>()
            .ToImmutableArray();
        return types.Where(IsRoot)
            .Where(root => !metadata.Any(type =>
                SymbolEqualityComparer.Default.Equals(type, root)))
            .OrderBy(static type => type.ToDisplayString(), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool IsRoot(INamedTypeSymbol type) =>
        !type.IsAbstract
        && type.TypeKind is TypeKind.Class or TypeKind.Struct
        && (DerivesFrom(type, ResultEnvelope)
            || HasAttribute(type, RootAttribute));

    private static bool DerivesFrom(
        INamedTypeSymbol type,
        string baseType) =>
        AnalyzerTypes.Inherits(type, baseType);

    private static bool HasAttribute(
        INamedTypeSymbol type,
        string attribute) =>
        type.GetAttributes().Any(item =>
            item.AttributeClass?.ToDisplayString() == attribute);
}
