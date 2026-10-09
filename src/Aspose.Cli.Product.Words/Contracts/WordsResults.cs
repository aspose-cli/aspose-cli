using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Structural information returned by <c>words inspect</c>.</summary>
public sealed record DocumentInfoResult() : ResultEnvelope("document-info", 2)
{
    /// <summary>The kind of file inspected: always <c>document</c>.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "document";

    /// <summary>The inspected document.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    /// <summary>The summary every inspect returns, whatever details it asks for.</summary>
    public required DocumentSummary Document { get; init; }

    /// <summary>Each section's page setup and headers and footers; returned with <c>--detail sections</c>.</summary>
    public IReadOnlyList<SectionData>? Sections { get; init; }

    /// <summary>The headings in document order; returned with <c>--detail outline</c>.</summary>
    public IReadOnlyList<OutlineItem>? Outline { get; init; }

    /// <summary>The names of the styles the document defines, in ordinal order; returned with <c>--detail styles</c>.</summary>
    public IReadOnlyList<string>? Styles { get; init; }

    /// <summary>The fields in every story; returned with <c>--detail fields</c>.</summary>
    public IReadOnlyList<FieldData>? Fields { get; init; }

    /// <summary>The bookmark names, in ordinal order; returned with <c>--detail bookmarks</c>.</summary>
    public IReadOnlyList<string>? Bookmarks { get; init; }

    /// <summary>The comments, replies included; returned with <c>--detail comments</c>.</summary>
    public IReadOnlyList<CommentData>? Comments { get; init; }

    /// <summary>
    /// The tracked changes in document order, one entry per change; returned with
    /// <c>--detail revisions</c>. A replacement is a deletion followed by an insertion.
    /// document.revisionCount counts the revisions the document stores, one per run, paragraph
    /// mark or other changed node, so it can exceed the entries here.
    /// </summary>
    public IReadOnlyList<RevisionData>? Revisions { get; init; }

    /// <summary>The embedded images in every story; returned with <c>--detail images</c>.</summary>
    public IReadOnlyList<ImageData>? Images { get; init; }

    /// <summary>The tables; returned with <c>--detail tables</c>.</summary>
    public IReadOnlyList<TableData>? Tables { get; init; }

    /// <summary>
    /// The title, author, subject and keywords document properties by name, null for one the
    /// document does not set; returned with <c>--detail properties</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Properties { get; init; }

    /// <summary>
    /// The fonts the document's text is drawn with, per character script; not the font table.
    /// Returned with <c>--detail fonts</c>.
    /// </summary>
    public IReadOnlyList<string>? Fonts { get; init; }
}

/// <summary>The summary of a document that every inspect returns.</summary>
public sealed record DocumentSummary
{
    /// <summary>The number of sections.</summary>
    [Minimum(0)]
    public required int SectionCount { get; init; }

    /// <summary>The number of blocks: the paragraphs and tables directly in section bodies.</summary>
    [Minimum(0)]
    public required int BlockCount { get; init; }

    /// <summary>The number of paragraph blocks.</summary>
    [Minimum(0)]
    public required int ParagraphCount { get; init; }

    /// <summary>The number of table blocks.</summary>
    [Minimum(0)]
    public required int TableCount { get; init; }

    /// <summary>The number of pages in the document's layout.</summary>
    [Minimum(0)]
    public required int PageCount { get; init; }

    /// <summary>The word count the document's built-in properties record.</summary>
    [Minimum(0)]
    public required int WordCount { get; init; }

    /// <summary>Whether the document holds tracked changes.</summary>
    public required bool RevisionsPresent { get; init; }

    /// <summary>
    /// The revisions the document stores, one per run, paragraph mark or other changed node;
    /// list the changes with <c>--detail revisions</c>.
    /// </summary>
    [Minimum(0)]
    public required int RevisionCount { get; init; }

    /// <summary>The distinct authors of the tracked changes, in ordinal order.</summary>
    public required IReadOnlyList<string> RevisionAuthors { get; init; }

    /// <summary>The comments in the document, replies included; list them with <c>--detail comments</c>.</summary>
    [Minimum(0)]
    public required int CommentCount { get; init; }

    /// <summary>
    /// The editing restriction: none, or the protect operation mode that sets it; every SDK
    /// protection type maps to one of these.
    /// </summary>
    [AllowedValues(typeof(WordsProtectionModes))]
    public required string Protection { get; init; }

    /// <summary>Whether the document carries a digital signature.</summary>
    public required bool Signed { get; init; }

    /// <summary>
    /// Whether the document holds macros (a VBA project); an output other than docm, dotm, doc,
    /// dot or wordml drops them and reports MACROS_DROPPED.
    /// </summary>
    public required bool HasMacros { get; init; }
}

/// <summary>One document section.</summary>
public sealed record SectionData
{
    /// <summary>The 1-based section number that section operations accept.</summary>
    [Minimum(1)]
    public required int Section { get; init; }

    /// <summary>The orientation of the section's pages.</summary>
    [AllowedValues(typeof(PageOrientations))]
    public required string Orientation { get; init; }

    /// <summary>The page width in points.</summary>
    [Minimum(0)]
    public required double WidthPoints { get; init; }

    /// <summary>The page height in points.</summary>
    [Minimum(0)]
    public required double HeightPoints { get; init; }

    /// <summary>The page margins.</summary>
    public required MarginData Margins { get; init; }

    /// <summary>
    /// The headers and footers the section defines itself; a kind it does not define continues
    /// from the previous section.
    /// </summary>
    public required IReadOnlyList<HeaderFooterData> HeadersFooters { get; init; }
}

/// <summary>
/// One header or footer of a section, named as set_header, set_footer and set_page_numbers
/// name it: its <c>location</c> and <c>kind</c>, and its visible text as <c>paragraphs</c>.
/// </summary>
public sealed record HeaderFooterData
{
    /// <summary>Whether it is a header or a footer, as set_page_numbers names it.</summary>
    [AllowedValues(typeof(HeaderFooterLocations))]
    public required string Location { get; init; }

    /// <summary>Which pages it shows on, as set_header and set_footer name it.</summary>
    [AllowedValues(typeof(HeaderFooterKinds))]
    public required string Kind { get; init; }

    /// <summary>The visible text of each paragraph, as set_header and set_footer take it.</summary>
    public required IReadOnlyList<string> Paragraphs { get; init; }
}

/// <summary>Page margins in points.</summary>
public sealed record MarginData
{
    /// <summary>The top margin.</summary>
    public required double Top { get; init; }

    /// <summary>The right margin.</summary>
    public required double Right { get; init; }

    /// <summary>The bottom margin.</summary>
    public required double Bottom { get; init; }

    /// <summary>The left margin.</summary>
    public required double Left { get; init; }
}

/// <summary>One heading in document order.</summary>
public sealed record OutlineItem
{
    /// <summary>The 1-based block of the heading paragraph.</summary>
    [Minimum(1)]
    public required int Block { get; init; }

    /// <summary>The heading level, 1 for Heading 1.</summary>
    [Minimum(1)]
    public required int HeadingLevel { get; init; }

    /// <summary>The heading's text.</summary>
    public required string Text { get; init; }
}

/// <summary>One field in any story.</summary>
public sealed record FieldData
{
    /// <summary>The SDK's name of the field type, such as FieldDate.</summary>
    public required string Type { get; init; }

    /// <summary>The story that holds the field, named as query search names its scopes.</summary>
    [AllowedValues(typeof(WordsStoryScopes))]
    public required string Scope { get; init; }

    /// <summary>
    /// The body block that holds the field, or that anchors the comment or note holding it;
    /// absent in headers and footers.
    /// </summary>
    [Minimum(1)]
    public int? Block { get; init; }

    /// <summary>The field code, such as <c>DATE</c> with its switches.</summary>
    public string? Code { get; init; }

    /// <summary>The result a reader sees, with the results of nested fields in place of their codes.</summary>
    public string? Result { get; init; }
}

/// <summary>One comment.</summary>
public sealed record CommentData
{
    /// <summary>The comment's author.</summary>
    public required string Author { get; init; }

    /// <summary>The comment's text.</summary>
    public required string Text { get; init; }

    /// <summary>The 1-based body block the comment is anchored in.</summary>
    [Minimum(1)]
    public int? Block { get; init; }
}

/// <summary>
/// One tracked change in document order: its 1-based number, which accept_revisions and
/// reject_revisions accept, its type, author, the date the document records and the text it
/// inserts, deletes or moves.
/// </summary>
public sealed record RevisionData
{
    /// <summary>
    /// The 1-based number of the change in this list, which the revisions of accept_revisions
    /// and reject_revisions accept.
    /// </summary>
    [Minimum(1)]
    public required int Revision { get; init; }

    /// <summary>The kind of change.</summary>
    [AllowedValues(typeof(WordsRevisionTypes))]
    public required string Type { get; init; }

    /// <summary>The change's author.</summary>
    public required string Author { get; init; }

    /// <summary>
    /// The date and time the document records, as ISO 8601 yyyy-MM-ddTHH:mm:ss without a time
    /// zone; absent when none is recorded.
    /// </summary>
    public string? Date { get; init; }

    /// <summary>
    /// The story that holds the change, named as query search names its scopes; absent for
    /// style definition changes.
    /// </summary>
    [AllowedValues(typeof(WordsStoryScopes))]
    public string? Scope { get; init; }

    /// <summary>
    /// The body block that holds the change, or that anchors the comment or note holding it;
    /// absent in headers and footers and for style definition changes.
    /// </summary>
    [Minimum(1)]
    public int? Block { get; init; }

    /// <summary>
    /// The text an insertion adds, a deletion removes or a move relocates; a longer text is cut
    /// to 300 characters and an ellipsis; absent for format and style definition changes.
    /// </summary>
    public string? Text { get; init; }
}

/// <summary>One embedded image in any story; its size is in points.</summary>
public sealed record ImageData
{
    /// <summary>The story that holds the image, named as query search names its scopes.</summary>
    [AllowedValues(typeof(WordsStoryScopes))]
    public required string Scope { get; init; }

    /// <summary>
    /// The body block that holds the image, or that anchors the comment or note holding it;
    /// absent in headers and footers.
    /// </summary>
    [Minimum(1)]
    public int? Block { get; init; }

    /// <summary>The name of the shape that holds the image.</summary>
    public string? Name { get; init; }

    /// <summary>The width in points.</summary>
    [Minimum(0)]
    public required double Width { get; init; }

    /// <summary>The height in points.</summary>
    [Minimum(0)]
    public required double Height { get; init; }
}

/// <summary>One table block.</summary>
public sealed record TableData
{
    /// <summary>The table's 1-based block.</summary>
    [Minimum(1)]
    public required int Block { get; init; }

    /// <summary>The number of rows.</summary>
    [Minimum(0)]
    public required int RowCount { get; init; }

    /// <summary>The number of cells in the widest row.</summary>
    [Minimum(0)]
    public required int ColumnCount { get; init; }

    /// <summary>
    /// The table style the table uses, accepted by insert_table's style; omitted for the default
    /// Table Normal style.
    /// </summary>
    [MinLength(1)]
    public string? Style { get; init; }
}

/// <summary>
/// Windowed block projection returned by <c>words query blocks</c>. Its <c>window</c> counts
/// blocks: the total is the blocks the range, section and scope select. <c>window.next</c>
/// starts again at a block whose content was truncated and doubles <c>--max-chars</c> when that
/// block alone exceeded the budget.
/// </summary>
[AlwaysPresent("window")]
public sealed record DocumentReadResult() : ResultEnvelope("document-read", 2)
{
    /// <summary>The kind of file read: always <c>document</c>.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "document";

    /// <summary>The document read.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    /// <summary>The projection, as <c>--scope</c> chose it.</summary>
    [AllowedValues(typeof(DocumentReadScopes))]
    public required string Scope { get; init; }

    /// <summary>The document's block count, whatever the range, section and scope select.</summary>
    [Minimum(0)]
    public required int BlockCount { get; init; }

    /// <summary>The blocks of this window, in document order.</summary>
    public required IReadOnlyList<BlockData> Blocks { get; init; }
}

/// <summary>A paragraph or table directly owned by a section body.</summary>
public sealed record BlockData
{
    /// <summary>The 1-based block number that operation block addresses accept.</summary>
    [Minimum(1)]
    public required int Block { get; init; }

    /// <summary>Whether the block is a paragraph or a table.</summary>
    [AllowedValues("paragraph", "table")]
    public required string Type { get; init; }

    /// <summary>The 1-based section that holds the block.</summary>
    [Minimum(1)]
    public required int Section { get; init; }

    /// <summary>
    /// The number Word draws before a numbered list paragraph, such as 6.2; bullets are not
    /// reported. The paragraph's text leaves it out; search snippets, find and heading addresses,
    /// extracted text and table cells read it before the text, followed by a space.
    /// </summary>
    public string? ListLabel { get; init; }

    /// <summary>A paragraph's visible text.</summary>
    public string? Text { get; init; }

    /// <summary>The name of a paragraph's style.</summary>
    public string? Style { get; init; }

    /// <summary>A heading paragraph's level, 1 for Heading 1.</summary>
    [Minimum(1)]
    public int? HeadingLevel { get; init; }

    /// <summary>
    /// The runs of the visible text, in order: their text joins to the paragraph's text, without
    /// field codes, deleted text or anchored comments and footnotes. Returned in full scope.
    /// </summary>
    public IReadOnlyList<RunData>? Runs { get; init; }

    /// <summary>The paragraph's indents, spacing and alignment, returned in full scope.</summary>
    public ParagraphFormatData? ParagraphFormat { get; init; }

    /// <summary>The images in a paragraph.</summary>
    public IReadOnlyList<BlockImageData>? Images { get; init; }

    /// <summary>
    /// The break that follows this block: a page break, including one that starts the next
    /// paragraph, or a section break after the last block of a section.
    /// </summary>
    [AllowedValues(typeof(WordsBreakKinds))]
    public string? BreakAfter { get; init; }

    /// <summary>A table's number of rows.</summary>
    [Minimum(0)]
    public int? RowCount { get; init; }

    /// <summary>The number of cells in a table's widest row.</summary>
    [Minimum(0)]
    public int? ColumnCount { get; init; }

    /// <summary>
    /// The text of each cell by row; a numbered list paragraph in a cell starts with its number
    /// and a space.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>>? Cells { get; init; }

    /// <summary>
    /// Returned document text shares the max-chars budget, including repeated projections.
    /// Re-read this item with a larger budget for omitted content.
    /// </summary>
    public bool ContentTruncated { get; init; }
}

/// <summary>One image in a paragraph block; its size is in points.</summary>
public sealed record BlockImageData
{
    /// <summary>The 1-based block that holds the image.</summary>
    [Minimum(1)]
    public required int Block { get; init; }

    /// <summary>The name of the shape that holds the image.</summary>
    public string? Name { get; init; }

    /// <summary>The width in points.</summary>
    [Minimum(0)]
    public required double Width { get; init; }

    /// <summary>The height in points.</summary>
    [Minimum(0)]
    public required double Height { get; init; }
}

/// <summary>
/// The format a paragraph has, from its own settings and its style, in points; returned in
/// full scope.
/// </summary>
public sealed record ParagraphFormatData
{
    /// <summary>Such as left, center, right, justify or distributed.</summary>
    public required string Alignment { get; init; }

    /// <summary>The left indent.</summary>
    public required double LeftIndent { get; init; }

    /// <summary>The right indent.</summary>
    public required double RightIndent { get; init; }

    /// <summary>The first line's indent; negative for a hanging indent.</summary>
    public required double FirstLineIndent { get; init; }

    /// <summary>The space before the paragraph.</summary>
    public required double SpaceBefore { get; init; }

    /// <summary>The space after the paragraph.</summary>
    public required double SpaceAfter { get; init; }

    /// <summary>How <c>lineSpacing</c> is measured.</summary>
    [AllowedValues("atLeast", "exactly", "multiple")]
    public required string LineSpacingRule { get; init; }

    /// <summary>Lines for the multiple rule, where 1 is single spacing; points otherwise.</summary>
    public required double LineSpacing { get; init; }
}

/// <summary>One run of a paragraph's visible text and its formatting, returned in full scope.</summary>
public sealed record RunData
{
    /// <summary>The run's text.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The East Asian font when the run starts with a Chinese, Japanese or Korean character,
    /// otherwise the Latin font.
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The font of the run's Latin text.</summary>
    public string? LatinFont { get; init; }

    /// <summary>The font of the run's Chinese, Japanese and Korean text.</summary>
    public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    public double? Size { get; init; }

    /// <summary>Whether the run is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Whether the run is italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>A #RRGGBB color.</summary>
    public string? Color { get; init; }
}

/// <summary>Result of <c>words convert</c>.</summary>
public sealed record WordsConvertResult() : ResultEnvelope("convert-result", 2)
{
    /// <summary>The converted document.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The written file.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The 1-based pages the output holds, as <c>--pages</c> selected them; absent for every page.</summary>
    public string? Pages { get; init; }
}

/// <summary>Result of <c>words render</c>.</summary>
public sealed record WordsRenderResult() : ResultEnvelope("render-result", 2)
{
    /// <summary>The rendered document.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>One written image per rendered page, in page order.</summary>
    [MinItems(1)]
    public required IReadOnlyList<PageOutput> Outputs { get; init; }

    /// <summary>The raster resolution of png and jpeg output; absent for svg.</summary>
    [Minimum(RenderPixelGuard.MinimumDpi)]
    [Maximum(RenderPixelGuard.MaximumDpi)]
    public int? Dpi { get; init; }
}

/// <summary>One rendered page.</summary>
public sealed record PageOutput
{
    /// <summary>The 1-based page.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The page's image file.</summary>
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of creating a document.</summary>
public sealed record WordsCreateResult() : ResultEnvelope("create-result", 2)
{
    /// <summary>The created document.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of a Words mutation.</summary>
public sealed record WordsEditResult() : ResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The edited document.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The written document; absent for a dry run.</summary>
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }

    /// <summary>Whether the batch was only checked, writing nothing.</summary>
    public required bool DryRun { get; init; }

    /// <summary>The outcome of each operation, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>The backup of the replaced file, when the edit wrote over an existing file.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>
    /// The output pages the batch changed: those of the blocks and sections the operations
    /// addressed or inserted, of replaced text, and of the content that took the place of
    /// removed blocks. An operation whose only target is document can change any page.
    /// </summary>
    [Minimum(1, Depth = 1)]
    [UniqueItems]
    public IReadOnlyList<int>? PagesTouched { get; init; }

    /// <summary>The checks run on the written document after reopening it.</summary>
    public WordsVerification? Verification { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static item => item.Status == OpStatuses.Failed)
        || Verification is { Ok: false };
}

/// <summary>Post-edit verification evidence.</summary>
public sealed record WordsVerification
{
    /// <summary>Whether the written document passed every check.</summary>
    public required bool Ok { get; init; }

    /// <summary>What the checks found.</summary>
    public required IReadOnlyList<VerificationIssue> Issues { get; init; }

    /// <summary>Whether the written document's content differs from the input's once both accept every tracked change.</summary>
    public bool? SemanticChangesDetected { get; init; }

    /// <summary>The number of fields in the written document.</summary>
    [Minimum(0)]
    public int? FieldCount { get; init; }

    /// <summary>The number of revisions the written document stores.</summary>
    [Minimum(0)]
    public int? RevisionCount { get; init; }

    /// <summary>
    /// The editing restriction: none, or the protect operation mode that sets it; every SDK
    /// protection type maps to one of these.
    /// </summary>
    [AllowedValues(typeof(WordsProtectionModes))]
    public string? Protection { get; init; }
}

/// <summary>Semantic comparison result.</summary>
public sealed record WordsCompareResult() : ResultEnvelope("compare-result", 2)
{
    /// <summary>The original document.</summary>
    public required SourceInfo Left { get; init; }

    /// <summary>The changed document.</summary>
    public required SourceInfo Right { get; init; }

    /// <summary>Whether the comparison found no revisions.</summary>
    public required bool Identical { get; init; }

    /// <summary>The revisions the comparison found, counted by type.</summary>
    public required RevisionCounts Revisions { get; init; }

    /// <summary>The first revisions the comparison found.</summary>
    public required IReadOnlyList<RevisionSample> Samples { get; init; }

    /// <summary>The redline document that tracks every revision; absent when none is written.</summary>
    public OutputInfo? Output { get; init; }
}

/// <summary>Counts by revision type.</summary>
public sealed record RevisionCounts
{
    /// <summary>The number of insertions.</summary>
    [Minimum(0)]
    public required int InsertionCount { get; init; }

    /// <summary>The number of deletions.</summary>
    [Minimum(0)]
    public required int DeletionCount { get; init; }

    /// <summary>The number of format changes.</summary>
    [Minimum(0)]
    public required int FormatChangeCount { get; init; }

    /// <summary>The number of moves.</summary>
    [Minimum(0)]
    public required int MoveCount { get; init; }
}

/// <summary>One revision the comparison found.</summary>
public sealed record RevisionSample
{
    /// <summary>The kind of change.</summary>
    [AllowedValues(typeof(WordsRevisionTypes))]
    public required string Type { get; init; }

    /// <summary>The text of the revision's run, cut to 300 characters and an ellipsis; absent for a paragraph mark.</summary>
    public string? Text { get; init; }
}

/// <summary>One window of search hits; its <c>window</c> counts hits and continues with <c>--skip</c>.</summary>
[AlwaysPresent("window")]
public sealed record WordsSearchResult() : ResultEnvelope("search-result", 2)
{
    /// <summary>The searched document.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Source { get; init; }

    /// <summary>The text or regular expression searched for.</summary>
    [JsonPropertyOrder(-49)]
    public required string Pattern { get; init; }

    /// <summary>The hits of this window, in document order.</summary>
    [JsonPropertyOrder(-48)]
    public required IReadOnlyList<WordsSearchHit> Hits { get; init; }
}

/// <summary>One search hit.</summary>
public sealed record WordsSearchHit
{
    /// <summary>The body block that holds the hit; absent in headers and footers.</summary>
    [Minimum(1)]
    public int? Block { get; init; }

    /// <summary>The section whose body, header or footer holds the hit.</summary>
    [Minimum(1)]
    public required int Section { get; init; }

    /// <summary>The story that holds the hit.</summary>
    [AllowedValues(typeof(WordsStoryScopes))]
    public required string Scope { get; init; }

    /// <summary>In a header or footer, which one holds the hit; absent elsewhere.</summary>
    [AllowedValues(typeof(HeaderFooterLocations))]
    public string? Location { get; init; }

    /// <summary>In a header or footer, its kind as set_header and set_footer name it; absent elsewhere.</summary>
    [AllowedValues(typeof(HeaderFooterKinds))]
    public string? Kind { get; init; }

    /// <summary>
    /// The text of the paragraph, comment or note that holds the hit, cut to 300 characters and
    /// an ellipsis.
    /// </summary>
    public required string Snippet { get; init; }
}

/// <summary>Result of splitting a document.</summary>
public sealed record WordsSplitResult() : ResultEnvelope("split-result", 2)
{
    /// <summary>The split document.</summary>
    public required SourceInfo Input { get; init; }

    /// <summary>The written parts, in document order.</summary>
    public required IReadOnlyList<SplitOutput> Outputs { get; init; }
}

/// <summary>One split output.</summary>
public sealed record SplitOutput
{
    /// <summary>The part's 1-based number.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The part's file.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>What of the document the part holds: <c>section-N</c>, <c>page-N</c> or <c>blocks-S-E</c>.</summary>
    public string? Source { get; init; }
}

/// <summary>Result of extracting assets.</summary>
public sealed record WordsExtractResult() : ResultEnvelope("extract-result", 2)
{
    /// <summary>The document extracted from.</summary>
    public required SourceInfo Input { get; init; }

    /// <summary>What was extracted, as <c>--what</c> named it.</summary>
    [AllowedValues(typeof(WordsExtractTargets))]
    public required string What { get; init; }

    /// <summary>The written files.</summary>
    public required IReadOnlyList<ExtractedItem> Items { get; init; }
}

/// <summary>One extracted file.</summary>
public sealed record ExtractedItem
{
    /// <summary>The written file's path.</summary>
    public required string Path { get; init; }

    /// <summary>What the file holds.</summary>
    [AllowedValues("image", "comments", "text", "table")]
    public required string Kind { get; init; }

    /// <summary>The file's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>The 1-based block the image or table comes from.</summary>
    [Minimum(1)]
    public int? Block { get; init; }
}
