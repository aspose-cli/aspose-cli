using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Structural information returned by <c>words inspect</c>.</summary>
public sealed record DocumentInfoResult() : ResultEnvelope(WordsSchemaIds.DocumentInfo, 2)
{
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "document";
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }
    public required DocumentSummary Document { get; init; }
    public IReadOnlyList<SectionData>? Sections { get; init; }
    public IReadOnlyList<OutlineItem>? Outline { get; init; }
    public IReadOnlyList<string>? Styles { get; init; }
    public IReadOnlyList<FieldData>? Fields { get; init; }
    public IReadOnlyList<string>? Bookmarks { get; init; }
    public IReadOnlyList<CommentData>? Comments { get; init; }
    public IReadOnlyList<ImageData>? Images { get; init; }
    public IReadOnlyList<TableData>? Tables { get; init; }
    public IReadOnlyDictionary<string, string?>? Properties { get; init; }
    public IReadOnlyList<string>? Fonts { get; init; }
}

/// <summary>Always-on document summary.</summary>
public sealed record DocumentSummary
{
    public required int Sections { get; init; }
    public required int Blocks { get; init; }
    public required int Paragraphs { get; init; }
    public required int Tables { get; init; }
    public required int Pages { get; init; }
    public required int Words { get; init; }
    public required bool RevisionsPresent { get; init; }
    public required int RevisionCount { get; init; }
    public required IReadOnlyList<string> RevisionAuthors { get; init; }
    public required string Protection { get; init; }
    public required bool Signed { get; init; }
}

/// <summary>One document section.</summary>
public sealed record SectionData
{
    public required int Index { get; init; }
    public required string Orientation { get; init; }
    public required double WidthPoints { get; init; }
    public required double HeightPoints { get; init; }
    public required MarginData Margins { get; init; }
}

/// <summary>Page margins in points.</summary>
public sealed record MarginData
{
    public required double Top { get; init; }
    public required double Right { get; init; }
    public required double Bottom { get; init; }
    public required double Left { get; init; }
}

/// <summary>One heading in document order.</summary>
public sealed record OutlineItem
{
    public required int Block { get; init; }
    public required int Level { get; init; }
    public required string Text { get; init; }
}

/// <summary>One field summary.</summary>
public sealed record FieldData
{
    public required string Type { get; init; }
    public required int Block { get; init; }
    public string? Code { get; init; }
    public string? Result { get; init; }
}

/// <summary>One comment summary.</summary>
public sealed record CommentData
{
    public required string Author { get; init; }
    public required string Text { get; init; }
    public int? Block { get; init; }
}

/// <summary>One embedded image summary.</summary>
public sealed record ImageData
{
    public required int Block { get; init; }
    public string? Name { get; init; }
    public required double WidthPoints { get; init; }
    public required double HeightPoints { get; init; }
}

/// <summary>One table summary.</summary>
public sealed record TableData
{
    public required int Block { get; init; }
    public required int Rows { get; init; }
    public required int Columns { get; init; }
}

/// <summary>Windowed block projection returned by <c>words query blocks</c>.</summary>
public sealed record DocumentReadResult() : ResultEnvelope(WordsSchemaIds.DocumentRead, 2)
{
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "document";
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }
    public required string Scope { get; init; }
    public required BlockWindow Window { get; init; }
    public required IReadOnlyList<BlockData> Blocks { get; init; }
    public string? Next { get; init; }
}

/// <summary>Description of the returned block window.</summary>
public sealed record BlockWindow
{
    public required string Blocks { get; init; }
    public required int Of { get; init; }
    public required bool Truncated { get; init; }
}

/// <summary>A paragraph or table directly owned by a section body.</summary>
public sealed record BlockData
{
    public required int I { get; init; }
    public required string Type { get; init; }
    public required int Section { get; init; }
    public string? Text { get; init; }
    public string? Style { get; init; }
    public int? HeadingLevel { get; init; }
    public IReadOnlyList<RunData>? Runs { get; init; }
    public IReadOnlyList<ImageData>? Images { get; init; }
    public string? BreakAfter { get; init; }
    public int? Rows { get; init; }
    public int? Columns { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Cells { get; init; }
    public bool ContentTruncated { get; init; }
}

/// <summary>Run-level formatting returned in full scope.</summary>
public sealed record RunData
{
    public required string Text { get; init; }
    public string? Font { get; init; }
    public double? Size { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public string? Color { get; init; }
}

/// <summary>Result of <c>words convert</c>.</summary>
public sealed record WordsConvertResult() : ResultEnvelope(WordsSchemaIds.ConvertResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
    public string? Pages { get; init; }
}

/// <summary>Result of <c>words render</c>.</summary>
public sealed record WordsRenderResult() : ResultEnvelope(WordsSchemaIds.RenderResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    public required IReadOnlyList<PageOutput> Outputs { get; init; }
    public int? Dpi { get; init; }
}

/// <summary>One rendered page.</summary>
public sealed record PageOutput
{
    public required int Page { get; init; }
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of creating a document.</summary>
public sealed record WordsCreateResult() : ResultEnvelope(WordsSchemaIds.CreateResult, 2)
{
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of a Words mutation.</summary>
public sealed record WordsEditResult() : ResultEnvelope(WordsSchemaIds.EditResult, 2), IPartialOutcome
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }
    public required bool DryRun { get; init; }
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }
    public BackupInfo? Backup { get; init; }
    public IReadOnlyList<int>? PagesTouched { get; init; }
    public WordsVerification? Verification { get; init; }
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static item => item.Status == OpStatuses.Failed)
        || Verification is { Ok: false };
}

/// <summary>One Words op outcome.</summary>
/// <summary>Post-edit verification evidence.</summary>
public sealed record WordsVerification
{
    public required bool Ok { get; init; }
    public required IReadOnlyList<string> Issues { get; init; }
    public bool? SemanticChangesDetected { get; init; }
    public int? FieldCount { get; init; }
    public int? RevisionCount { get; init; }
    public string? Protection { get; init; }
}

/// <summary>Semantic comparison result.</summary>
public sealed record WordsCompareResult() : ResultEnvelope(WordsSchemaIds.CompareResult, 2)
{
    public required SourceInfo Left { get; init; }
    public required SourceInfo Right { get; init; }
    public required bool Identical { get; init; }
    public required RevisionCounts Revisions { get; init; }
    public required IReadOnlyList<RevisionSample> Samples { get; init; }
    public OutputInfo? Output { get; init; }
}

/// <summary>Counts by revision type.</summary>
public sealed record RevisionCounts
{
    public required int Insertions { get; init; }
    public required int Deletions { get; init; }
    public required int FormatChanges { get; init; }
    public required int Moves { get; init; }
}

/// <summary>Bounded revision sample.</summary>
public sealed record RevisionSample
{
    public required string Type { get; init; }
    public required string Text { get; init; }
}

/// <summary>Budgeted document search result.</summary>
public sealed record WordsSearchResult() : ResultEnvelope(WordsSchemaIds.SearchResult, 2)
{
    public required SourceInfo Source { get; init; }
    public required string Pattern { get; init; }
    public required IReadOnlyList<WordsSearchHit> Hits { get; init; }
    public required bool Truncated { get; init; }
}

/// <summary>One search hit.</summary>
public sealed record WordsSearchHit
{
    public required int Block { get; init; }
    public required int Section { get; init; }
    public required string Scope { get; init; }
    public required string Snippet { get; init; }
}

/// <summary>Result of splitting a document.</summary>
public sealed record WordsSplitResult() : ResultEnvelope(WordsSchemaIds.SplitResult, 2)
{
    public required SourceInfo Input { get; init; }
    public required IReadOnlyList<SplitOutput> Outputs { get; init; }
}

/// <summary>One split output.</summary>
public sealed record SplitOutput
{
    public required int Index { get; init; }
    public required OutputInfo Output { get; init; }
    public string? Source { get; init; }
}

/// <summary>Result of extracting assets.</summary>
public sealed record WordsExtractResult() : ResultEnvelope(WordsSchemaIds.ExtractResult, 2)
{
    public required SourceInfo Input { get; init; }
    public required string What { get; init; }
    public required IReadOnlyList<ExtractedItem> Items { get; init; }
}

/// <summary>One extracted item.</summary>
public sealed record ExtractedItem
{
    public required string Path { get; init; }
    public required string Kind { get; init; }
    public required long SizeBytes { get; init; }
    public int? Block { get; init; }
}
