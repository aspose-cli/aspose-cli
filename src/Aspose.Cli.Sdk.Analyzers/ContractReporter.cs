using static Aspose.Cli.Sdk.Analyzers.ContractTypes;

namespace Aspose.Cli.Sdk.Analyzers;

/// <summary>
/// Reports the contract errors of one generator pass and remembers that it failed, so the pass
/// writes no source for contracts that have errors.
/// </summary>
internal abstract class ContractReporter(Action<Location, string> report)
{
    /// <summary>Whether any contract error was reported.</summary>
    protected bool Failed { get; private set; }

    /// <summary>Reports <paramref name="reason"/> against <paramref name="property"/> and returns no code for it.</summary>
    protected string Invalid(IPropertySymbol property, string reason)
    {
        Report(property, $"'{property.ContainingType.Name}.{property.Name}': {reason}.");
        return string.Empty;
    }

    protected void Report(ISymbol symbol, string message) => Report(SourceLocation(symbol), message);

    protected void Report(Location location, string message)
    {
        Failed = true;
        report(location, message);
    }
}
