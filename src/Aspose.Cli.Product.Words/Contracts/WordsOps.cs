using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>A validated Words edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ProductJsonRoot]
public sealed record WordsOpsBatch : BoundedOperationEnvelope<WordsOp>;

/// <summary>Base of every Words operation.</summary>
[JsonConverter(typeof(Serialization.WordsOpJsonConverter))]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public abstract record WordsOp : BoundedOperation;

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
    public required WordsTarget At { get; init; }
    public required string Text { get; init; }
}

/// <summary>Insert structured paragraphs at a block boundary.</summary>
public sealed record InsertParagraphsOp : WordsOp
{
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
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Markdown { get; init; }
}

/// <summary>Delete one or more canonical blocks.</summary>
public sealed record DeleteBlocksOp : WordsOp
{
    public required WordsTarget Target { get; init; }
}

/// <summary>Insert a page or section break.</summary>
public sealed record InsertBreakOp : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Kind { get; init; }
}

/// <summary>Insert a local image.</summary>
public sealed record InsertImageOp(bool Inline = true) : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Path { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
}

/// <summary>Insert a table at a block boundary.</summary>
public sealed record InsertTableOp : WordsOp
{
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
    public required WordsTarget At { get; init; }
    public required int Row { get; init; }
    public required int Col { get; init; }
    public required string Text { get; init; }
}

/// <summary>Insert and update a table of contents.</summary>
public sealed record InsertTocOp(int MaxLevel = 3) : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
}

/// <summary>Bookmark a paragraph's visible text.</summary>
public sealed record InsertBookmarkOp : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Name { get; init; }
}

/// <summary>Insert a hyperlink at a block boundary.</summary>
public sealed record InsertHyperlinkOp : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Text { get; init; }
    public required string Url { get; init; }
}

/// <summary>Insert a Word field at a block boundary.</summary>
public sealed record InsertFieldOp : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Position { get; init; }
    public required string Code { get; init; }
}

/// <summary>Add a document section.</summary>
public sealed record AddSectionOp(string Position = "end") : WordsOp
{
    public int? After { get; init; }
    public PageSetupInput? PageSetup { get; init; }
}

/// <summary>Delete one section.</summary>
public sealed record DeleteSectionOp : WordsOp
{
    public required int Section { get; init; }
}

/// <summary>Apply page setup to one or all sections.</summary>
public sealed record SetPageSetupOp : WordsOp
{
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
    public int? Section { get; init; }
    public IReadOnlyList<string>? Paragraphs { get; init; }
    public string? Markdown { get; init; }
}

/// <summary>Replace a section footer.</summary>
public sealed record SetFooterOp(string Kind = "primary") : WordsOp
{
    public int? Section { get; init; }
    public IReadOnlyList<string>? Paragraphs { get; init; }
    public string? Markdown { get; init; }
}

/// <summary>Configure page-number fields.</summary>
public sealed record SetPageNumbersOp(string Location = "footer", string Alignment = "center") : WordsOp
{
    public int? Section { get; init; }
    public string? Format { get; init; }
    public int? Start { get; init; }
}

/// <summary>Apply character formatting to target runs.</summary>
public sealed record FormatTextOp : WordsOp
{
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
    public required WordsTarget Target { get; init; }
    public required string Style { get; init; }
}

/// <summary>Create or update a named paragraph style.</summary>
public sealed record DefineStyleOp : WordsOp
{
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
    public required WordsTarget Target { get; init; }
    public required string Kind { get; init; }
    public int Level { get; init; }
}

/// <summary>Update default paragraph and character-style fonts.</summary>
public sealed record SetDefaultFontOp : WordsOp
{
    public required string Font { get; init; }
    public double? Size { get; init; }
}

/// <summary>Set built-in and custom document properties.</summary>
public sealed record SetPropertiesOp : WordsOp
{
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public string? Keywords { get; init; }
    public IReadOnlyDictionary<string, string?>? Custom { get; init; }
}

/// <summary>Add a text or image watermark.</summary>
public sealed record AddWatermarkOp : WordsOp
{
    public string? Text { get; init; }
    public string? ImagePath { get; init; }
    public double? Opacity { get; init; }
    public string? Color { get; init; }
}

/// <summary>Remove the document watermark.</summary>
public sealed record RemoveWatermarkOp : WordsOp;

/// <summary>Protect a document, optionally with an environment-sourced password.</summary>
public sealed record ProtectOp : WordsOp
{
    public required string Mode { get; init; }
    public string? PasswordEnv { get; init; }
}

/// <summary>Remove document protection.</summary>
public sealed record UnprotectOp : WordsOp
{
    public string? PasswordEnv { get; init; }
}

/// <summary>Accept revisions, optionally filtered by author.</summary>
public sealed record AcceptRevisionsOp : WordsOp
{
    public string? Author { get; init; }
}

/// <summary>Reject revisions, optionally filtered by author.</summary>
public sealed record RejectRevisionsOp : WordsOp
{
    public string? Author { get; init; }
}

/// <summary>Add a paragraph comment.</summary>
public sealed record AddCommentOp : WordsOp
{
    public required WordsTarget At { get; init; }
    public required string Author { get; init; }
    public required string Text { get; init; }
}

/// <summary>Remove all comments or comments by one author.</summary>
public sealed record RemoveCommentsOp : WordsOp
{
    public string? Author { get; init; }
}

/// <summary>Append a local document.</summary>
public sealed record AppendDocumentOp(string ImportFormatMode = "keepSource") : WordsOp
{
    public required string Path { get; init; }
}

/// <summary>Run a simple or region mail merge.</summary>
public sealed record MailMergeOp : WordsOp
{
    public string? Path { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, string?>>? Inline { get; init; }
    public bool Regions { get; init; }
}

/// <summary>Update TOC or all document fields and page layout.</summary>
public sealed record UpdateFieldsOp(string What = "all") : WordsOp;

/// <summary>The Words operation vocabulary, in published order.</summary>
public static class WordsOps
{
    public static OperationCatalog<WordsOp> Catalog { get; } = new OperationCatalog<WordsOp>(WordsSchemaIds.Ops, maximumOperations: 256)
        .Add<AcceptRevisionsOp>("accept_revisions")
        .Add<AddCommentOp>("add_comment", WordsOpRules.AddComment)
        .Add<AddSectionOp>("add_section", WordsOpRules.AddSection)
        .Add<AddWatermarkOp>("add_watermark", WordsOpRules.AddWatermark)
        .Add<AppendDocumentOp>("append_document", WordsOpRules.AppendDocument)
        .Add<ApplyListOp>("apply_list", WordsOpRules.ApplyList)
        .Add<DefineStyleOp>("define_style", WordsOpRules.DefineStyle)
        .Add<DeleteBlocksOp>("delete_blocks", WordsOpRules.DeleteBlocks)
        .Add<DeleteSectionOp>("delete_section", WordsOpRules.DeleteSection)
        .Add<FormatTextOp>("format_text", WordsOpRules.FormatText)
        .Add<InsertBookmarkOp>("insert_bookmark", WordsOpRules.InsertBookmark)
        .Add<InsertBreakOp>("insert_break", WordsOpRules.InsertBreak)
        .Add<InsertFieldOp>("insert_field", WordsOpRules.InsertField)
        .Add<InsertHyperlinkOp>("insert_hyperlink", WordsOpRules.InsertHyperlink)
        .Add<InsertImageOp>("insert_image", WordsOpRules.InsertImage)
        .Add<InsertMarkdownOp>("insert_markdown", WordsOpRules.InsertMarkdown)
        .Add<InsertParagraphsOp>("insert_paragraphs", WordsOpRules.InsertParagraphs)
        .Add<InsertTableOp>("insert_table", WordsOpRules.InsertTable)
        .Add<InsertTocOp>("insert_toc", WordsOpRules.InsertToc)
        .Add<MailMergeOp>("mail_merge", WordsOpRules.MailMerge)
        .Add<ProtectOp>("protect", WordsOpRules.Protect)
        .Add<RejectRevisionsOp>("reject_revisions")
        .Add<RemoveCommentsOp>("remove_comments")
        .Add<RemoveWatermarkOp>("remove_watermark")
        .Add<ReplaceTextOp>("replace_text", WordsOpRules.ReplaceText)
        .Add<SetDefaultFontOp>("set_default_font", WordsOpRules.SetDefaultFont)
        .Add<SetFooterOp>("set_footer", WordsOpRules.SetFooter)
        .Add<SetHeaderOp>("set_header", WordsOpRules.SetHeader)
        .Add<SetPageNumbersOp>("set_page_numbers", WordsOpRules.SetPageNumbers)
        .Add<SetPageSetupOp>("set_page_setup", WordsOpRules.SetPageSetup)
        .Add<SetPropertiesOp>("set_properties")
        .Add<SetStyleOp>("set_style", WordsOpRules.SetStyle)
        .Add<SetTableCellOp>("set_table_cell", WordsOpRules.SetTableCell)
        .Add<SetTextOp>("set_text", WordsOpRules.SetText)
        .Add<UnprotectOp>("unprotect")
        .Add<UpdateFieldsOp>("update_fields", WordsOpRules.UpdateFields);
}
