using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells create</c>.
/// </summary>
public sealed record CellsCreateResult() : EngineResultEnvelope("create-result", 2)
{
    /// <summary>The produced file.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>Sheet names of a newly created workbook.</summary>
    [MinItems(1), MinLength(1)]
    public required IReadOnlyList<string> Sheets { get; init; }
}
