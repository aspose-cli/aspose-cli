using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words render</c>.</summary>
public sealed record WordsRenderResult() : EngineResultEnvelope("render-result", 2)
{
    /// <summary>The rendered document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>One written image per rendered page, in page order.</summary>
    [MinItems(1)]
    public required IReadOnlyList<WordsPageOutput> Outputs { get; init; }

    /// <summary>The raster resolution of png and jpeg output; absent for svg.</summary>
    [Minimum(RenderPixelGuard.MinimumDpi)]
    [Maximum(RenderPixelGuard.MaximumDpi)]
    public int? Dpi { get; init; }
}

/// <summary>One rendered page.</summary>
public sealed record WordsPageOutput
{
    /// <summary>The 1-based page.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The page's image file.</summary>
    public required OutputInfo Output { get; init; }
}
