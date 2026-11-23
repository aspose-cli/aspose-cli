namespace Aspose.Cli.Sdk.Text;

/// <summary>One validated search, as an engine receives it.</summary>
/// <param name="Text">The validated pattern, ready to match.</param>
/// <param name="MaxHits">The returned-hit budget.</param>
/// <param name="Skip">Matches to pass over before the returned hits start.</param>
public sealed record SearchQuery(TextSearch Text, int MaxHits, int Skip = 0)
{
    /// <summary>The option that holds <see cref="Skip"/>, which a truncated search's <c>window.next</c> names.</summary>
    internal const string SkipOption = "--skip";

    /// <summary>Starts collecting the hits this query returns.</summary>
    public SearchHits<THit> Collect<THit>() => new(Skip, MaxHits);
}
