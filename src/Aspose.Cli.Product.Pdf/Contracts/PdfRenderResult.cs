using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf render</c>.</summary>
public sealed record PdfRenderResult() : EngineResultEnvelope("render-result", 2)
{
    /// <summary>The rendered PDF.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>Each written image, in page order.</summary>
    public required IReadOnlyList<PdfPageOutput> Outputs { get; init; }

    /// <summary>The resolution raster images were rendered at; absent for SVG.</summary>
    [Minimum(36)]
    [Maximum(1200)]
    public int? Dpi { get; init; }

    /// <summary>The coordinate grid drawn on every output image, present only when one was requested.</summary>
    public PdfRenderGrid? Grid { get; init; }
}

/// <summary>
/// A coordinate grid drawn on rendered images, in the coordinates <c>redact_area</c> takes: points from the
/// top-left corner of the visible, rotated page box.
/// </summary>
public sealed record PdfRenderGrid
{
    /// <summary>Distance between grid lines, in points.</summary>
    [Minimum(10)]
    [Maximum(500)]
    public required int Spacing { get; init; }

    /// <summary>Distance between the heavier, labelled lines, in points.</summary>
    [Minimum(10)]
    public required int LabelSpacing { get; init; }

    /// <summary>The unit of the distances and labels: always <c>pt</c>.</summary>
    public string Unit { get; } = "pt";

    /// <summary>Where the coordinates start: always <c>top-left</c>.</summary>
    public string Origin { get; } = "top-left";
}

/// <summary>One rendered PDF page.</summary>
public sealed record PdfPageOutput
{
    /// <summary>The 1-based page rendered.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The image written for the page.</summary>
    public required OutputInfo Output { get; init; }
}
