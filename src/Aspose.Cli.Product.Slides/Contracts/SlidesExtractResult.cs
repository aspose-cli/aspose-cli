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
    public required IReadOnlyList<SlidesExtractedItem> Items { get; init; }
}

/// <summary>One extracted file.</summary>
public sealed record SlidesExtractedItem
{
    /// <summary>Absolute path of the written file.</summary>
    public required string Path { get; init; }

    /// <summary>What the file holds: a media kind, or the speaker notes or text of one slide.</summary>
    [AllowedValues("image", "audio", "video", "notes", "text")]
    public required string Kind { get; init; }

    /// <summary>The file's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>The 1-based slide number of slide text or notes; omitted for media.</summary>
    [Minimum(1)]
    public int? Slide { get; init; }

    /// <summary>The stable slide id of slide text or notes; omitted for media.</summary>
    [Minimum(1)]
    public uint? SlideId { get; init; }

    /// <summary>
    /// The media item's 1-based position among all images, then audio, then video, as inspect
    /// reports it; omitted for text and notes.
    /// </summary>
    [Minimum(1)]
    public int? Index { get; init; }

    /// <summary>The slide's name for slide text or notes; omitted when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>The MIME type of the file; omitted when the presentation states none for a media item.</summary>
    public string? ContentType { get; init; }
}
