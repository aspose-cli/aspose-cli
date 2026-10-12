using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides create</c>: the new presentation and what it was built from.</summary>
public sealed record SlidesCreateResult() : EngineResultEnvelope("create-result", 2)
{
    /// <summary>The created presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required OutputInfo Output { get; init; }

    /// <summary>How many slides the created presentation holds.</summary>
    [Minimum(1)]
    public required int SlideCount { get; init; }

    /// <summary>The template the presentation was built from; omitted without <c>--template</c>.</summary>
    public InputInfo? Template { get; init; }

    /// <summary>The Markdown outline the slides were filled from; omitted without <c>--from-markdown</c>.</summary>
    public InputInfo? Markdown { get; init; }
}
