using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf search</c>: the matches the engine finds in the text of the document's pages.</summary>
public sealed record PdfSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The hits in page order: the address of the page's text, the row, the matched text and the engine's rectangle of it; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableHit> Hits { get; init; }

    /// <summary>The warnings Aspose.PDF reported while it loaded the document, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
