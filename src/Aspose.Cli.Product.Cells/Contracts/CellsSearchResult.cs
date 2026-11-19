using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>cells search</c>: the matches in the text rows of the workbook's objects.</summary>
public sealed record CellsSearchResult() : WindowedResultEnvelope("search-result")
{
    /// <summary>The searched workbook.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The hits in document order: the object's address, the text row, and where the match is in it; last, since it grows with the workbook.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableHit> Hits { get; init; }

    /// <summary>The warnings Aspose.Cells reported while it loaded the workbook, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
