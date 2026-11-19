using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>cells edit</c>: what each operation left, read from the saved workbook after it
/// was opened again, and the inventory counts that changed.
/// </summary>
public sealed record CellsEditResult() : EngineResultEnvelope("edit-result")
{
    /// <summary>The edited workbook.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written workbook.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>What each operation left, in batch order: the object at its address in the saved file, or what its action returned; last, since it grows with the batch.</summary>
    [JsonPropertyOrder(1000)]
    public required IReadOnlyList<TableOpReport> Ops { get; init; }

    /// <summary>The inventory counts that changed, each as [before, after]; the keys are those read reports in inventory.</summary>
    [JsonPropertyOrder(850)]
    public required IReadOnlyDictionary<string, IReadOnlyList<long>> Counts { get; init; }

    /// <summary>The warnings Aspose.Cells reported while it loaded, saved and reopened the workbook, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
