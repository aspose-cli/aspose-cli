using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides extract</c>: the files written for the extracted media.</summary>
public sealed record SlidesExtractResult() : EngineResultEnvelope("extract-result")
{
    /// <summary>The presentation extracted from.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted.</summary>
    [AllowedValues(typeof(SlidesExtractKinds))]
    public required string What { get; init; }

    /// <summary>The written files, in presentation order: images, then audio, then video.</summary>
    public required IReadOnlyList<SlidesExtractedOutput> Outputs { get; init; }

    /// <summary>The warnings Aspose.Slides reported while it loaded the presentation, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}

/// <summary>One extracted file and the media item it holds, by its <c>index</c>.</summary>
public sealed record SlidesExtractedOutput : OutputItem
{
    /// <summary>Describes a file written for one media item.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Companion</c> describes it.</param>
    internal SlidesExtractedOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The media item's 1-based position among all images, then audio, then video.</summary>
    [Minimum(1)]
    public int? Index { get; init; }
}
