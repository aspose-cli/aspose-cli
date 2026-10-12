using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>aspose-cli cells convert</c>.</summary>
public sealed record CellsConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted input file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The produced output file.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The sheets the conversion was limited to, in workbook order, when <c>--sheets</c> selected some.</summary>
    [MinItems(1)]
    public IReadOnlyList<string>? Sheets { get; init; }
}
