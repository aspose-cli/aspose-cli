using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>cells read</c>: a window of the workbook's objects in document order, each with
/// every row of the table it has, and the whole workbook's inventory.
/// </summary>
public sealed record CellsReadResult() : WindowedResultEnvelope("read-result")
{
    /// <summary>The workbook read.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>The objects of the window, in document order, each with its address, kind and rows; last, since it grows with the workbook.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableObject> Objects { get; init; }

    /// <summary>How many objects of each kind of content the workbook holds: under read the kinds read lists, under notRead the others.</summary>
    [JsonPropertyOrder(850)]
    public required TableInventory Inventory { get; init; }

    /// <summary>The warnings Aspose.Cells reported while it loaded the workbook, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
