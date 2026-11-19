using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides create</c>: the new presentation and the template it was saved from, if any.</summary>
public sealed record SlidesCreateResult() : EngineResultEnvelope("create-result")
{
    /// <summary>The created presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required OutputInfo Output { get; init; }

    /// <summary>How many slides the created presentation holds.</summary>
    [Minimum(0)]
    public required int SlideCount { get; init; }

    /// <summary>The template the presentation was saved from; empty for the engine's new presentation.</summary>
    public required IReadOnlyList<SourceInput> Inputs { get; init; }

    /// <summary>The warnings Aspose.Slides reported while it loaded the template and saved the presentation, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
