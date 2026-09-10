namespace Aspose.Cli.Sdk.Analyzers;

internal static class AnalyzerTypes
{
    internal static IEnumerable<INamedTypeSymbol> Declared(
        Compilation compilation) =>
        compilation.GetSymbolsWithName(
                static _ => true,
                SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(type => SymbolEqualityComparer.Default.Equals(
                type.ContainingAssembly,
                compilation.Assembly));

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
