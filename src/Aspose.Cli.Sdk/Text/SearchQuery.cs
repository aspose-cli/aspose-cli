namespace Aspose.Cli.Sdk.Text;

/// <summary>One validated search, as an engine receives it.</summary>
/// <param name="Text">The validated pattern, ready to match.</param>
/// <param name="MaxHits">The returned-hit budget.</param>
/// <param name="Scope">The selected product scope, or null when the product has none.</param>
/// <param name="Skip">Matches to pass over before the returned hits start.</param>
public sealed record SearchQuery(TextSearch Text, int MaxHits, string? Scope, int Skip = 0)
{
    /// <summary>Starts collecting the hits this query returns.</summary>
    public SearchHits<THit> Collect<THit>() => new(Skip, MaxHits);
}
