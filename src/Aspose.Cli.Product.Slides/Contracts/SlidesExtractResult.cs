using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides extract</c>: the files written for the extracted content.</summary>
public sealed record SlidesExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The presentation extracted from.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted.</summary>
    [AllowedValues(typeof(SlidesExtractKinds))]
    public required string What { get; init; }

    /// <summary>The written files: media in presentation order, or one text file per slide that has text.</summary>
    public required IReadOnlyList<SlidesExtractedOutput> Outputs { get; init; }
}

/// <summary>
/// One extracted file, and the media item or slide it holds: a media file names its
/// <c>index</c>, a file of slide text or notes its <c>slide</c>.
/// </summary>
public sealed record SlidesExtractedOutput : OutputItem
{
    /// <summary>Describes a file written for one media item or one slide's text or notes.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Companion</c> describes it.</param>
    internal SlidesExtractedOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The 1-based slide number of slide text or notes; omitted for media.</summary>
    [Minimum(1)]
    public int? Slide { get; init; }

    /// <summary>
    /// The media item's 1-based position among all images, then audio, then video, as inspect
    /// reports it; omitted for text and notes.
    /// </summary>
    [Minimum(1)]
    public int? Index { get; init; }
}
