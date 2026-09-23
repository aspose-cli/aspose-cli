using static Aspose.Cli.Sdk.Operations.OperationInvalidException;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Semantic rules of Words operations that the contract types cannot express.</summary>
internal static class WordsOpRules
{
    // Word's own table limits.
    private const int MaximumTableRows = 32_767;
    private const int MaximumTableColumns = 63;

    internal static void ReplaceText(ReplaceTextOp op)
    {
        Require(op.Find.Length > 0, "'find' must not be empty");
        Require(op.MaxReplacements is null or > 0, "'maxReplacements' must be positive");
    }

    internal static void SetText(SetTextOp op) => Target(op.At);

    internal static void InsertParagraphs(InsertParagraphsOp op)
    {
        Insert(op.At, op.Position);
        Require(op.Paragraphs.Count > 0, "'paragraphs' must not be empty");
        Require(op.Paragraphs.All(static paragraph => paragraph.ListLevel is null or >= 0 and <= 8),
            "paragraph listLevel must be 0-8");
    }

    internal static void InsertMarkdown(InsertMarkdownOp op)
    {
        Insert(op.At, op.Position);
        Require(op.Markdown.Length > 0, "'markdown' must not be empty");
    }

    internal static void DeleteBlocks(DeleteBlocksOp op) => Target(op.Target);

    internal static void InsertBreak(InsertBreakOp op)
    {
        Insert(op.At, op.Position);
        Require(op.Kind is "page" or "section", "'kind' must be page or section");
    }

    internal static void InsertImage(InsertImageOp op)
    {
        Insert(op.At, op.Position);
        Require(op.Path.Length > 0, "'path' must not be empty");
        Require(op.Width is null or > 0, "'width' must be positive");
        Require(op.Height is null or > 0, "'height' must be positive");
    }

    internal static void InsertTable(InsertTableOp op)
    {
        Insert(op.At, op.Position);
        Require(op.Rows is > 0 and <= MaximumTableRows && op.Cols is > 0 and <= MaximumTableColumns,
            $"rows must be 1-{MaximumTableRows} and cols 1-{MaximumTableColumns}");
        Require(op.Data is null || op.Data.Count <= op.Rows, "data has more rows than the table");
        Require(op.Data is null || op.Data.All(row => row.Count <= op.Cols), "data has more columns than the table");
    }

    internal static void SetTableCell(SetTableCellOp op)
    {
        Target(op.At);
        Require(op.Row > 0 && op.Col > 0, "row and col are 1-based positive numbers");
    }

    internal static void InsertToc(InsertTocOp op)
    {
        Insert(op.At, op.Position);
        Require(op.MaxLevel is >= 1 and <= 9, "'maxLevel' must be 1-9");
    }

    internal static void InsertBookmark(InsertBookmarkOp op)
    {
        Target(op.At);
        Require(op.Name.Length > 0, "'name' must not be empty");
    }

    internal static void InsertHyperlink(InsertHyperlinkOp op)
    {
        Insert(op.At, op.Position);
        Require(Uri.TryCreate(op.Url, UriKind.Absolute, out _), "'url' must be absolute");
    }

    internal static void InsertField(InsertFieldOp op)
    {
        Insert(op.At, op.Position);
        Require(op.Code.Length > 0, "'code' must not be empty");
    }

    internal static void AddSection(AddSectionOp op)
    {
        Require(op.Position is "start" or "end" or "after", "'position' must be start, end or after");
        Require((op.Position == "after") == (op.After is not null), "'after' is required only when position is after");
        Require(op.After is null or > 0, "'after' is 1-based");
        if (op.PageSetup is not null)
        {
            PageSetup(op.PageSetup);
        }
    }

    internal static void DeleteSection(DeleteSectionOp op) => Require(op.Section > 0, "'section' is 1-based");

    internal static void SetPageSetup(SetPageSetupOp op)
    {
        Require(op.Section is null or > 0, "'section' is 1-based");
        PageSetup(op.Setup);
    }

    internal static void SetHeader(SetHeaderOp op) => HeaderFooter(op.Section, op.Kind, op.Paragraphs, op.Markdown);

    internal static void SetFooter(SetFooterOp op) => HeaderFooter(op.Section, op.Kind, op.Paragraphs, op.Markdown);

    internal static void SetPageNumbers(SetPageNumbersOp op)
    {
        Require(op.Section is null or > 0, "'section' is 1-based");
        Require(op.Location is "header" or "footer", "'location' must be header or footer");
        Require(op.Alignment is "left" or "center" or "right", "'alignment' must be left, center or right");
        Require(op.Format is null or "decimal" or "upperRoman" or "lowerRoman" or "upperLetter" or "lowerLetter",
            "unknown page number format");
        Require(op.Start is null or >= 0, "'start' must not be negative");
    }

    internal static void AppendDocument(AppendDocumentOp op)
    {
        Require(op.Path.Length > 0, "'path' must not be empty");
        Require(op.ImportFormatMode is "keepSource" or "useDestination",
            "'importFormatMode' must be keepSource or useDestination");
    }

    internal static void FormatText(FormatTextOp op)
    {
        Target(op.Target);
        Require(op.Size is null or > 0, "'size' must be positive");
    }

    internal static void SetStyle(SetStyleOp op)
    {
        Target(op.Target);
        Require(op.Style.Length > 0, "'style' must not be empty");
    }

    internal static void DefineStyle(DefineStyleOp op)
    {
        Require(op.Name.Length > 0, "'name' must not be empty");
        Require(op.Size is null or > 0, "'size' must be positive");
        Require(op.SpaceBefore is null or >= 0, "'spaceBefore' must not be negative");
        Require(op.SpaceAfter is null or >= 0, "'spaceAfter' must not be negative");
    }

    internal static void ApplyList(ApplyListOp op)
    {
        Target(op.Target);
        Require(op.Kind is "bullet" or "number", "'kind' must be bullet or number");
        Require(op.Level is >= 0 and <= 8, "'level' must be 0-8");
    }

    internal static void SetDefaultFont(SetDefaultFontOp op)
    {
        Require(op.Font.Length > 0, "'font' must not be empty");
        Require(op.Size is null or > 0, "'size' must be positive");
    }

    internal static void AddWatermark(AddWatermarkOp op)
    {
        Require((op.Text is null) != (op.ImagePath is null), "give exactly one of text or imagePath");
        Require(op.Color is null || op.Text is not null, "'color' applies to text watermarks only");
    }

    internal static void Protect(ProtectOp op) =>
        Require(op.Mode is "readOnly" or "forms" or "comments" or "trackedChanges", "unknown protection mode");

    internal static void AddComment(AddCommentOp op) => Target(op.At);

    internal static void MailMerge(MailMergeOp op)
    {
        Require((op.Path is null) != (op.Inline is null), "give exactly one of path or inline");
        Require(op.Inline is null or { Count: > 0 }, "'inline' must not be empty");
    }

    /// <summary>
    /// Whether Word can record the operation as tracked changes. Aspose.Words tracks the
    /// insertion and deletion of content only; formatting, styles, lists, page setup,
    /// properties, protection, merges, field updates, header replacement and section
    /// structure would change silently, and resolving revisions is not itself an edit.
    /// </summary>
    internal static bool IsTrackable(WordsOp op) => op is ReplaceTextOp or SetTextOp or InsertParagraphsOp
        or InsertMarkdownOp or DeleteBlocksOp or InsertBreakOp { Kind: "page" } or InsertImageOp or InsertTableOp
        or SetTableCellOp or InsertTocOp or InsertBookmarkOp or InsertHyperlinkOp or InsertFieldOp
        or AddCommentOp or RemoveCommentsOp or AppendDocumentOp;

    internal static void UpdateFields(UpdateFieldsOp op) => Require(op.What is "all" or "toc", "'what' must be all or toc");

    private static void Target(WordsTarget target)
    {
        int count = (target.Block is null ? 0 : 1) + (target.Blocks is null ? 0 : 1)
            + (target.Bookmark is null ? 0 : 1) + (target.Heading is null ? 0 : 1)
            + (target.Find is null ? 0 : 1);
        Require(count == 1, "an address must set exactly one of block, blocks, bookmark, heading or find");
        Require(target.Block is null or > 0, "'block' is 1-based");
        if (target.Blocks is not null)
        {
            _ = PageRange.Parse(target.Blocks);
        }

        Require(target.Nth is null or > 0, "'nth' is 1-based");
    }

    private static void Insert(WordsTarget target, string position)
    {
        Target(target);
        Require(position is "before" or "after", "'position' must be before or after");
    }

    private static void HeaderFooter(int? section, string kind, IReadOnlyList<string>? paragraphs, string? markdown)
    {
        Require(section is null or > 0, "'section' is 1-based");
        Require(kind is "primary" or "first" or "even", "'kind' must be primary, first or even");
        Require((paragraphs is null) != (markdown is null), "give exactly one of paragraphs or markdown");
    }

    private static void PageSetup(PageSetupInput setup)
    {
        Require(setup.Size is null or "a3" or "a4" or "a5" or "letter" or "legal", "unknown page size");
        Require(setup.Orientation is null or "portrait" or "landscape", "'orientation' must be portrait or landscape");
        Require(setup.Columns is null or > 0, "'columns' must be positive");
        if (setup.Margins is not null)
        {
            Require(setup.Margins.Top is null or >= 0, "top margin must not be negative");
            Require(setup.Margins.Right is null or >= 0, "right margin must not be negative");
            Require(setup.Margins.Bottom is null or >= 0, "bottom margin must not be negative");
            Require(setup.Margins.Left is null or >= 0, "left margin must not be negative");
        }
    }
}
