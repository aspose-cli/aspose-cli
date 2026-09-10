namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The read projection scope. Scopes are additive supersets of
/// <see cref="Values"/> rather than free combinations — a deliberate
/// simplification that keeps the surface predictable.
/// </summary>
public enum ReadScope
{
    /// <summary>Cell values and types.</summary>
    Values,

    /// <summary>Values plus formulas.</summary>
    Formulas,

    /// <summary>Values plus style references and the style pool.</summary>
    Styles,

    /// <summary>Values, formulas and styles.</summary>
    Full,
}

/// <summary>Mapping between <see cref="ReadScope"/> and its wire names.</summary>
public static class ReadScopeExtensions
{
    /// <summary>The contract string of a scope (see <c>ReadScopes</c>).</summary>
    public static string ToContractName(this ReadScope scope) => scope switch
    {
        ReadScope.Values => ReadScopes.Values,
        ReadScope.Formulas => ReadScopes.Formulas,
        ReadScope.Styles => ReadScopes.Styles,
        ReadScope.Full => ReadScopes.Full,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };

    /// <summary>Parses a wire name; returns false for unknown values.</summary>
    public static bool TryParse(string? text, out ReadScope scope)
    {
        switch (text)
        {
            case ReadScopes.Values:
                scope = ReadScope.Values;
                return true;
            case ReadScopes.Formulas:
                scope = ReadScope.Formulas;
                return true;
            case ReadScopes.Styles:
                scope = ReadScope.Styles;
                return true;
            case ReadScopes.Full:
                scope = ReadScope.Full;
                return true;
            default:
                scope = ReadScope.Values;
                return false;
        }
    }

    /// <summary><c>true</c> when the scope carries formulas.</summary>
    public static bool IncludesFormulas(this ReadScope scope) =>
        scope is ReadScope.Formulas or ReadScope.Full;

    /// <summary><c>true</c> when the scope carries styles.</summary>
    public static bool IncludesStyles(this ReadScope scope) =>
        scope is ReadScope.Styles or ReadScope.Full;
}
