using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>A validated Words edit batch.</summary>
[ProductJsonRoot]
public sealed record WordsOpsBatch : BoundedOperationEnvelope<WordsOp>;

/// <summary>
/// The operation document of <c>words edit --ops</c>. Every block address resolves against the
/// document as it was before the first operation, so an insertion does not shift a later
/// operation's anchor.
/// </summary>
[OperationVocabulary("ops", JsonContext = typeof(WordsOpsJsonContext), Addresses = new[] { "at", "position", "target", "section", "path", "passwordEnv", "sourcePasswordEnv" })]
[JsonConverter(typeof(OperationJsonConverter<WordsOp>))]
public abstract partial record WordsOp : BoundedOperation;

/// <summary>A block address: exactly one of block, blocks, bookmark, heading or find.</summary>
[ExactlyOneOf("block", "blocks", "bookmark", "heading", "find")]
public sealed record WordsTarget
{
    /// <summary>One canonical 1-based block.</summary>
    [Minimum(1)] public int? Block { get; init; }

    /// <summary>Canonical 1-based blocks and block ranges.</summary>
    [PageRange] public string? Blocks { get; init; }

    /// <summary>A bookmark name.</summary>
    [MinLength(1)] public string? Bookmark { get; init; }

    /// <summary>Visible heading text, matched case-insensitively.</summary>
    [MinLength(1)] public string? Heading { get; init; }

    /// <summary>Visible text that locates a block, matched case-insensitively.</summary>
    [MinLength(1)] public string? Find { get; init; }

    /// <summary>The 1-based match of a heading or find address; the first when omitted.</summary>
    [Minimum(1)] public int? Nth { get; init; }
}

/// <summary>Which side of the anchor block the new content goes.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class WordsPositionAttribute() : AllowedValuesAttribute("before", "after")
{
    public override string? Definition => "position";
}

/// <summary>One paragraph supplied to an insertion operation.</summary>
public sealed record WordsParagraphInput
{
    /// <summary>The paragraph text.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/text")]
    public required string Text { get; init; }

    /// <summary>
    /// An existing paragraph style. Without it, the paragraph continues the paragraph before the
    /// insertion point, or with list the existing item at that level it joins, as Word's Enter does:
    /// its indents, spacing, alignment and the run format of its last text, or after a heading
    /// the heading style's next style.
    /// </summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/style")]
    [MinLength(1)] public string? Style { get; init; }

    /// <summary>Makes the paragraph a list item at the list's 1-based level. When the anchor is a list item, it joins the anchor's list if that list defines the level, else the list of the nearest item at this level under the same parent, and without style continues that list's nearest existing item at this level, if any, with the indents it sets on itself. When the anchor is no list item, or no such list exists, it joins one new list of the list's kind, bullets by default. A header or footer paragraph takes no list.</summary>
    [MistakenFor("listLevel")]
    public WordsListInput? List { get; init; }
}

/// <summary>The list a paragraph belongs to, as a read reports it.</summary>
public sealed record WordsListInput
{
    /// <summary>The kind of a new list the paragraph starts; a list it joins keeps its own.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsListData/properties/kind")]
    [AllowedValues("bullet", "number")] public string? Kind { get; init; }

    /// <summary>The paragraph's 1-based level in the list.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsListData/properties/level")]
    [Minimum(1), Maximum(9)] public required int Level { get; init; }
}

/// <summary>Page size, orientation, margins and column settings; omitted settings keep their values.</summary>
public sealed record WordsPageSetupInput
{
    /// <summary>The page size: a preset, or its width and height in points; the orientation decides which side is long, so a landscape page is wider than it is high.</summary>
    [MistakenFor("size")]
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/pageSize")]
    public PageSizeInput? PageSize { get; init; }

    /// <summary>Whether the pages are upright (portrait) or sideways (landscape).</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/orientation")]
    [AllowedValues(typeof(WordsPageOrientations))] public string? Orientation { get; init; }

    /// <summary>The page margins; only the sides present change.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/margins")]
    public WordsMarginInput? Margins { get; init; }

    /// <summary>The number of text columns on each page.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/columnCount")]
    [Minimum(1)] public int? ColumnCount { get; init; }
}

/// <summary>Page margins in points; omitted margins keep their values.</summary>
public sealed record WordsMarginInput
{
    /// <summary>The distance from the top edge of the page to the text.</summary>
    [Minimum(0)] public double? Top { get; init; }

    /// <summary>The distance from the right edge of the page to the text.</summary>
    [Minimum(0)] public double? Right { get; init; }

    /// <summary>The distance from the bottom edge of the page to the text.</summary>
    [Minimum(0)] public double? Bottom { get; init; }

    /// <summary>The distance from the left edge of the page to the text.</summary>
    [Minimum(0)] public double? Left { get; init; }
}

/// <summary>Replaces literal or regular-expression text within a document scope.</summary>
[Operation("replace_text")]
public sealed record ReplaceTextOp : WordsOp
{
    /// <summary>The text to find, or the pattern when regex is true.</summary>
    [NotState]
    [MinLength(1)] public required string Find { get; init; }

    /// <summary>With regex, .NET substitutions such as $1 and ${name} apply; write $$ for a literal $.</summary>
    [MistakenFor("replacement", "replaceWith")]
    [NotState]
    public required string Replace { get; init; }

    /// <summary>Whether find is a .NET regular expression rather than literal text.</summary>
    [NotState]
    public bool Regex { get; init; }

    /// <summary>Whether a match must have the same letter case as find.</summary>
    [MistakenFor("matchCase")]
    [NotState]
    public bool CaseSensitive { get; init; }

    /// <summary>Whether a match must be a whole word, not part of a longer one.</summary>
    [NotState]
    public bool WholeWord { get; init; }

    /// <summary>
    /// body is the main text without the comments and footnotes it anchors; footnotes include
    /// endnotes. Field codes and text a tracked change deletes are never replaced.
    /// </summary>
    [NotState]
    [AllowedValues(typeof(WordsTextScopes))] public string Scope { get; init; } = WordsTextScopes.Body;

    /// <summary>The most matches replaced; every match when omitted.</summary>
    [NotState]
    [Minimum(1)] public int? MaxReplacementCount { get; init; }
}

/// <summary>Replaces the inline content of paragraph blocks, or the text a bookmark encloses.</summary>
[Operation("set_text")]
public sealed record SetTextOp : WordsOp
{
    /// <summary>The paragraph blocks, or the bookmark, whose text is replaced.</summary>
    [MistakenFor("at")]
    public required WordsTarget Target { get; init; }

    /// <summary>The new text, in the font of the first text it replaces; a paragraph keeps its own format.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/text")]
    public required string Text { get; init; }
}

/// <summary>Inserts structured paragraphs at a block boundary.</summary>
[Operation("insert_paragraphs")]
public sealed record InsertParagraphsOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The paragraphs to insert, in order.</summary>
    [MinItems(1)] public required IReadOnlyList<WordsParagraphInput> Paragraphs { get; init; }
}

/// <summary>Imports Markdown at a block boundary.</summary>
[Operation("insert_markdown")]
public sealed record InsertMarkdownOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The Markdown to import; its paragraphs and tables take the document's styles.</summary>
    [NotState]
    [MinLength(1)] public required string Markdown { get; init; }
}

/// <summary>Deletes one or more canonical blocks.</summary>
[Operation("delete_blocks")]
public sealed record DeleteBlocksOp : WordsOp
{
    /// <summary>The blocks to delete.</summary>
    public required WordsTarget Target { get; init; }
}

/// <summary>Inserts a page break, or splits the section at a block boundary.</summary>
[Operation("insert_break")]
public sealed record InsertBreakOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>page starts a new page; section splits the section there, the new section starting with the same page setup, headers and footers.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBreakData/properties/kind")]
    [AllowedValues(typeof(WordsBreakKinds))] public required string Kind { get; init; }
}

/// <summary>Inserts a local image in a new paragraph.</summary>
[Operation("insert_image")]
public sealed record InsertImageOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The image, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>The width in points; the image's own when omitted.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockImageData/properties/width")]
    [ExclusiveMinimum(0)] public double? Width { get; init; }

    /// <summary>The height in points; the image's own when omitted.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockImageData/properties/height")]
    [ExclusiveMinimum(0)] public double? Height { get; init; }

    /// <summary>Whether the image sits in the text line rather than floating with square wrapping.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockImageData/properties/inline")]
    public bool Inline { get; init; } = true;
}

/// <summary>Inserts a table at a block boundary; values must fit the table's rows and columns.</summary>
[Operation("insert_table")]
public sealed record InsertTableOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The row count.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/rowCount")]
    [Minimum(1), Maximum(32_767)] public required int RowCount { get; init; }

    /// <summary>The column count; Word allows at most 63.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/columnCount")]
    [Minimum(1), Maximum(63)] public required int ColumnCount { get; init; }

    /// <summary>Cell text by row, then column; cells beyond it stay empty.</summary>
    [MistakenFor("cells")]
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/values")]
    public IReadOnlyList<IReadOnlyList<string>>? Values { get; init; }

    /// <summary>An existing table style.</summary>
    [MistakenFor("style")]
    [ReadBack("inspect-result.schema.json#/$defs/wordsTableData/properties/tableStyle")]
    [MinLength(1)] public string? TableStyle { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(Values is null || Values.Count <= RowCount, "values has more rows than the table");
        OperationInvalidException.Require(Values is null || Values.All(row => row.Count <= ColumnCount), "values has more columns than the table");
        return this;
    }
}

/// <summary>Replaces the text of one cell of a table block.</summary>
[Operation("set_table_cell")]
public sealed record SetTableCellOp : WordsOp
{
    /// <summary>The table block.</summary>
    [MistakenFor("at")]
    public required WordsTarget Target { get; init; }

    /// <summary>The 1-based row of the cell.</summary>
    [NotState]
    [Minimum(1)] public required int Row { get; init; }

    /// <summary>The 1-based column of the cell.</summary>
    [MistakenFor("col")]
    [NotState]
    [Minimum(1)] public required int Column { get; init; }

    /// <summary>The new cell text, which replaces the cell's text.</summary>
    [ReadBackByPosition] // Reads back as cells[row][column] of the table block.
    public required string Text { get; init; }
}

/// <summary>
/// Repeats one template row of a table block once per item, in order, and removes the template
/// row. Each copy keeps the row's formatting and replaces every <c>{{key}}</c> placeholder with
/// the item's value as literal text in the placeholder's formatting; an item without a key for
/// one of the row's placeholders fails the operation.
/// </summary>
[Operation("repeat_table_row")]
[ExactlyOneOf("items", "path")]
public sealed record RepeatTableRowOp : WordsOp
{
    /// <summary>The table block that holds the template row.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>The 1-based template row; when omitted, the table's one row that contains a <c>{{key}}</c> placeholder.</summary>
    [NotState]
    [Minimum(1)] public int? Row { get; init; }

    /// <summary>
    /// Placeholder values by key, one object per row copy, read as a data file's are: a number
    /// or Boolean as its JSON text and null as empty text. An empty list removes the template row.
    /// </summary>
    [NotState]
    [JsonScalar] public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Items { get; init; }

    /// <summary>A JSON array of flat objects, or a CSV file with a header row, relative to the working directory.</summary>
    [InputPath] public string? Path { get; init; }
}

/// <summary>
/// Sets how one table block breaks across pages; omitted settings keep their values.
/// keepTogether true also stops rows breaking, so it cannot be combined with
/// allowRowBreakAcrossPages true.
/// </summary>
[Operation("set_table_pagination")]
[MistakenFor("format_table")]
[AtLeastOneOf("keepTogether", "allowRowBreakAcrossPages", "headerRowCount", "keepWithNext")]
public sealed record SetTablePaginationOp : WordsOp
{
    /// <summary>The table block.</summary>
    [MistakenFor("at")]
    public required WordsTarget Target { get; init; }

    /// <summary>
    /// true keeps the whole table on one page when it fits: no row breaks across pages, and
    /// every paragraph keeps with the next except the last paragraph of each last-row cell.
    /// false clears keep-with-next on those same paragraphs and leaves the row setting.
    /// </summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsTableData/properties/keepTogether")]
    public bool? KeepTogether { get; init; }

    /// <summary>Whether the text of each row may split across a page break.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsTableData/properties/allowRowBreakAcrossPages")]
    public bool? AllowRowBreakAcrossPages { get; init; }

    /// <summary>The first rows repeated as a heading on every page the table spans; 0 clears it. At most the table's row count.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsTableData/properties/headerRowCount")]
    [Minimum(0)] public int? HeaderRowCount { get; init; }

    /// <summary>Whether the last paragraph of each last-row cell keeps with the paragraph after the table, keeping the table with it.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsTableData/properties/keepWithNext")]
    public bool? KeepWithNext { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(
            KeepTogether is not true || AllowRowBreakAcrossPages is not true,
            "keepTogether true stops rows breaking across pages and cannot be combined with allowRowBreakAcrossPages true");
        return this;
    }
}

/// <summary>Inserts and updates a table of contents.</summary>
[Operation("insert_toc")]
public sealed record InsertTocOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The deepest heading level listed.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsFieldData/properties/maxLevel")]
    [Minimum(1), Maximum(9)] public int MaxLevel { get; init; } = 3;
}

/// <summary>Bookmarks a paragraph's visible text.</summary>
[Operation("insert_bookmark")]
public sealed record InsertBookmarkOp : WordsOp
{
    /// <summary>The paragraph block whose visible text the bookmark encloses.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>The bookmark name.</summary>
    [ReadBackByPosition] // Reads back as an entry of the document's bookmarks list.
    [MinLength(1)] public required string Name { get; init; }
}

/// <summary>Inserts a hyperlink in a new paragraph.</summary>
[Operation("insert_hyperlink")]
public sealed record InsertHyperlinkOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The link text shown in the document.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/text")]
    public required string Text { get; init; }

    /// <summary>The address a click opens.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/url")]
    [WebLink] public required string Url { get; init; }
}

/// <summary>Inserts a Word field in a new paragraph.</summary>
[Operation("insert_field")]
public sealed record InsertFieldOp : WordsOp
{
    /// <summary>The block the new content goes next to.</summary>
    public required WordsTarget At { get; init; }

    /// <summary>Whether the new content goes before or after the at block.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>The field code, such as DATE.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsFieldData/properties/code")]
    [MinLength(1)] public required string Code { get; init; }
}

/// <summary>Inserts an empty section before or after a section.</summary>
[Operation("insert_section")]
[MistakenFor("add_section")]
public sealed record InsertSectionOp : WordsOp
{
    /// <summary>The 1-based section number the new section goes before or after, as position says.</summary>
    [MistakenFor("after")]
    [Minimum(1)] public required int At { get; init; }

    /// <summary>Whether the new section goes before or after section at.</summary>
    [WordsPosition] public required string Position { get; init; }

    /// <summary>Page setup applied over the neighbouring section's.</summary>
    public WordsPageSetupInput? PageSetup { get; init; }
}

/// <summary>Deletes one section; a document keeps at least one.</summary>
[Operation("delete_section")]
public sealed record DeleteSectionOp : WordsOp
{
    /// <summary>The 1-based section to delete, with its content.</summary>
    [Minimum(1)] public required int Section { get; init; }
}

/// <summary>Applies page setup to one section, or to every section; omitted settings keep their values.</summary>
[Operation("set_page_setup")]
[AtLeastOneOf("pageSize", "orientation", "margins", "columnCount")]
public sealed record SetPageSetupOp : WordsOp
{
    /// <summary>The 1-based section; every section when omitted.</summary>
    [Minimum(1)] public int? Section { get; init; }

    /// <summary>The page size: a preset, or its width and height in points; the orientation decides which side is long, so a landscape page is wider than it is high.</summary>
    [MistakenFor("size", "setup")]
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/pageSize")]
    public PageSizeInput? PageSize { get; init; }

    /// <summary>Whether the pages are upright (portrait) or sideways (landscape).</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/orientation")]
    [AllowedValues(typeof(WordsPageOrientations))] public string? Orientation { get; init; }

    /// <summary>The page margins; only the sides present change.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/margins")]
    public WordsMarginInput? Margins { get; init; }

    /// <summary>The number of text columns on each page.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsSectionData/properties/columnCount")]
    [Minimum(1)] public int? ColumnCount { get; init; }

    /// <summary>The settings as one page setup.</summary>
    internal WordsPageSetupInput Setup => new() { PageSize = PageSize, Orientation = Orientation, Margins = Margins, ColumnCount = ColumnCount };
}

/// <summary>Replaces one kind of header or footer with paragraphs or with Markdown.</summary>
[ExactlyOneOf("paragraphs", "markdown")]
public abstract record HeaderFooterOp : WordsOp
{
    /// <summary>The 1-based section; every section when omitted.</summary>
    [Minimum(1)] public int? Section { get; init; }

    /// <summary>Which pages it shows on; first and even also turn on the section setting that shows it.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsHeaderFooterData/properties/kind")]
    [AllowedValues(typeof(WordsHeaderFooterKinds))] public string Kind { get; init; } = WordsHeaderFooterKinds.Primary;

    /// <summary>The paragraphs, one per item, in the paragraph and character format of the first paragraph they replace, such as its alignment, or in Word's Header or Footer style when there is none; a style given replaces that paragraph format.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsHeaderFooterData/properties/paragraphs")]
    public IReadOnlyList<WordsParagraphInput>? Paragraphs { get; init; }

    /// <summary>Markdown for the content instead of paragraphs; its paragraphs and tables take the document's styles.</summary>
    [NotState]
    public string? Markdown { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(
            Paragraphs?.All(static paragraph => paragraph.List is null) != false,
            "a header or footer paragraph takes no list",
            "Give the paragraph text and a style; insert list items in the body with insert_paragraphs.");
        return this;
    }
}

/// <summary>Replaces one kind of section header with paragraphs or with Markdown.</summary>
[Operation("set_header")]
public sealed record SetHeaderOp : HeaderFooterOp;

/// <summary>Replaces one kind of section footer with paragraphs or with Markdown.</summary>
[Operation("set_footer")]
public sealed record SetFooterOp : HeaderFooterOp;

/// <summary>Adds or aligns a PAGE field in the primary header or footer and sets page numbering.</summary>
[Operation("set_page_numbers")]
public sealed record SetPageNumbersOp : WordsOp
{
    /// <summary>The 1-based section; every section when omitted.</summary>
    [Minimum(1)] public int? Section { get; init; }

    /// <summary>Whether the number goes in the primary header or the primary footer.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsPageNumbersData/properties/location")]
    [AllowedValues(typeof(WordsHeaderFooterLocations))] public string Location { get; init; } = WordsHeaderFooterLocations.Footer;

    /// <summary>The alignment of the paragraph that holds the number.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsPageNumbersData/properties/alignment")]
    [AllowedValues("left", "center", "right")] public string Alignment { get; init; } = "center";

    /// <summary>How the page numbers are written; the section's numbering keeps its style when omitted.</summary>
    [MistakenFor("format")]
    [ReadBack("inspect-result.schema.json#/$defs/wordsPageNumbersData/properties/numberStyle")]
    [AllowedValues(typeof(PageNumberStyles))] public string? NumberStyle { get; init; }

    /// <summary>The number of the section's first page; numbering continues when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsPageNumbersData/properties/start")]
    [Minimum(0)] public int? Start { get; init; }
}

/// <summary>
/// Applies character formatting to the text of target blocks, or with match only to the text it
/// matches there; omitted settings keep their values. Regex, caseSensitive and wholeWord true need match.
/// </summary>
[Operation("format_text")]
[MistakenFor("highlight_text")]
public sealed record FormatTextOp : WordsOp
{
    /// <summary>The blocks whose text is formatted.</summary>
    public required WordsTarget Target { get; init; }

    /// <summary>
    /// The text to format within the target blocks, or the pattern when regex is true, matched in
    /// the visible text 'aspose-cli words search' reads, each paragraph with the text boxes it anchors; every
    /// match within the text of its runs is formatted and the rest of the text keeps its format.
    /// Without it, all the text of the target blocks is formatted. Field codes and text a tracked
    /// change deletes are never matched.
    /// </summary>
    [NotState]
    [MinLength(1)] public string? Match { get; init; }

    /// <summary>Whether match is a .NET regular expression rather than literal text.</summary>
    [NotState]
    public bool Regex { get; init; }

    /// <summary>Whether a match must have the same letter case as match.</summary>
    [MistakenFor("matchCase")]
    [NotState]
    public bool CaseSensitive { get; init; }

    /// <summary>Whether a match must be a whole word, not part of a longer one.</summary>
    [NotState]
    public bool WholeWord { get; init; }

    /// <summary>Whether the text is bold; false removes bold.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/bold")]
    public bool? Bold { get; init; }

    /// <summary>Whether the text is italic; false removes italic.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/italic")]
    public bool? Italic { get; init; }

    /// <summary>Whether the text has a single underline; false removes any underline.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/underline")]
    public bool? Underline { get; init; }

    /// <summary>The font size in points.</summary>
    [MistakenFor("size")]
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/fontSize")]
    [ExclusiveMinimum(0)] public double? FontSize { get; init; }

    /// <summary>The text color, as #RRGGBB.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/color")]
    [HexColor] public string? Color { get; init; }

    /// <summary>The font of Latin and East Asian text.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/font")]
    [MinLength(1)] public string? Font { get; init; }

    /// <summary>The font of Latin text, in place of font's.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/latinFont")]
    [MinLength(1)] public string? LatinFont { get; init; }

    /// <summary>The font of Chinese, Japanese and Korean text, in place of font's.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/eastAsianFont")]
    [MinLength(1)] public string? EastAsianFont { get; init; }

    /// <summary>The highlight, a color of Word's highlight palette, the only highlights a Word document stores.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsRunData/properties/highlight")]
    [AllowedValues(typeof(WordsHighlightColors))] public string? Highlight { get; init; }

    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(
            Match is not null || !(Regex || CaseSensitive || WholeWord),
            "regex, caseSensitive and wholeWord true need match, the text to match",
            "Give match the text or pattern, or drop regex, caseSensitive and wholeWord.");
        return this;
    }
}

/// <summary>Applies an existing paragraph style to target blocks: the typeface, size and color set on their runs give way to the style, while emphasis such as bold, italic or underline set on a run stays.</summary>
[Operation("set_style")]
public sealed record SetStyleOp : WordsOp
{
    /// <summary>The blocks that take the style.</summary>
    public required WordsTarget Target { get; init; }

    /// <summary>The name of an existing paragraph style, such as Heading 1.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsBlockData/properties/style")]
    [MinLength(1)] public required string Style { get; init; }
}

/// <summary>Creates or updates a named paragraph style; omitted settings keep their values.</summary>
[Operation("define_style")]
public sealed record DefineStyleOp : WordsOp
{
    /// <summary>The style name; the style of that name is updated, or a new paragraph style created when there is none.</summary>
    [NotState]
    [MinLength(1)] public required string Name { get; init; }

    /// <summary>The existing style this one inherits every setting it does not set from.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/basedOn")]
    [MinLength(1)] public string? BasedOn { get; init; }

    /// <summary>The font of Latin and East Asian text.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/font")]
    [MinLength(1)] public string? Font { get; init; }

    /// <summary>The font of Latin text, in place of font's.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/latinFont")]
    [MinLength(1)] public string? LatinFont { get; init; }

    /// <summary>The font of Chinese, Japanese and Korean text, in place of font's.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/eastAsianFont")]
    [MinLength(1)] public string? EastAsianFont { get; init; }

    /// <summary>The font size in points.</summary>
    [MistakenFor("size")]
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/fontSize")]
    [ExclusiveMinimum(0)] public double? FontSize { get; init; }

    /// <summary>Whether the style's text is bold.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/bold")]
    public bool? Bold { get; init; }

    /// <summary>The text color, as #RRGGBB.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/color")]
    [HexColor] public string? Color { get; init; }

    /// <summary>Space before the paragraph, in points.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/spaceBefore")]
    [Minimum(0)] public double? SpaceBefore { get; init; }

    /// <summary>Space after the paragraph, in points.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/spaceAfter")]
    [Minimum(0)] public double? SpaceAfter { get; init; }
}

/// <summary>Makes the paragraphs of target blocks one new bullet or numbered list.</summary>
[Operation("set_list")]
[MistakenFor("apply_list")]
public sealed record SetListOp : WordsOp
{
    /// <summary>The blocks whose paragraphs become the list's items.</summary>
    public required WordsTarget Target { get; init; }

    /// <summary>Whether the items show bullets or numbers.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsListData/properties/kind")]
    [AllowedValues("bullet", "number")] public required string Kind { get; init; }

    /// <summary>The 1-based list level.</summary>
    [ReadBack("read-result.schema.json#/$defs/wordsListData/properties/level")]
    [Minimum(1), Maximum(9)] public int Level { get; init; } = 1;
}

/// <summary>Sets the font of every paragraph and character style.</summary>
[Operation("set_default_font")]
[AtLeastOneOf("font", "latinFont", "eastAsianFont")]
public sealed record SetDefaultFontOp : WordsOp
{
    /// <summary>The font of Latin and East Asian text.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/font")]
    [MinLength(1)] public string? Font { get; init; }

    /// <summary>The font of Latin text, in place of font's.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/latinFont")]
    [MinLength(1)] public string? LatinFont { get; init; }

    /// <summary>The font of Chinese, Japanese and Korean text, in place of font's.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/eastAsianFont")]
    [MinLength(1)] public string? EastAsianFont { get; init; }

    /// <summary>The font size in points; sizes are kept when omitted.</summary>
    [MistakenFor("size")]
    [ReadBack("inspect-result.schema.json#/$defs/wordsStyleData/properties/fontSize")]
    [ExclusiveMinimum(0)] public double? FontSize { get; init; }
}

/// <summary>Sets built-in and custom document properties; omitted properties keep their values.</summary>
[Operation("set_properties")]
public sealed record SetPropertiesOp : WordsOp
{
    /// <summary>The document title.</summary>
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Title { get; init; }

    /// <summary>The document author.</summary>
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Author { get; init; }

    /// <summary>The document subject.</summary>
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Subject { get; init; }

    /// <summary>The document keywords, as one text such as "budget, 2026".</summary>
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Keywords { get; init; }

    /// <summary>
    /// Custom properties by name; a null value removes the property. <c>words inspect --detail
    /// properties</c> reports each under its name as it is, and a custom property a document
    /// already has under a standard key's name as <c>custom:</c> and that name. A name
    /// must not be title, author, subject or keywords in any case, which the fields above set, and
    /// must not start with <c>custom:</c>.
    /// </summary>
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public IReadOnlyDictionary<string, string?>? Custom { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        foreach (string key in Custom?.Keys ?? [])
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new OperationInvalidException(
                    "custom must not name a property with a blank name",
                    "Give every custom property a name.");
            }

            if (new[] { "title", "author", "subject", "keywords" }.FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) is { } standard)
            {
                throw new OperationInvalidException(
                    $"custom must not name the standard property '{key}'",
                    $"Set it with the {standard} field instead.");
            }

            if (key.StartsWith("custom:", StringComparison.Ordinal))
            {
                throw new OperationInvalidException(
                    $"custom must not name '{key}': the prefix 'custom:' marks a read key, not a name",
                    "Name the property without the 'custom:' prefix.");
            }
        }

        return this;
    }
}

/// <summary>Sets the text or image watermark, replacing any the document has; color and font apply to a text watermark only.</summary>
[Operation("set_watermark")]
[MistakenFor("add_watermark")]
[ExactlyOneOf("text", "path")]
[DependentRequired("color", "text")]
[DependentRequired("font", "text")]
public sealed record SetWatermarkOp : WordsOp
{
    /// <summary>The watermark text; Word holds at most 200 characters.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsWatermarkData/properties/text")]
    [MinLength(1), MaxLength(200)] public string? Text { get; init; }

    /// <summary>The watermark image, relative to the working directory.</summary>
    [MistakenFor("imagePath")]
    [NotState]
    [InputPath] public string? Path { get; init; }

    /// <summary>Semi-transparent text or a washed-out image.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsWatermarkData/properties/faded")]
    public bool Faded { get; init; } = true;

    /// <summary>The text color, as #RRGGBB.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsWatermarkData/properties/color")]
    [HexColor] public string? Color { get; init; }

    /// <summary>The text font; when omitted, East Asian text takes the default East Asian font of a Chinese, Japanese or Korean document, or else Microsoft YaHei.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsWatermarkData/properties/font")]
    [MinLength(1)] public string? Font { get; init; }
}

/// <summary>Deletes the document watermark.</summary>
[Operation("delete_watermark")]
[MistakenFor("remove_watermark")]
public sealed record DeleteWatermarkOp : WordsOp;

/// <summary>Restricts editing of the document.</summary>
[Operation("protect_document")]
[MistakenFor("protect")]
public sealed record ProtectDocumentOp : WordsOp
{
    /// <summary>The editing restriction.</summary>
    [MistakenFor("mode")]
    [AllowedValues(typeof(WordsProtectionModes))]
    [ReadBack("inspect-result.schema.json#/$defs/wordsDocumentSummary/properties/protection")]
    public required string Protection { get; init; }

    /// <summary>The environment variable that holds the protection password; no password when omitted.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Removes document protection.</summary>
[Operation("unprotect_document")]
[MistakenFor("unprotect")]
public sealed record UnprotectDocumentOp : WordsOp
{
    /// <summary>The environment variable that holds the protection password.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>
/// Decides tracked revisions: all, one author's, or the listed changes; author and revisions
/// cannot be combined.
/// </summary>
public abstract record RevisionDecisionOp : WordsOp
{
    /// <summary>The author whose revisions are decided; a name no revision has changes nothing and reports OP_NO_EFFECT.</summary>
    [NotState]
    [MinLength(1)] public string? Author { get; init; }

    /// <summary>
    /// The 1-based revision numbers of the changes <c>words inspect --detail revisions</c> lists,
    /// numbered as the document was before the batch. Such a decision comes before every
    /// operation other than a revision decision, and lists together the changes that share a
    /// node and type, as its error names them.
    /// </summary>
    [NotState]
    [MinItems(1), Minimum(1)] public IReadOnlyList<int>? Revisions { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(Author is null || Revisions is null, "author and revisions cannot be combined");
        return this;
    }
}

/// <summary>Accepts tracked revisions: all, one author's, or the listed changes; author and revisions cannot be combined.</summary>
[Operation("accept_revisions")]
public sealed record AcceptRevisionsOp : RevisionDecisionOp;

/// <summary>Rejects tracked revisions: all, one author's, or the listed changes; author and revisions cannot be combined.</summary>
[Operation("reject_revisions")]
public sealed record RejectRevisionsOp : RevisionDecisionOp;

/// <summary>Adds a comment on a paragraph block.</summary>
[Operation("add_comment")]
public sealed record AddCommentOp : WordsOp
{
    /// <summary>The paragraph block the comment is anchored to.</summary>
    [MistakenFor("at")]
    public required WordsTarget Target { get; init; }

    /// <summary>The comment author's name.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsCommentData/properties/author")]
    [MinLength(1)] public required string Author { get; init; }

    /// <summary>The comment text.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/wordsCommentData/properties/text")]
    public required string Text { get; init; }
}

/// <summary>Deletes comments: all, or one author's.</summary>
[Operation("delete_comments")]
[MistakenFor("remove_comments")]
public sealed record DeleteCommentsOp : WordsOp
{
    /// <summary>The author whose comments are deleted; a name no comment has changes nothing and reports OP_NO_EFFECT.</summary>
    [NotState]
    [MinLength(1)] public string? Author { get; init; }
}

/// <summary>Appends the sections of a local document, each with the headers and footers it defines; a kind it does not define continues from the section before it.</summary>
[Operation("import_sections")]
[MistakenFor("append_document")]
public sealed record ImportSectionsOp : WordsOp
{
    /// <summary>The document, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>Which definition a style both documents name takes: the imported document's (keep-source) or this document's (use-destination). Headers and footers are not styles and come with their sections either way.</summary>
    [MistakenFor("importFormatMode")]
    [NotState]
    [AllowedValues(typeof(FormattingModes))] public string Formatting { get; init; } = FormattingModes.KeepSource;

    /// <summary>The environment variable that holds the source document's open password, for an encrypted source.</summary>
    [MistakenFor("passwordEnv")]
    [NotState]
    [SecretEnv] public string? SourcePasswordEnv { get; init; }
}

/// <summary>Runs a mail merge from a data file or items: one copy of the document per row, or one region repeated per row.</summary>
[Operation("mail_merge")]
[ExactlyOneOf("path", "items")]
public sealed record MailMergeOp : WordsOp
{
    /// <summary>A JSON array of flat objects, or a CSV file with a header row, relative to the working directory; it must hold at least one row.</summary>
    [InputPath] public string? Path { get; init; }

    /// <summary>Merge rows of field values by field name, read as a data file's are: a number or Boolean merges as its JSON text.</summary>
    [MistakenFor("inline")]
    [NotState]
    [MinItems(1), JsonScalar] public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Items { get; init; }

    /// <summary>Whether the rows repeat the template's one TableStart/TableEnd region.</summary>
    [NotState]
    public bool Regions { get; init; }
}

/// <summary>Refreshes the tables of contents, or every field and the page layout.</summary>
[Operation("refresh_fields")]
[MistakenFor("update_fields")]
public sealed record RefreshFieldsOp : WordsOp
{
    /// <summary>toc refreshes the tables of contents; all refreshes every field and the page layout.</summary>
    [NotState]
    [AllowedValues("all", "toc")] public string What { get; init; } = "all";
}

/// <summary>Source-generated serialization metadata for Words operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AcceptRevisionsOp))]
[JsonSerializable(typeof(AddCommentOp))]
[JsonSerializable(typeof(InsertSectionOp))]
[JsonSerializable(typeof(SetWatermarkOp))]
[JsonSerializable(typeof(ImportSectionsOp))]
[JsonSerializable(typeof(SetListOp))]
[JsonSerializable(typeof(DefineStyleOp))]
[JsonSerializable(typeof(DeleteBlocksOp))]
[JsonSerializable(typeof(DeleteSectionOp))]
[JsonSerializable(typeof(SetTablePaginationOp))]
[JsonSerializable(typeof(FormatTextOp))]
[JsonSerializable(typeof(InsertBookmarkOp))]
[JsonSerializable(typeof(InsertBreakOp))]
[JsonSerializable(typeof(InsertFieldOp))]
[JsonSerializable(typeof(InsertHyperlinkOp))]
[JsonSerializable(typeof(InsertImageOp))]
[JsonSerializable(typeof(InsertMarkdownOp))]
[JsonSerializable(typeof(InsertParagraphsOp))]
[JsonSerializable(typeof(InsertTableOp))]
[JsonSerializable(typeof(InsertTocOp))]
[JsonSerializable(typeof(MailMergeOp))]
[JsonSerializable(typeof(ProtectDocumentOp))]
[JsonSerializable(typeof(RejectRevisionsOp))]
[JsonSerializable(typeof(DeleteCommentsOp))]
[JsonSerializable(typeof(DeleteWatermarkOp))]
[JsonSerializable(typeof(RepeatTableRowOp))]
[JsonSerializable(typeof(ReplaceTextOp))]
[JsonSerializable(typeof(SetDefaultFontOp))]
[JsonSerializable(typeof(SetFooterOp))]
[JsonSerializable(typeof(SetHeaderOp))]
[JsonSerializable(typeof(SetPageNumbersOp))]
[JsonSerializable(typeof(SetPageSetupOp))]
[JsonSerializable(typeof(SetPropertiesOp))]
[JsonSerializable(typeof(SetStyleOp))]
[JsonSerializable(typeof(SetTableCellOp))]
[JsonSerializable(typeof(SetTextOp))]
[JsonSerializable(typeof(UnprotectDocumentOp))]
[JsonSerializable(typeof(RefreshFieldsOp))]
internal sealed partial class WordsOpsJsonContext : JsonSerializerContext;
