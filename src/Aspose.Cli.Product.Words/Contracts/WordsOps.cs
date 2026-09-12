using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>A validated Words edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ProductJsonRoot]
public sealed record WordsOpsBatch : BoundedOperationEnvelope<WordsOp>;

/// <summary>Base of every Words operation.</summary>
[JsonConverter(typeof(Serialization.WordsOpJsonConverter))]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public abstract record WordsOp : BoundedOperation
{
    /// <summary>Stable wire discriminator written as the <c>op</c> property.</summary>
    [JsonIgnore]
    public abstract string OpName { get; }
}

/// <summary>A block, range, bookmark, heading or find anchor.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WordsTarget
{
    /// <summary>One canonical 1-based block.</summary>
    public int? Block { get; init; }

    /// <summary>A canonical block range expression.</summary>
    public string? Blocks { get; init; }

    /// <summary>A bookmark name.</summary>
    public string? Bookmark { get; init; }

    /// <summary>Visible heading text.</summary>
    public string? Heading { get; init; }

    /// <summary>Visible text used to locate a block.</summary>
    public string? Find { get; init; }

    /// <summary>Optional 1-based match occurrence for heading/find targets.</summary>
    public int? Nth { get; init; }
}

/// <summary>Replace literal or regex text within a bounded document scope.</summary>
public sealed record ReplaceTextOp(string Scope = "body") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "replace_text";
    public required string Find { get; init; }
    public required string Replace { get; init; }
    public bool Regex { get; init; }
    public bool MatchCase { get; init; }
    public bool WholeWord { get; init; }
    public int? MaxReplacements { get; init; }
}

/// <summary>Replace the inline content of paragraph blocks.</summary>
public sealed record SetTextOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_text";
    public required WordsTarget At { get; init; }
    public required string Text { get; init; }
}

/// <summary>Insert structured paragraphs at a block boundary.</summary>
public sealed record InsertParagraphsOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_paragraphs";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required IReadOnlyList<ParagraphInput> Paragraphs { get; init; }
}

/// <summary>One paragraph supplied to an insertion operation.</summary>
public sealed record ParagraphInput
{
    public required string Text { get; init; }
    public string? Style { get; init; }
    public int? ListLevel { get; init; }
}

/// <summary>Import Markdown at a block boundary.</summary>
public sealed record InsertMarkdownOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_markdown";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Markdown { get; init; }
}

/// <summary>Delete one or more canonical blocks.</summary>
public sealed record DeleteBlocksOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "delete_blocks";
    public required WordsTarget Target { get; init; }
}

/// <summary>Insert a page or section break.</summary>
public sealed record InsertBreakOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_break";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Kind { get; init; }
}

/// <summary>Insert a local image.</summary>
public sealed record InsertImageOp(bool Inline = true) : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_image";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Path { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
}

/// <summary>Insert a table at a block boundary.</summary>
public sealed record InsertTableOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_table";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required int Rows { get; init; }
    public required int Cols { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Data { get; init; }
    public string? Style { get; init; }
}

/// <summary>Replace one 1-based table cell.</summary>
public sealed record SetTableCellOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_table_cell";
    public required WordsTarget At { get; init; }
    public required int Row { get; init; }
    public required int Col { get; init; }
    public required string Text { get; init; }
}

/// <summary>Insert and update a table of contents.</summary>
public sealed record InsertTocOp(int MaxLevel = 3) : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_toc";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
}

/// <summary>Bookmark a paragraph's visible text.</summary>
public sealed record InsertBookmarkOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_bookmark";
    public required WordsTarget At { get; init; }
    public required string Name { get; init; }
}

/// <summary>Insert a hyperlink at a block boundary.</summary>
public sealed record InsertHyperlinkOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_hyperlink";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Text { get; init; }
    public required string Url { get; init; }
}

/// <summary>Insert a Word field at a block boundary.</summary>
public sealed record InsertFieldOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "insert_field";
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Code { get; init; }
}

/// <summary>Add a document section.</summary>
public sealed record AddSectionOp(string Position = "end") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "add_section";
    public int? After { get; init; }
    public PageSetupInput? PageSetup { get; init; }
}

/// <summary>Delete one section.</summary>
public sealed record DeleteSectionOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "delete_section";
    public required int Section { get; init; }
}

/// <summary>Apply page setup to one or all sections.</summary>
public sealed record SetPageSetupOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_page_setup";
    public int? Section { get; init; }
    public required PageSetupInput Setup { get; init; }
}

/// <summary>Page size, orientation, margins and column settings.</summary>
public sealed record PageSetupInput
{
    public string? Size { get; init; }
    public string? Orientation { get; init; }
    public MarginInput? Margins { get; init; }
    public int? Columns { get; init; }
}

/// <summary>Page margins in points.</summary>
public sealed record MarginInput
{
    public double? Top { get; init; }
    public double? Right { get; init; }
    public double? Bottom { get; init; }
    public double? Left { get; init; }
}

/// <summary>Replace a section header.</summary>
public sealed record SetHeaderOp(string Kind = "primary") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_header";
    public int? Section { get; init; }
    public IReadOnlyList<string>? Paragraphs { get; init; }
    public string? Markdown { get; init; }
}

/// <summary>Replace a section footer.</summary>
public sealed record SetFooterOp(string Kind = "primary") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_footer";
    public int? Section { get; init; }
    public IReadOnlyList<string>? Paragraphs { get; init; }
    public string? Markdown { get; init; }
}

/// <summary>Configure page-number fields.</summary>
public sealed record SetPageNumbersOp(string Location = "footer", string Alignment = "center") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_page_numbers";
    public int? Section { get; init; }
    public string? Format { get; init; }
    public int? Start { get; init; }
}

/// <summary>Apply character formatting to target runs.</summary>
public sealed record FormatTextOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "format_text";
    public required WordsTarget Target { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public bool? Underline { get; init; }
    public double? Size { get; init; }
    public string? Color { get; init; }
    public string? Font { get; init; }
    public string? Highlight { get; init; }
}

/// <summary>Apply an existing paragraph style.</summary>
public sealed record SetStyleOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_style";
    public required WordsTarget Target { get; init; }
    public required string Style { get; init; }
}

/// <summary>Create or update a named paragraph style.</summary>
public sealed record DefineStyleOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "define_style";
    public required string Name { get; init; }
    public string? BasedOn { get; init; }
    public string? Font { get; init; }
    public double? Size { get; init; }
    public bool? Bold { get; init; }
    public string? Color { get; init; }
    public double? SpaceBefore { get; init; }
    public double? SpaceAfter { get; init; }
}

/// <summary>Apply bullet or number list formatting.</summary>
public sealed record ApplyListOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "apply_list";
    public required WordsTarget Target { get; init; }
    public required string Kind { get; init; }
    public int Level { get; init; }
}

/// <summary>Update default paragraph and character-style fonts.</summary>
public sealed record SetDefaultFontOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_default_font";
    public required string Font { get; init; }
    public double? Size { get; init; }
}

/// <summary>Set built-in and custom document properties.</summary>
public sealed record SetPropertiesOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "set_properties";
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public string? Keywords { get; init; }
    public IReadOnlyDictionary<string, string?>? Custom { get; init; }
}

/// <summary>Add a text or image watermark.</summary>
public sealed record AddWatermarkOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "add_watermark";
    public string? Text { get; init; }
    public string? ImagePath { get; init; }
    public double? Opacity { get; init; }
    public string? Color { get; init; }
}

/// <summary>Remove the document watermark.</summary>
public sealed record RemoveWatermarkOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "remove_watermark";
}

/// <summary>Protect a document, optionally with an environment-sourced password.</summary>
public sealed record ProtectOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "protect";
    public required string Mode { get; init; }
    public string? PasswordEnv { get; init; }
}

/// <summary>Remove document protection.</summary>
public sealed record UnprotectOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "unprotect";
    public string? PasswordEnv { get; init; }
}

/// <summary>Accept revisions, optionally filtered by author.</summary>
public sealed record AcceptRevisionsOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "accept_revisions";
    public string? Author { get; init; }
}

/// <summary>Reject revisions, optionally filtered by author.</summary>
public sealed record RejectRevisionsOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "reject_revisions";
    public string? Author { get; init; }
}

/// <summary>Add a paragraph comment.</summary>
public sealed record AddCommentOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "add_comment";
    public required WordsTarget At { get; init; }
    public required string Author { get; init; }
    public required string Text { get; init; }
}

/// <summary>Remove all comments or comments by one author.</summary>
public sealed record RemoveCommentsOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "remove_comments";
    public string? Author { get; init; }
}

/// <summary>Append a local document.</summary>
public sealed record AppendDocumentOp(string ImportFormatMode = "keepSource") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "append_document";
    public required string Path { get; init; }
}

/// <summary>Run a simple or region mail merge.</summary>
public sealed record MailMergeOp : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "mail_merge";
    public string? Path { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, string?>>? Inline { get; init; }
    public bool Regions { get; init; }
}

/// <summary>Update TOC or all document fields and page layout.</summary>
public sealed record UpdateFieldsOp(string What = "all") : WordsOp
{
    /// <inheritdoc />
    public override string OpName => "update_fields";
}

/// <summary>Registry of the v2 Words operation vocabulary.</summary>
public static class WordsOps
{
    /// <summary>Stable discriminator-to-DTO registry.</summary>
    public static IReadOnlyDictionary<string, Type> Registry { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["replace_text"] = typeof(ReplaceTextOp),
        ["set_text"] = typeof(SetTextOp),
        ["insert_paragraphs"] = typeof(InsertParagraphsOp),
        ["insert_markdown"] = typeof(InsertMarkdownOp),
        ["delete_blocks"] = typeof(DeleteBlocksOp),
        ["insert_break"] = typeof(InsertBreakOp),
        ["insert_image"] = typeof(InsertImageOp),
        ["insert_table"] = typeof(InsertTableOp),
        ["set_table_cell"] = typeof(SetTableCellOp),
        ["insert_toc"] = typeof(InsertTocOp),
        ["insert_bookmark"] = typeof(InsertBookmarkOp),
        ["insert_hyperlink"] = typeof(InsertHyperlinkOp),
        ["insert_field"] = typeof(InsertFieldOp),
        ["add_section"] = typeof(AddSectionOp),
        ["delete_section"] = typeof(DeleteSectionOp),
        ["set_page_setup"] = typeof(SetPageSetupOp),
        ["set_header"] = typeof(SetHeaderOp),
        ["set_footer"] = typeof(SetFooterOp),
        ["set_page_numbers"] = typeof(SetPageNumbersOp),
        ["format_text"] = typeof(FormatTextOp),
        ["set_style"] = typeof(SetStyleOp),
        ["define_style"] = typeof(DefineStyleOp),
        ["apply_list"] = typeof(ApplyListOp),
        ["set_default_font"] = typeof(SetDefaultFontOp),
        ["set_properties"] = typeof(SetPropertiesOp),
        ["add_watermark"] = typeof(AddWatermarkOp),
        ["remove_watermark"] = typeof(RemoveWatermarkOp),
        ["protect"] = typeof(ProtectOp),
        ["unprotect"] = typeof(UnprotectOp),
        ["accept_revisions"] = typeof(AcceptRevisionsOp),
        ["reject_revisions"] = typeof(RejectRevisionsOp),
        ["add_comment"] = typeof(AddCommentOp),
        ["remove_comments"] = typeof(RemoveCommentsOp),
        ["append_document"] = typeof(AppendDocumentOp),
        ["mail_merge"] = typeof(MailMergeOp),
        ["update_fields"] = typeof(UpdateFieldsOp),
    };

    /// <summary>All public operation names in ordinal order.</summary>
    public static IReadOnlyList<string> Names { get; } = Registry.Keys.Order(StringComparer.Ordinal).ToArray();
}
