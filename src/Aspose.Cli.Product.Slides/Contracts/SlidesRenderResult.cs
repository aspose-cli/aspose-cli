using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides render</c>: one image per rendered slide.</summary>
public sealed record SlidesRenderResult() : EngineResultEnvelope("render-result", 2)
{
    /// <summary>The rendered presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The rendered slides in presentation order.</summary>
    [MinItems(1)]
    public required IReadOnlyList<SlidesSlideOutput> Outputs { get; init; }

    /// <summary>The raster resolution used; omitted for SVG and when <c>--width</c> set the size.</summary>
    [Minimum(RenderPixelGuard.MinimumDpi)]
    [Maximum(RenderPixelGuard.MaximumDpi)]
    public int? Dpi { get; init; }

    /// <summary>The exact raster width in pixels <c>--width</c> asked for; omitted otherwise.</summary>
    [Minimum(64)]
    [Maximum(20_000)]
    public int? Width { get; init; }
}

/// <summary>One rendered slide image.</summary>
public sealed record SlidesSlideOutput : OutputItem
{
    /// <summary>Describes the image written for a slide.</summary>
    /// <param name="file">The published image, as <c>FileObjects.Output</c> describes it.</param>
    internal SlidesSlideOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The 1-based slide number.</summary>
    [Minimum(1)]
    public required int Slide { get; init; }
}
