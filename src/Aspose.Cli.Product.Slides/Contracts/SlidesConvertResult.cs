using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>
/// Result of <c>slides convert</c>: the files a presentation or its selected slides were
/// converted to. PNG and JPEG use the default 192 DPI render geometry and pixel budgets.
/// </summary>
public sealed record SlidesConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written files: one per slide, each with its slide, for an image format; otherwise one, with any companion files the format needs.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<SlidesSlideOutput> Outputs { get; init; }

    /// <summary>The <c>--slides</c> selection that was converted, normalized; omitted when every slide was.</summary>
    [PageRange]
    public string? Slides { get; init; }
}
