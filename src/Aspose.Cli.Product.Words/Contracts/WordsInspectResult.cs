using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Structural information returned by <c>words inspect</c>.</summary>
public sealed record WordsInspectResult() : EngineResultEnvelope("inspect-result", 2)
{
    /// <summary>The inspected document.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>The summary every inspect returns, whatever details it asks for.</summary>
    public required WordsDocumentSummary Document { get; init; }

    /// <summary>Each section's page setup and headers and footers; returned with <c>--detail sections</c>.</summary>
    public IReadOnlyList<WordsSectionData>? Sections { get; init; }

    /// <summary>The headings in document order; returned with <c>--detail outline</c>.</summary>
    public IReadOnlyList<WordsOutlineItem>? Outline { get; init; }

    /// <summary>The styles the document defines, in ordinal order of their names; returned with <c>--detail styles</c>.</summary>
    public IReadOnlyList<WordsStyleData>? Styles { get; init; }

    /// <summary>The fields in every story; returned with <c>--detail fields</c>.</summary>
    public IReadOnlyList<WordsFieldData>? Fields { get; init; }

    /// <summary>The bookmark names, in ordinal order; returned with <c>--detail bookmarks</c>.</summary>
    public IReadOnlyList<string>? Bookmarks { get; init; }

    /// <summary>The comments, replies included; returned with <c>--detail comments</c>.</summary>
    public IReadOnlyList<WordsCommentData>? Comments { get; init; }

    /// <summary>
    /// The tracked changes in document order, one entry per change; returned with
    /// <c>--detail revisions</c>. A replacement is a deletion followed by an insertion.
    /// document.revisionCount counts the revisions the document stores, one per run, paragraph
    /// mark or other changed node, so it can exceed the entries here.
    /// </summary>
    public IReadOnlyList<WordsRevisionData>? Revisions { get; init; }

    /// <summary>The embedded images in every story; returned with <c>--detail images</c>.</summary>
    public IReadOnlyList<WordsImageData>? Images { get; init; }

    /// <summary>The tables; returned with <c>--detail tables</c>.</summary>
    public IReadOnlyList<WordsTableData>? Tables { get; init; }

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
public sealed record WordsDocumentSummary
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
    public required bool HasRevisions { get; init; }

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
    /// The editing restriction, as the protect operation mode that sets it; every SDK protection
    /// type maps to one of these. Absent for a document without one.
    /// </summary>
    [AllowedValues(typeof(WordsProtectionModes))]
    public string? Protection { get; init; }

    /// <summary>Whether the document carries a digital signature.</summary>
    public required bool Signed { get; init; }

    /// <summary>
    /// Whether the document holds macros (a VBA project); an output other than docm, dotm, doc,
    /// dot or wordml drops them and reports MACROS_DROPPED.
    /// </summary>
    public required bool HasMacros { get; init; }

    /// <summary>The watermark the document's headers draw; absent without one.</summary>
    public WordsWatermarkData? Watermark { get; init; }
}

/// <summary>A watermark, as set_watermark sets it.</summary>
public sealed record WordsWatermarkData
{
    /// <summary>Whether it draws text or an image.</summary>
    [AllowedValues("text", "image")]
    public required string Kind { get; init; }

    /// <summary>A text watermark's text.</summary>
    public string? Text { get; init; }

    /// <summary>A text watermark's font.</summary>
    public string? Font { get; init; }

    /// <summary>A text watermark's color, as #RRGGBB.</summary>
    public string? Color { get; init; }

    /// <summary>Whether the text is semi-transparent or the image washed out.</summary>
    public required bool Faded { get; init; }
}

/// <summary>One style the document defines, under the names define_style sets it with.</summary>
public sealed record WordsStyleData
{
    /// <summary>The style's name.</summary>
    public required string Name { get; init; }

    /// <summary>What the style formats.</summary>
    [AllowedValues("paragraph", "character", "table", "list")]
    public required string Kind { get; init; }

    /// <summary>The style it is based on; absent for none.</summary>
    public string? BasedOn { get; init; }

    /// <summary>The font of its Latin and East Asian text.</summary>
    public string? Font { get; init; }

    /// <summary>The font of its Latin text.</summary>
    public string? LatinFont { get; init; }

    /// <summary>The font of its Chinese, Japanese and Korean text.</summary>
    public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    public double? FontSize { get; init; }

    /// <summary>Whether its text is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Its text color, as #RRGGBB; absent for automatic.</summary>
    public string? Color { get; init; }

    /// <summary>The space before a paragraph of the style, in points.</summary>
    public double? SpaceBefore { get; init; }

    /// <summary>The space after a paragraph of the style, in points.</summary>
    public double? SpaceAfter { get; init; }
}

/// <summary>One document section.</summary>
public sealed record WordsSectionData
{
    /// <summary>The 1-based section number that section operations accept.</summary>
    [Minimum(1)]
    public required int Section { get; init; }

    /// <summary>The orientation of the section's pages.</summary>
    [AllowedValues(typeof(WordsPageOrientations))]
    public required string Orientation { get; init; }

    /// <summary>The size of the section's pages as laid out, wider than high in landscape, and the preset it matches.</summary>
    public required PageSizeInfo PageSize { get; init; }

    /// <summary>The page margins.</summary>
    public required WordsMarginData Margins { get; init; }

    /// <summary>The number of text columns.</summary>
    [Minimum(1)]
    public required int ColumnCount { get; init; }

    /// <summary>The section's page numbering: its style, its first number when it restarts, and the PAGE field that shows it.</summary>
    public required WordsPageNumbersData PageNumbers { get; init; }

    /// <summary>
    /// The headers and footers the section defines itself; a kind it does not define continues
    /// from the previous section.
    /// </summary>
    public required IReadOnlyList<WordsHeaderFooterData> HeadersFooters { get; init; }
}

/// <summary>
/// One header or footer of a section, named as set_header, set_footer and set_page_numbers
/// name it: its <c>location</c> and <c>kind</c>, and its visible text as <c>paragraphs</c>.
/// </summary>
public sealed record WordsHeaderFooterData
{
    /// <summary>Whether it is a header or a footer, as set_page_numbers names it.</summary>
    [AllowedValues(typeof(WordsHeaderFooterLocations))]
    public required string Location { get; init; }

    /// <summary>Which pages it shows on, as set_header and set_footer name it.</summary>
    [AllowedValues(typeof(WordsHeaderFooterKinds))]
    public required string Kind { get; init; }

    /// <summary>Each paragraph's visible text, style and list, as set_header and set_footer take them.</summary>
    public required IReadOnlyList<WordsParagraphData> Paragraphs { get; init; }
}

/// <summary>A paragraph of a header or footer.</summary>
public sealed record WordsParagraphData
{
    /// <summary>
    /// The visible text; a field whose result changes from page to page, such as PAGE or
    /// NUMPAGES, reads as its placeholder, such as {PAGE}.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>The name of the paragraph's style.</summary>
    public string? Style { get; init; }

    /// <summary>The list it belongs to; absent for a paragraph in none.</summary>
    public WordsListData? List { get; init; }
}

/// <summary>Page margins in points.</summary>
public sealed record WordsMarginData
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
public sealed record WordsOutlineItem
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
public sealed record WordsFieldData
{
    /// <summary>The SDK's name of the field type, such as FieldDate.</summary>
    public required string Kind { get; init; }

    /// <summary>The story that holds the field, named as <c>words search</c> names its scopes.</summary>
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

    /// <summary>The deepest heading level a table of contents lists, as insert_toc maxLevel sets it; absent for other fields.</summary>
    [Minimum(1)]
    public int? MaxLevel { get; init; }
}

/// <summary>One comment.</summary>
public sealed record WordsCommentData
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
/// reject_revisions accept, its kind, author, the date the document records and the text it
/// inserts, deletes or moves.
/// </summary>
public sealed record WordsRevisionData
{
    /// <summary>
    /// The 1-based number of the change in this list, which the revisions of accept_revisions
    /// and reject_revisions accept.
    /// </summary>
    [Minimum(1)]
    public required int Revision { get; init; }

    /// <summary>The kind of change.</summary>
    [AllowedValues(typeof(WordsRevisionKinds))]
    public required string Kind { get; init; }

    /// <summary>The change's author.</summary>
    public required string Author { get; init; }

    /// <summary>
    /// The date and time the document records for the change, reported as UTC; absent when none
    /// is recorded. Word stores the author's local time in this field, so a date Word wrote can
    /// be off from the true UTC by the author's time zone offset.
    /// </summary>
    public DateTimeOffset? Date { get; init; }

    /// <summary>
    /// The story that holds the change, named as <c>words search</c> names its scopes; absent for
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
    /// The text an insertion adds, a deletion removes or a move relocates, each paragraph break
    /// in it as a line feed; a longer text is cut to 300 characters and an ellipsis; absent for
    /// format and style definition changes.
    /// </summary>
    public string? Text { get; init; }
}

/// <summary>One embedded image in any story; its size is in points.</summary>
public sealed record WordsImageData
{
    /// <summary>The story that holds the image, named as <c>words search</c> names its scopes.</summary>
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
public sealed record WordsTableData
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

    /// <summary>The table style the table uses, Table Normal by default, accepted by insert_table's tableStyle.</summary>
    [MinLength(1)]
    public required string TableStyle { get; init; }

    /// <summary>Whether the whole table keeps on one page when it fits, as set_table_pagination keepTogether sets it.</summary>
    public required bool KeepTogether { get; init; }

    /// <summary>Whether the text of every row may split across a page break.</summary>
    public required bool AllowRowBreakAcrossPages { get; init; }

    /// <summary>The first rows repeated as a heading on every page the table spans.</summary>
    [Minimum(0)]
    public required int HeaderRowCount { get; init; }

    /// <summary>Whether the table keeps with the paragraph after it.</summary>
    public required bool KeepWithNext { get; init; }
}

/// <summary>A section's page numbering, as set_page_numbers sets it.</summary>
public sealed record WordsPageNumbersData
{
    /// <summary>Whether the primary header or, without one there, the primary footer holds the PAGE field; absent when neither does.</summary>
    [AllowedValues(typeof(WordsHeaderFooterLocations))]
    public string? Location { get; init; }

    /// <summary>The alignment of the paragraph that holds the PAGE field; absent without one.</summary>
    [AllowedValues("left", "center", "right")]
    public string? Alignment { get; init; }

    /// <summary>How the page numbers are written.</summary>
    [AllowedValues(typeof(PageNumberStyles))]
    public string? NumberStyle { get; init; }

    /// <summary>The number of the section's first page when its numbering restarts; absent when it continues.</summary>
    [Minimum(0)]
    public int? Start { get; init; }
}
