namespace Aspose.Cli.Sdk.Analyzers;

internal static class AnalyzerTypes
{
    /// <summary>An error-severity rule, enabled by default.</summary>
    internal static DiagnosticDescriptor Rule(
        string category,
        string id,
        string title,
        string message) =>
        new(
            id,
            title,
            message,
            category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    /// <summary>
    /// An error-severity rule for this repository's production code only: disabled by default and
    /// enabled by the <c>[src/**.cs]</c> section of <c>.editorconfig</c>.
    /// </summary>
    internal static DiagnosticDescriptor SourceRule(
        string category,
        string id,
        string title,
        string message) =>
        new(
            id,
            title,
            message,
            category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: false);

    internal static IEnumerable<INamedTypeSymbol> Declared(
        Compilation compilation) =>
        compilation.GetSymbolsWithName(
                static _ => true,
                SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(type => SymbolEqualityComparer.Default.Equals(
                type.ContainingAssembly,
                compilation.Assembly));

    /// <summary>
    /// The name <see cref="Compilation.GetTypeByMetadataName"/> takes for a type, as
    /// <see cref="SdkMetadataNames"/> lists it: generic arity as <c>`1</c>, nested types after <c>+</c>.
    /// </summary>
    internal static string MetadataName(INamedTypeSymbol type) =>
        type.ContainingType is { } outer
            ? MetadataName(outer) + "+" + type.MetadataName
            : type.ContainingNamespace is { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString() + "." + type.MetadataName
                : type.MetadataName;

    internal static bool Inherits(
        INamedTypeSymbol type,
        string baseType)
    {
        for (INamedTypeSymbol? current = type.BaseType;
            current is not null;
            current = current.BaseType)
        {
            if (current.ToDisplayString() == baseType)
            {
                return true;
            }
        }
        return false;
    }
}
