using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf merge</c>.</summary>
public sealed record PdfMergeResult() : EngineResultEnvelope("merge-result", 2)
{
    /// <summary>The command that wrote the PDF: always <c>merge</c>.</summary>
    [JsonPropertyOrder(-50)]
    [AllowedValues("merge")]
    public required string Action { get; init; }

    /// <summary>The PDF written.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The documents merged, in order.</summary>
    public required IReadOnlyList<InputInfo> Inputs { get; init; }
}
