using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf render</c>.</summary>
public sealed record PdfRenderResult() : EngineResultEnvelope("render-result")
{
    /// <summary>The rendered PDF.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>Each written image and the page it shows, in page order.</summary>
    public required IReadOnlyList<PdfPageOutput> Outputs { get; init; }

    /// <summary>The resolution raster images were rendered at; absent for SVG.</summary>
    [Minimum(36)]
    [Maximum(1200)]
    public int? Dpi { get; init; }
}
