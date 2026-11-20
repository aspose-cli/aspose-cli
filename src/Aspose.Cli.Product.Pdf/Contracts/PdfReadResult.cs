using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// Result of <c>pdf read</c>: a window of the document's objects in document order, each with
/// every row of the table it has, and the whole document's inventory.
/// </summary>
public sealed record PdfReadResult() : WindowedResultEnvelope("read-result")
{
    /// <summary>The document read.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>The objects of the window, in document order, each with its address, kind and rows; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableObject> Objects { get; init; }

    /// <summary>How many objects of each kind of content the document holds: under read the kinds read lists, under notRead the others.</summary>
    [JsonPropertyOrder(850)]
    public required TableInventory Inventory { get; init; }

    /// <summary>The warnings Aspose.PDF reported while it loaded the document, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
