using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// Extracts the operation contracts of every <c>[OperationVocabulary]</c> base record into
/// descriptors and generates the vocabulary's catalog, handler interface and dispatch. It only
/// reads facts from the records; validation, schema writing and default filling live in the SDK.
/// </summary>
[Generator]
public sealed class OperationContractGenerator : IIncrementalGenerator
{
    internal const string Operations = "Aspose.Cli.Sdk.Operations.";

    internal static readonly DiagnosticDescriptor InvalidContract = new(
        "APCLI012",
        "Operation contract is invalid",
        "{0}",
        "Aspose.Cli.OperationContracts",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "Operation records must describe their wire contract completely: every operation is "
            + "listed in its vocabulary's camelCase JSON context, and every member has a supported "
            + "type, a distinct wire name and a declared requirement or default.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> vocabularies = Records(context, "OperationVocabularyAttribute");
        IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> operations = Records(context, "OperationAttribute");
        IncrementalValueProvider<ImmutableArray<(IPropertySymbol Property, Location Location)>> constantLists = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Operations + "AllowedValuesAttribute",
                static (node, _) => node is PropertyDeclarationSyntax,
                static (attributed, _) => attributed.Attributes
                    .Where(static attribute => attribute.ConstructorArguments.FirstOrDefault().Kind == TypedConstantKind.Type)
                    .Select(attribute => ((IPropertySymbol)attributed.TargetSymbol, VocabularyWriter.AttributeLocation(attribute, attributed.TargetSymbol)))
                    .ToImmutableArray())
            .SelectMany(static (found, _) => found)
            .Collect();
        context.RegisterSourceOutput(
            vocabularies.Combine(operations).Combine(constantLists).Combine(context.CompilationProvider),
            static (production, input) => Emit(production, input.Left.Left.Left, input.Left.Left.Right, input.Left.Right, input.Right));
    }

    private static IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> Records(IncrementalGeneratorInitializationContext context, string attribute) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Operations + attribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributed, _) => (INamedTypeSymbol)attributed.TargetSymbol)
            .Collect();

    private static void Emit(
        SourceProductionContext production,
        ImmutableArray<INamedTypeSymbol> declaredVocabularies,
        ImmutableArray<INamedTypeSymbol> declaredOperations,
        ImmutableArray<(IPropertySymbol Property, Location Location)> constantLists,
        Compilation compilation)
    {
        var reported = new HashSet<string>(StringComparer.Ordinal);
        void Report(Location location, string message)
        {
            if (reported.Add(location + message))
            {
                production.ReportDiagnostic(Diagnostic.Create(InvalidContract, location, message));
            }
        }

        var vocabularies = new Dictionary<INamedTypeSymbol, List<(string Name, INamedTypeSymbol Type)>>(SymbolEqualityComparer.Default);
        foreach (INamedTypeSymbol vocabulary in declaredVocabularies.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
        {
            vocabularies.Add(vocabulary, []);
        }

        foreach (INamedTypeSymbol operation in declaredOperations.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
        {
            INamedTypeSymbol? vocabulary = VocabularyWriter.BaseTypes(operation).FirstOrDefault(vocabularies.ContainsKey);
            if (vocabulary is null || operation.IsAbstract || !operation.IsRecord
                || VocabularyWriter.Find(operation, Operations + "OperationAttribute")?.ConstructorArguments.FirstOrDefault().Value
                    is not string { Length: > 0 } name)
            {
                Report(
                    VocabularyWriter.SourceLocation(operation),
                    $"Operation '{operation.Name}' must be a non-abstract record with a wire name that derives from an [OperationVocabulary] base record.");
                continue;
            }

            vocabularies[vocabulary].Add((name, operation));
        }

        var described = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (KeyValuePair<INamedTypeSymbol, List<(string Name, INamedTypeSymbol Type)>> vocabulary in vocabularies)
        {
            var writer = new VocabularyWriter(compilation, vocabulary.Key, vocabulary.Value, Report);
            if (writer.Write() is { } source)
            {
                production.AddSource(vocabulary.Key.Name + ".Operations.g.cs", SourceText.From(source, Encoding.UTF8));
            }

            described.UnionWith(writer.Records);
        }

        foreach ((IPropertySymbol property, Location location) in constantLists)
        {
            if (!described.Contains(property.ContainingType))
            {
                Report(location, $"'{property.ContainingType.Name}.{property.Name}': [AllowedValues(typeof(...))] is expanded only on members of operation contract records.");
            }
        }
    }
}
