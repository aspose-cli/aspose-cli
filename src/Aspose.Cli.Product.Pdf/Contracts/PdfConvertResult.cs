using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf convert</c>.</summary>
public sealed record PdfConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted PDF.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The files written: one, or one per page for an image format.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<OutputInfo> Outputs { get; init; }

    /// <summary>The page range converted, as <c>--pages</c> gave it; absent when every page was.</summary>
    public string? Pages { get; init; }
}
