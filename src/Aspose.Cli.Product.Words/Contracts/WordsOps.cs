using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>A validated Words edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ProductJsonRoot]
public sealed record WordsOpsBatch : BoundedOperationEnvelope<WordsOp>;

/// <summary>
/// The operation document of <c>words edit --ops</c>. Every block address resolves against the
/// document as it was before the first operation, so an insertion does not shift a later
/// operation's anchor.
/// </summary>
[OperationVocabulary(WordsSchemaIds.Ops, MaximumOperations = 256, JsonContext = typeof(WordsOpsJsonContext))]
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
public sealed record ParagraphInput
{
    public required string Text { get; init; }

    /// <summary>An existing paragraph style.</summary>
    [MinLength(1)] public string? Style { get; init; }

    /// <summary>Makes the paragraph a list item at this level: in the anchor's list when the anchor is a list item, otherwise in one new bullet list.</summary>
    [Minimum(0), Maximum(8)] public int? ListLevel { get; init; }
}

/// <summary>Page size, orientation, margins and column settings; omitted settings keep their values.</summary>
public sealed record PageSetupInput
{
    [AllowedValues("a3", "a4", "a5", "letter", "legal")] public string? Size { get; init; }

    [AllowedValues("portrait", "landscape")] public string? Orientation { get; init; }

    public MarginInput? Margins { get; init; }

    [Minimum(1)] public int? Columns { get; init; }
}

/// <summary>Page margins in points; omitted margins keep their values.</summary>
public sealed record MarginInput
{
    [Minimum(0)] public double? Top { get; init; }

    [Minimum(0)] public double? Right { get; init; }

    [Minimum(0)] public double? Bottom { get; init; }

    [Minimum(0)] public double? Left { get; init; }
}

/// <summary>Replaces literal or regular-expression text within a document scope.</summary>
[Operation("replace_text")]
public sealed record ReplaceTextOp : WordsOp
{
    [MinLength(1)] public required string Find { get; init; }

    /// <summary>With regex, .NET substitutions such as $1 and ${name} apply; write $$ for a literal $.</summary>
    public required string Replace { get; init; }

    /// <summary>Whether find is a .NET regular expression rather than literal text.</summary>
    public bool Regex { get; init; }

    public bool MatchCase { get; init; }

    public bool WholeWord { get; init; }

    /// <summary>
    /// body is the main text without the comments and footnotes it anchors; footnotes include
    /// endnotes. Field codes and text a tracked change deletes are never replaced.
    /// </summary>
    [AllowedValues(typeof(WordsTextScopes))] public string Scope { get; init; } = WordsTextScopes.Body;

    /// <summary>The most matches replaced; every match when omitted.</summary>
    [Minimum(1)] public int? MaxReplacements { get; init; }
}

/// <summary>Replaces the inline content of paragraph blocks, or the text a bookmark encloses.</summary>
[Operation("set_text")]
public sealed record SetTextOp : WordsOp
{
    public required WordsTarget At { get; init; }

    public required string Text { get; init; }
}

/// <summary>Inserts structured paragraphs at a block boundary.</summary>
[Operation("insert_paragraphs")]
public sealed record InsertParagraphsOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    [MinItems(1)] public required IReadOnlyList<ParagraphInput> Paragraphs { get; init; }
}

/// <summary>Imports Markdown at a block boundary.</summary>
[Operation("insert_markdown")]
public sealed record InsertMarkdownOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    [MinLength(1)] public required string Markdown { get; init; }
}

/// <summary>Deletes one or more canonical blocks.</summary>
[Operation("delete_blocks")]
public sealed record DeleteBlocksOp : WordsOp
{
    public required WordsTarget Target { get; init; }
}

/// <summary>Inserts a page break, or splits the section at a block boundary.</summary>
[Operation("insert_break")]
public sealed record InsertBreakOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    [AllowedValues("page", "section")] public required string Kind { get; init; }
}

/// <summary>Inserts a local image in a new paragraph.</summary>
[Operation("insert_image")]
public sealed record InsertImageOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    /// <summary>The image, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>The width in points; the image's own when omitted.</summary>
    [ExclusiveMinimum(0)] public double? Width { get; init; }

    /// <summary>The height in points; the image's own when omitted.</summary>
    [ExclusiveMinimum(0)] public double? Height { get; init; }

    /// <summary>Whether the image sits in the text line rather than floating with square wrapping.</summary>
    public bool Inline { get; init; } = true;
}

/// <summary>Inserts a table at a block boundary; data must fit the table's rows and columns.</summary>
[Operation("insert_table")]
public sealed record InsertTableOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    [Minimum(1), Maximum(32_767)] public required int Rows { get; init; }

    /// <summary>The column count; Word allows at most 63.</summary>
    [Minimum(1), Maximum(63)] public required int Cols { get; init; }

    /// <summary>Cell text by row, then column; cells beyond it stay empty.</summary>
    public IReadOnlyList<IReadOnlyList<string>>? Data { get; init; }

    /// <summary>An existing table style.</summary>
    [MinLength(1)] public string? Style { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(Data is null || Data.Count <= Rows, "data has more rows than the table");
        OperationInvalidException.Require(Data is null || Data.All(row => row.Count <= Cols), "data has more columns than the table");
        return this;
    }
}

/// <summary>Replaces the text of one cell of a table block.</summary>
[Operation("set_table_cell")]
public sealed record SetTableCellOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [Minimum(1)] public required int Row { get; init; }

    [Minimum(1)] public required int Col { get; init; }

    public required string Text { get; init; }
}

/// <summary>Inserts and updates a table of contents.</summary>
[Operation("insert_toc")]
public sealed record InsertTocOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    /// <summary>The deepest heading level listed.</summary>
    [Minimum(1), Maximum(9)] public int MaxLevel { get; init; } = 3;
}

/// <summary>Bookmarks a paragraph's visible text.</summary>
[Operation("insert_bookmark")]
public sealed record InsertBookmarkOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [MinLength(1)] public required string Name { get; init; }
}

/// <summary>Inserts a hyperlink in a new paragraph.</summary>
[Operation("insert_hyperlink")]
public sealed record InsertHyperlinkOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    public required string Text { get; init; }

    [WebLink] public required string Url { get; init; }
}

/// <summary>Inserts a Word field in a new paragraph.</summary>
[Operation("insert_field")]
public sealed record InsertFieldOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [WordsPosition] public required string Position { get; init; }

    /// <summary>The field code, such as DATE.</summary>
    [MinLength(1)] public required string Code { get; init; }
}

/// <summary>Adds an empty section at the start, the end or after a section.</summary>
[Operation("add_section")]
[PresentWhen("after", "position", "after")]
public sealed record AddSectionOp : WordsOp
{
    [AllowedValues("start", "end", "after")] public string Position { get; init; } = "end";

    /// <summary>The 1-based section the new section follows; set exactly when position is after.</summary>
    [Minimum(1)] public int? After { get; init; }

    /// <summary>Page setup applied over the neighbouring section's.</summary>
    public PageSetupInput? PageSetup { get; init; }
}

/// <summary>Deletes one section; a document keeps at least one.</summary>
[Operation("delete_section")]
public sealed record DeleteSectionOp : WordsOp
{
    [Minimum(1)] public required int Section { get; init; }
}

/// <summary>Applies page setup to one section, or to every section.</summary>
[Operation("set_page_setup")]
public sealed record SetPageSetupOp : WordsOp
{
    /// <summary>The 1-based section; every section when omitted.</summary>
    [Minimum(1)] public int? Section { get; init; }

    public required PageSetupInput Setup { get; init; }
}

/// <summary>Replaces one kind of header or footer with paragraphs or with Markdown.</summary>
[ExactlyOneOf("paragraphs", "markdown")]
public abstract record HeaderFooterOp : WordsOp
{
    /// <summary>The 1-based section; every section when omitted.</summary>
    [Minimum(1)] public int? Section { get; init; }

    /// <summary>Which pages it shows on; first and even also turn on the section setting that shows it.</summary>
    [AllowedValues("primary", "first", "even")] public string Kind { get; init; } = "primary";

    /// <summary>Plain paragraphs, one per item.</summary>
    public IReadOnlyList<string>? Paragraphs { get; init; }

    public string? Markdown { get; init; }
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

    [AllowedValues("header", "footer")] public string Location { get; init; } = "footer";

    [AllowedValues("left", "center", "right")] public string Alignment { get; init; } = "center";

    [AllowedValues("decimal", "upperRoman", "lowerRoman", "upperLetter", "lowerLetter")] public string? Format { get; init; }

    /// <summary>The number of the section's first page; numbering continues when omitted.</summary>
    [Minimum(0)] public int? Start { get; init; }
}

/// <summary>Applies character formatting to the runs of target blocks; omitted settings keep their values.</summary>
[Operation("format_text")]
public sealed record FormatTextOp : WordsOp
{
    public required WordsTarget Target { get; init; }

    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public bool? Underline { get; init; }

    /// <summary>The font size in points.</summary>
    [ExclusiveMinimum(0)] public double? Size { get; init; }

    /// <summary>A #RRGGBB or named color.</summary>
    public string? Color { get; init; }

    [MinLength(1)] public string? Font { get; init; }

    /// <summary>A #RRGGBB or named highlight color.</summary>
    public string? Highlight { get; init; }
}

/// <summary>Applies an existing paragraph style to target blocks.</summary>
[Operation("set_style")]
public sealed record SetStyleOp : WordsOp
{
    public required WordsTarget Target { get; init; }

    [MinLength(1)] public required string Style { get; init; }
}

/// <summary>Creates or updates a named paragraph style; omitted settings keep their values.</summary>
[Operation("define_style")]
public sealed record DefineStyleOp : WordsOp
{
    [MinLength(1)] public required string Name { get; init; }

    [MinLength(1)] public string? BasedOn { get; init; }

    [MinLength(1)] public string? Font { get; init; }

    /// <summary>The font size in points.</summary>
    [ExclusiveMinimum(0)] public double? Size { get; init; }

    public bool? Bold { get; init; }

    /// <summary>A #RRGGBB or named color.</summary>
    public string? Color { get; init; }

    /// <summary>Space before the paragraph, in points.</summary>
    [Minimum(0)] public double? SpaceBefore { get; init; }

    /// <summary>Space after the paragraph, in points.</summary>
    [Minimum(0)] public double? SpaceAfter { get; init; }
}

/// <summary>Makes the paragraphs of target blocks one new bullet or numbered list.</summary>
[Operation("apply_list")]
public sealed record ApplyListOp : WordsOp
{
    public required WordsTarget Target { get; init; }

    [AllowedValues("bullet", "number")] public required string Kind { get; init; }

    [Minimum(0), Maximum(8)] public int Level { get; init; }
}

/// <summary>Sets the font of every paragraph and character style.</summary>
[Operation("set_default_font")]
public sealed record SetDefaultFontOp : WordsOp
{
    [MinLength(1)] public required string Font { get; init; }

    /// <summary>The font size in points; sizes are kept when omitted.</summary>
    [ExclusiveMinimum(0)] public double? Size { get; init; }
}

/// <summary>Sets built-in and custom document properties; omitted properties keep their values.</summary>
[Operation("set_properties")]
public sealed record SetPropertiesOp : WordsOp
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Subject { get; init; }

    public string? Keywords { get; init; }

    /// <summary>Custom properties by name; a null value removes the property.</summary>
    public IReadOnlyDictionary<string, string?>? Custom { get; init; }
}

/// <summary>Adds a text or image watermark; color applies to a text watermark only.</summary>
[Operation("add_watermark")]
[ExactlyOneOf("text", "imagePath")]
[DependentRequired("color", "text")]
public sealed record AddWatermarkOp : WordsOp
{
    /// <summary>The watermark text; Word holds at most 200 characters.</summary>
    [MinLength(1), MaxLength(200)] public string? Text { get; init; }

    /// <summary>The watermark image, relative to the working directory.</summary>
    [InputPath] public string? ImagePath { get; init; }

    /// <summary>Semi-transparent text or a washed-out image.</summary>
    public bool Faded { get; init; } = true;

    /// <summary>A #RRGGBB or named text color.</summary>
    public string? Color { get; init; }
}

/// <summary>Removes the document watermark.</summary>
[Operation("remove_watermark")]
public sealed record RemoveWatermarkOp : WordsOp;

/// <summary>Restricts editing of the document.</summary>
[Operation("protect")]
public sealed record ProtectOp : WordsOp
{
    [AllowedValues("readOnly", "comments", "trackedChanges", "forms")] public required string Mode { get; init; }

    /// <summary>The environment variable that holds the protection password; no password when omitted.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Removes document protection.</summary>
[Operation("unprotect")]
public sealed record UnprotectOp : WordsOp
{
    /// <summary>The environment variable that holds the protection password.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Accepts tracked revisions: all, or one author's.</summary>
[Operation("accept_revisions")]
public sealed record AcceptRevisionsOp : WordsOp
{
    [MinLength(1)] public string? Author { get; init; }
}

/// <summary>Rejects tracked revisions: all, or one author's.</summary>
[Operation("reject_revisions")]
public sealed record RejectRevisionsOp : WordsOp
{
    [MinLength(1)] public string? Author { get; init; }
}

/// <summary>Adds a comment on a paragraph block.</summary>
[Operation("add_comment")]
public sealed record AddCommentOp : WordsOp
{
    public required WordsTarget At { get; init; }

    [MinLength(1)] public required string Author { get; init; }

    public required string Text { get; init; }
}

/// <summary>Removes comments: all, or one author's.</summary>
[Operation("remove_comments")]
public sealed record RemoveCommentsOp : WordsOp
{
    [MinLength(1)] public string? Author { get; init; }
}

/// <summary>Appends a local document.</summary>
[Operation("append_document")]
public sealed record AppendDocumentOp : WordsOp
{
    /// <summary>The document, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    [AllowedValues("keepSource", "useDestination")] public string ImportFormatMode { get; init; } = "keepSource";
}

/// <summary>Runs a mail merge from a data file or inline rows: one copy of the document per row, or one region repeated per row.</summary>
[Operation("mail_merge")]
[ExactlyOneOf("path", "inline")]
public sealed record MailMergeOp : WordsOp
{
    /// <summary>A JSON array of flat objects, or a CSV file with a header row, relative to the working directory.</summary>
    [InputPath] public string? Path { get; init; }

    /// <summary>Merge rows of field values by field name.</summary>
    [MinItems(1)] public IReadOnlyList<IReadOnlyDictionary<string, string?>>? Inline { get; init; }

    /// <summary>Whether the rows repeat the template's one TableStart/TableEnd region.</summary>
    public bool Regions { get; init; }
}

/// <summary>Updates the tables of contents, or every field and the page layout.</summary>
[Operation("update_fields")]
public sealed record UpdateFieldsOp : WordsOp
{
    [AllowedValues("all", "toc")] public string What { get; init; } = "all";
}

/// <summary>Source-generated serialization metadata for Words operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AcceptRevisionsOp))]
[JsonSerializable(typeof(AddCommentOp))]
[JsonSerializable(typeof(AddSectionOp))]
[JsonSerializable(typeof(AddWatermarkOp))]
[JsonSerializable(typeof(AppendDocumentOp))]
[JsonSerializable(typeof(ApplyListOp))]
[JsonSerializable(typeof(DefineStyleOp))]
[JsonSerializable(typeof(DeleteBlocksOp))]
[JsonSerializable(typeof(DeleteSectionOp))]
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
[JsonSerializable(typeof(ProtectOp))]
[JsonSerializable(typeof(RejectRevisionsOp))]
[JsonSerializable(typeof(RemoveCommentsOp))]
[JsonSerializable(typeof(RemoveWatermarkOp))]
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
[JsonSerializable(typeof(UnprotectOp))]
[JsonSerializable(typeof(UpdateFieldsOp))]
internal sealed partial class WordsOpsJsonContext : JsonSerializerContext;
