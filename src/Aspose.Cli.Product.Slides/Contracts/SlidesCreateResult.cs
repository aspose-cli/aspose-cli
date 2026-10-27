using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides create</c>: the new presentation and what it was built from.</summary>
public sealed record SlidesCreateResult() : EngineResultEnvelope("create-result")
{
    /// <summary>The created presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required OutputInfo Output { get; init; }

    /// <summary>How many slides the created presentation holds.</summary>
    [Minimum(1)]
    public required int SlideCount { get; init; }

    /// <summary>The files the presentation was built from, the template and the Markdown outline, in command-line order; empty for a blank presentation.</summary>
    public required IReadOnlyList<SourceInput> Inputs { get; init; }
}
