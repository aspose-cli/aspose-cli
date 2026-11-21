using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// Describes the result records of the compilation (see <see cref="ResultContractWriter"/>). It
/// only reads facts from the records; schema writing lives in the SDK.
/// </summary>
[Generator]
public sealed class ResultContractGenerator : IIncrementalGenerator
{
    internal static readonly DiagnosticDescriptor InvalidResult = new(
        "APCLI013",
        "Result contract is invalid",
        "{0}",
        "Aspose.Cli.ResultContracts",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "Result records must describe their published schema completely: every serialized member "
            + "has a documentation summary and a supported type, a published id is relative and used "
            + "once, and every constraint fits its member.");

    internal static readonly DiagnosticDescriptor MissingSdkType = new(
        "APCLI014",
        "SDK type is missing",
        "The contract generator cannot resolve the SDK type '{0}', so it describes no contract; reference Aspose.Cli.Sdk, or update SdkMetadataNames if the SDK renamed the type",
        "Aspose.Cli.SdkMetadata",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The contract generator recognizes SDK types by their metadata names; a contract whose "
            + "compilation cannot resolve one of them is reported rather than described without the "
            + "rules that type carries.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<(IPropertySymbol Property, Location Location)>> constantLists = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SdkMetadataNames.AllowedValuesAttribute,
                static (node, _) => node is PropertyDeclarationSyntax,
                static (attributed, _) => attributed.Attributes
                    .Where(static attribute => attribute.ConstructorArguments.FirstOrDefault().Kind == TypedConstantKind.Type)
                    .Select(attribute => ((IPropertySymbol)attributed.TargetSymbol, ContractTypes.AttributeLocation(attribute, attributed.TargetSymbol)))
                    .ToImmutableArray())
            .SelectMany(static (found, _) => found)
            .Collect();
        context.RegisterSourceOutput(
            constantLists.Combine(context.CompilationProvider),
            static (production, input) => Emit(production, input.Left, input.Right));
    }

    private static void Emit(
        SourceProductionContext production,
        ImmutableArray<(IPropertySymbol Property, Location Location)> constantLists,
        Compilation compilation)
    {
        var reported = new HashSet<string>(StringComparer.Ordinal);
        void ReportResult(Location location, string message)
        {
            if (reported.Add(location + message))
            {
                production.ReportDiagnostic(Diagnostic.Create(InvalidResult, location, message));
            }
        }

        var symbols = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (string name in SdkMetadataNames.ContractSymbols)
        {
            if (compilation.GetTypeByMetadataName(name) is { } symbol)
            {
                symbols.Add(name, symbol);
            }
            else
            {
                missing.Add(name);
            }
        }

        bool Resolved(ISymbol owner)
        {
            foreach (string name in missing)
            {
                production.ReportDiagnostic(Diagnostic.Create(MissingSdkType, ContractTypes.SourceLocation(owner), name));
            }

            return missing.Count == 0;
        }

        var results = new ResultContractWriter(compilation, symbols, Resolved, ReportResult);
        foreach ((string hintName, string source) in results.Write())
        {
            production.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }

        var described = new HashSet<INamedTypeSymbol>(results.Records, SymbolEqualityComparer.Default);
        foreach ((IPropertySymbol property, Location location) in missing.Count == 0 ? constantLists : [])
        {
            if (!described.Contains(property.ContainingType))
            {
                ReportResult(location, $"'{property.ContainingType.Name}.{property.Name}': [AllowedValues(typeof(...))] is expanded only on members of result contract records.");
            }
        }
    }
}
