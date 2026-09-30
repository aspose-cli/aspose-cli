using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Structural information about a presentation.</summary>
public sealed record PresentationInfoResult() : ResultEnvelope(SlidesSchemaIds.PresentationInfo, 2)
{
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "presentation";

    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    public required PresentationSummary Presentation { get; init; }
    public required IReadOnlyList<SlideInfo> Slides { get; init; }
    public IReadOnlyList<PresentationSectionInfo>? Sections { get; init; }
    public IReadOnlyList<PresentationMasterInfo>? Masters { get; init; }
    public IReadOnlyList<PresentationLayoutInfo>? Layouts { get; init; }
    public IReadOnlyList<PresentationMediaInfo>? Media { get; init; }
    public IReadOnlyList<PresentationNotesInfo>? Notes { get; init; }
    public IReadOnlyList<PresentationCommentInfo>? Comments { get; init; }
    public IReadOnlyList<string>? Fonts { get; init; }
    public IReadOnlyDictionary<string, string?>? Properties { get; init; }
}

/// <summary>Stable presentation-level counts and geometry.</summary>
public sealed record PresentationSummary
{
    public required int SlideCount { get; init; }
    public required double WidthPoints { get; init; }
    public required double HeightPoints { get; init; }
    public required string Orientation { get; init; }
    public required int MasterCount { get; init; }
    public required int LayoutCount { get; init; }
    public required int SectionCount { get; init; }
    public required int CommentCount { get; init; }
    public required int MediaCount { get; init; }
    public required bool HasMacros { get; init; }
}

/// <summary>One slide in a structural presentation inventory.</summary>
public sealed record SlideInfo
{
    /// <summary>The 1-based slide number.</summary>
    public required int Slide { get; init; }
    public required uint SlideId { get; init; }
    public string? Name { get; init; }
    public string? Layout { get; init; }
    public string? Title { get; init; }
    public string? PreviewText { get; init; }
    public required int ShapeCount { get; init; }
    public required bool Hidden { get; init; }
    public required bool HasNotes { get; init; }
    public required int CommentCount { get; init; }
}

/// <summary>One presentation master and its direct slide usage.</summary>
public sealed record PresentationMasterInfo
{
    public required string Name { get; init; }

    /// <summary>How many slides use one of this master's layouts.</summary>
    public required int SlideCount { get; init; }
}

/// <summary>One presentation layout and its direct slide usage.</summary>
public sealed record PresentationLayoutInfo
{
    public required string Name { get; init; }
    public required string? Master { get; init; }

    /// <summary>How many slides use this layout.</summary>
    public required int SlideCount { get; init; }
}

/// <summary>One bounded embedded media asset.</summary>
public sealed record PresentationMediaInfo
{
    public required int Index { get; init; }
    public required string Type { get; init; }
    public string? ContentType { get; init; }
    public required long SizeBytes { get; init; }
}

/// <summary>Speaker-note presence and size for one slide.</summary>
public sealed record PresentationNotesInfo
{
    public required int Slide { get; init; }
    public required bool Present { get; init; }
    public required int CharacterCount { get; init; }
}

/// <summary>One presentation comment in deterministic slide order.</summary>
public sealed record PresentationCommentInfo
{
    public required int Slide { get; init; }
    public required string Author { get; init; }
    public required string Text { get; init; }
}

/// <summary>One named presentation section.</summary>
public sealed record PresentationSectionInfo
{
    public required string Name { get; init; }
    public required string SectionId { get; init; }
    public required int StartSlide { get; init; }
}

/// <summary>Windowed slide-content projection returned by <c>slides query slides</c>.</summary>
public sealed record PresentationReadResult() : ResultEnvelope(SlidesSchemaIds.PresentationRead, 2)
{
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "presentation";

    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    public required string Scope { get; init; }

    /// <summary>How many slides the presentation holds, which open slide ranges resolve against.</summary>
    public required int SlideCount { get; init; }

    public required IReadOnlyList<SlideData> Slides { get; init; }
}

/// <summary>Agent-friendly content of one slide.</summary>
public sealed record SlideData
{
    /// <summary>The 1-based slide number.</summary>
    public required int Slide { get; init; }
    public required uint SlideId { get; init; }
    public string? Name { get; init; }
    public string? Layout { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<string>? Text { get; init; }
    public required IReadOnlyList<SlideShapeData> Shapes { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<SlideCommentData>? Comments { get; init; }
    public required bool ContentTruncated { get; init; }
}

/// <summary>One bounded, SDK-neutral slide shape.</summary>
public sealed record SlideShapeData
{
    /// <summary>A positive shape identifier persisted within its slide.</summary>
    public required long ShapeId { get; init; }
    public string? ShapeName { get; init; }
    public required string Type { get; init; }

    /// <summary>The placeholder role, which set_text and the other shape operations accept as placeholder when it is one they name.</summary>
    public string? Placeholder { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<SlideTextRunData>? Runs { get; init; }
    public required SlideRect Rect { get; init; }

    // Product-internal review facts. These deliberately remain outside the
    // frozen wire contract while preserving the shape order and effective fill
    // semantics needed for conservative deterministic overlap checks.
    internal int ZOrder { get; init; }
    internal bool HasOpaqueFill { get; init; }

    /// <summary>Where the shape's text is laid out, in slide points; null for a shape without text.</summary>
    internal SlideRect? TextRect { get; init; }
}

/// <summary>One text run with effective formatting used by the full projection.</summary>
public sealed record SlideTextRunData
{
    public required string Text { get; init; }
    public string? Font { get; init; }
    public double? Size { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
}

/// <summary>A slide rectangle in points from the top-left origin.</summary>
public sealed record SlideRect
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}

/// <summary>One comment attached to a slide.</summary>
public sealed record SlideCommentData
{
    public required string Author { get; init; }
    public required string Text { get; init; }
}

/// <summary>Result of converting a presentation or selected slides.</summary>
public sealed record SlidesConvertResult() : ResultEnvelope(SlidesSchemaIds.ConvertResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    [JsonPropertyOrder(-49)]
    public required IReadOnlyList<OutputInfo> Outputs { get; init; }

    public string? Slides { get; init; }
}

/// <summary>Result of rendering one or more presentation slides.</summary>
public sealed record SlidesRenderResult() : ResultEnvelope(SlidesSchemaIds.RenderResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    public required IReadOnlyList<SlideRenderOutput> Outputs { get; init; }
    public int? Dpi { get; init; }
    public int? Width { get; init; }
}

/// <summary>One rendered slide artifact.</summary>
public sealed record SlideRenderOutput
{
    public required int Slide { get; init; }
    public required uint SlideId { get; init; }
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of creating a presentation.</summary>
public sealed record SlidesCreateResult() : ResultEnvelope(SlidesSchemaIds.CreateResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required OutputInfo Output { get; init; }

    public required int SlideCount { get; init; }
    public SourceInfo? Template { get; init; }
    public SourceInfo? Markdown { get; init; }
}

/// <summary>Result of extracting bounded presentation content.</summary>
public sealed record SlidesExtractResult() : ResultEnvelope(SlidesSchemaIds.ExtractResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    public required string What { get; init; }
    public required IReadOnlyList<SlidesExtractedItem> Items { get; init; }
}

/// <summary>One extracted presentation asset or text artifact.</summary>
public sealed record SlidesExtractedItem
{
    public required string Path { get; init; }
    public required string Kind { get; init; }
    public required long SizeBytes { get; init; }
    public int? Slide { get; init; }
    public uint? SlideId { get; init; }
    public int? Index { get; init; }
    public string? Name { get; init; }
    public string? ContentType { get; init; }
}

/// <summary>Result of applying one atomic presentation operation batch.</summary>
public sealed record SlidesEditResult() : ResultEnvelope(SlidesSchemaIds.EditResult, 2), IPartialOutcome
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    public OutputInfo? Output { get; init; }
    public required bool DryRun { get; init; }
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }
    public BackupInfo? Backup { get; init; }
    public IReadOnlyList<uint>? SlidesTouched { get; init; }

    [JsonIgnore]
    public bool HasFailures => Applied.Any(static op => op.Status == "failed");
}

/// <summary>Bounded presentation search result.</summary>
public sealed record SlidesSearchResult() : ResultEnvelope(SlidesSchemaIds.SearchResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Source { get; init; }

    public required string Pattern { get; init; }
    public required string Scope { get; init; }
    public required IReadOnlyList<SlidesSearchHit> Hits { get; init; }
}

/// <summary>One shape or notes match in a presentation.</summary>
public sealed record SlidesSearchHit
{
    public required int Slide { get; init; }
    public required uint SlideId { get; init; }
    public required string Scope { get; init; }
    public long? ShapeId { get; init; }
    public string? ShapeName { get; init; }
    public required string Text { get; init; }
    public required int Start { get; init; }
    public required int Length { get; init; }
}
