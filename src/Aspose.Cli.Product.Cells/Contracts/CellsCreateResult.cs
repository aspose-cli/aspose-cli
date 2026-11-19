using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>cells create</c>: the new workbook and the template it was saved from, if any.</summary>
public sealed record CellsCreateResult() : EngineResultEnvelope("create-result")
{
    /// <summary>The created workbook.</summary>
    [JsonPropertyOrder(-50)]
    public required OutputInfo Output { get; init; }

    /// <summary>The template the workbook was saved from; empty for the engine's new workbook.</summary>
    public required IReadOnlyList<SourceInput> Inputs { get; init; }

    /// <summary>The warnings Aspose.Cells reported while it loaded the template and saved the workbook, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
