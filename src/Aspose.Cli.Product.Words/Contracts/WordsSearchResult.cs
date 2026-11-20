using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words search</c>: the matches in the text rows of the document's objects.</summary>
public sealed record WordsSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The hits in document order: the object's address, the text row, and where the match is in it; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableHit> Hits { get; init; }

    /// <summary>The warnings Aspose.Words reported while it loaded the document, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
