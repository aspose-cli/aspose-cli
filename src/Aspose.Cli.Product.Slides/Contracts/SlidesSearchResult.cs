using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides search</c>: the matches in the text rows of the presentation's objects.</summary>
public sealed record SlidesSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The hits in document order: the object's address, the text row, and where the match is in it; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableHit> Hits { get; init; }

    /// <summary>The warnings Aspose.Slides reported while it loaded the presentation, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
