using System.Text.Json;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Words.Operations;

/// <summary>Parses and validates Words operations before a document is opened.</summary>
internal static class WordsOpsParser
{
    private const int MaximumOperations = 256;

    public static WordsOpsBatch Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Invalid("the ops document is empty");
        }

        WordsOpsBatch batch;
        try
        {
            batch = ProductJsonContext.Definition.Deserialize<WordsOpsBatch>(json);
        }
        catch (JsonException ex)
        {
            throw Invalid(ex.Message);
        }

        return Prepare(batch);
    }

    /// <summary>Validates a composed batch and assigns stable missing IDs.</summary>
    internal static WordsOpsBatch Prepare(WordsOpsBatch batch)
    {
        BoundedOperationValidation.ValidateEnvelope(
            batch,
            WordsSchemaIds.Ops,
            Invalid);

        if (batch.Ops.Count is < 1 or > MaximumOperations)
        {
            throw Invalid($"the ops array must contain 1-{MaximumOperations} operations");
        }

        IReadOnlyList<WordsOp> identified = BoundedOperationIds.Assign(
            batch.Ops,
            static operation => operation.Id,
            static (operation, id) => operation with { Id = id },
            Invalid);
        for (int index = 0; index < identified.Count; index++)
        {
            Validate(identified[index], index);
        }

        return batch with { SchemaVersion = 2, Ops = identified };
    }

    private static void Validate(WordsOp op, int index)
    {
        switch (op)
        {
            case ReplaceTextOp or SetTextOp or InsertParagraphsOp
                or InsertMarkdownOp or DeleteBlocksOp or InsertBreakOp:
                ValidateContent(op, index);
                break;
            case InsertImageOp or InsertTableOp or SetTableCellOp
                or InsertTocOp or InsertBookmarkOp or InsertHyperlinkOp
                or InsertFieldOp:
                ValidateTablesAndObjects(op, index);
                break;
            case AddSectionOp or DeleteSectionOp or SetPageSetupOp
                or SetHeaderOp or SetFooterOp or SetPageNumbersOp
                or AppendDocumentOp:
                ValidateStructure(op, index);
                break;
            case FormatTextOp or SetStyleOp or DefineStyleOp
                or ApplyListOp or SetDefaultFontOp:
                ValidateFormatting(op, index);
                break;
            case AddWatermarkOp or ProtectOp or AddCommentOp
                or MailMergeOp or UpdateFieldsOp:
                ValidateDocumentState(op, index);
                break;
        }
    }

    private static void ValidateContent(WordsOp op, int index)
    {
        switch (op)
        {
            case ReplaceTextOp value:
                Require(value.Find.Length > 0, index, op, "'find' must not be empty");
                Require(value.MaxReplacements is null or > 0, index, op, "'maxReplacements' must be positive");
                break;
            case SetTextOp value: ValidateTarget(value.At, index, op); break;
            case InsertParagraphsOp value:
                ValidateInsert(value.At, value.Position, index, op);
                Require(value.Paragraphs.Count > 0, index, op, "'paragraphs' must not be empty");
                Require(value.Paragraphs.All(static paragraph => paragraph.ListLevel is null or >= 0 and <= 8), index, op, "paragraph listLevel must be 0-8");
                break;
            case InsertMarkdownOp value: ValidateInsert(value.At, value.Position, index, op); Require(value.Markdown.Length > 0, index, op, "'markdown' must not be empty"); break;
            case DeleteBlocksOp value: ValidateTarget(value.Target, index, op); break;
            case InsertBreakOp value: ValidateInsert(value.At, value.Position, index, op); Require(value.Kind is "page" or "section", index, op, "'kind' must be page or section"); break;
        }
    }

    private static void ValidateTablesAndObjects(WordsOp op, int index)
    {
        switch (op)
        {
            case InsertImageOp value:
                ValidateInsert(value.At, value.Position, index, op);
                Require(value.Path.Length > 0, index, op, "'path' must not be empty");
                Require(value.Width is null or > 0, index, op, "'width' must be positive");
                Require(value.Height is null or > 0, index, op, "'height' must be positive");
                break;
            case InsertTableOp value:
                ValidateInsert(value.At, value.Position, index, op);
                Require(value.Rows > 0 && value.Cols > 0, index, op, "rows and cols must be positive");
                Require(value.Data is null || value.Data.Count <= value.Rows, index, op, "data has more rows than the table");
                Require(value.Data is null || value.Data.All(row => row.Count <= value.Cols), index, op, "data has more columns than the table");
                break;
            case SetTableCellOp value:
                ValidateTarget(value.At, index, op);
                Require(value.Row > 0 && value.Col > 0, index, op, "row and col are 1-based positive numbers");
                break;
            case InsertTocOp value: ValidateInsert(value.At, value.Position, index, op); Require(value.MaxLevel is >= 1 and <= 9, index, op, "'maxLevel' must be 1-9"); break;
            case InsertBookmarkOp value: ValidateTarget(value.At, index, op); Require(value.Name.Length > 0, index, op, "'name' must not be empty"); break;
            case InsertHyperlinkOp value: ValidateInsert(value.At, value.Position, index, op); Require(Uri.TryCreate(value.Url, UriKind.Absolute, out _), index, op, "'url' must be absolute"); break;
            case InsertFieldOp value: ValidateInsert(value.At, value.Position, index, op); Require(value.Code.Length > 0, index, op, "'code' must not be empty"); break;
        }
    }

    private static void ValidateStructure(WordsOp op, int index)
    {
        switch (op)
        {
            case AddSectionOp value:
                Require(value.Position is "start" or "end" or "after", index, op, "'position' must be start, end or after");
                Require((value.Position == "after") == (value.After is not null), index, op, "'after' is required only when position is after");
                Require(value.After is null or > 0, index, op, "'after' is 1-based");
                if (value.PageSetup is not null)
                {
                    ValidatePageSetup(value.PageSetup, index, op);
                }

                break;
            case DeleteSectionOp value: Require(value.Section > 0, index, op, "'section' is 1-based"); break;
            case SetPageSetupOp value:
                Require(value.Section is null or > 0, index, op, "'section' is 1-based");
                ValidatePageSetup(value.Setup, index, op);
                break;
            case SetHeaderOp value: ValidateHeader(value.Section, value.Kind, value.Paragraphs, value.Markdown, index, op); break;
            case SetFooterOp value: ValidateHeader(value.Section, value.Kind, value.Paragraphs, value.Markdown, index, op); break;
            case SetPageNumbersOp value:
                Require(value.Section is null or > 0, index, op, "'section' is 1-based");
                Require(value.Location is "header" or "footer", index, op, "'location' must be header or footer");
                Require(value.Alignment is "left" or "center" or "right", index, op, "'alignment' must be left, center or right");
                Require(value.Format is null or "decimal" or "upperRoman" or "lowerRoman" or "upperLetter" or "lowerLetter", index, op, "unknown page number format");
                Require(value.Start is null or >= 0, index, op, "'start' must not be negative");
                break;
            case AppendDocumentOp value:
                Require(value.Path.Length > 0, index, op, "'path' must not be empty");
                Require(value.ImportFormatMode is "keepSource" or "useDestination", index, op, "'importFormatMode' must be keepSource or useDestination");
                break;
        }
    }

    private static void ValidateFormatting(WordsOp op, int index)
    {
        switch (op)
        {
            case FormatTextOp value: ValidateTarget(value.Target, index, op); Require(value.Size is null or > 0, index, op, "'size' must be positive"); break;
            case SetStyleOp value: ValidateTarget(value.Target, index, op); Require(value.Style.Length > 0, index, op, "'style' must not be empty"); break;
            case DefineStyleOp value:
                Require(value.Name.Length > 0, index, op, "'name' must not be empty");
                Require(value.Size is null or > 0, index, op, "'size' must be positive");
                Require(value.SpaceBefore is null or >= 0, index, op, "'spaceBefore' must not be negative");
                Require(value.SpaceAfter is null or >= 0, index, op, "'spaceAfter' must not be negative");
                break;
            case ApplyListOp value: ValidateTarget(value.Target, index, op); Require(value.Kind is "bullet" or "number", index, op, "'kind' must be bullet or number"); Require(value.Level is >= 0 and <= 8, index, op, "'level' must be 0-8"); break;
            case SetDefaultFontOp value: Require(value.Font.Length > 0, index, op, "'font' must not be empty"); Require(value.Size is null or > 0, index, op, "'size' must be positive"); break;
        }
    }

    private static void ValidateDocumentState(WordsOp op, int index)
    {
        switch (op)
        {
            case AddWatermarkOp value:
                Require((value.Text is null) != (value.ImagePath is null), index, op, "give exactly one of text or imagePath");
                Require(value.Opacity is null or >= 0 and <= 1, index, op, "'opacity' must be from 0 through 1");
                break;
            case ProtectOp value: Require(value.Mode is "readOnly" or "forms" or "comments" or "trackedChanges", index, op, "unknown protection mode"); break;
            case AddCommentOp value: ValidateTarget(value.At, index, op); break;
            case MailMergeOp value:
                Require((value.Path is null) != (value.Inline is null), index, op, "give exactly one of path or inline");
                Require(value.Inline is null or { Count: > 0 }, index, op, "'inline' must not be empty");
                break;
            case UpdateFieldsOp value: Require(value.What is "all" or "toc", index, op, "'what' must be all or toc"); break;
        }
    }

    private static void ValidateTarget(WordsTarget target, int index, WordsOp op)
    {
        int count = (target.Block is null ? 0 : 1) + (target.Blocks is null ? 0 : 1)
            + (target.Bookmark is null ? 0 : 1) + (target.Heading is null ? 0 : 1)
            + (target.Find is null ? 0 : 1);
        Require(count == 1, index, op, "an address must set exactly one of block, blocks, bookmark, heading or find");
        Require(target.Block is null or > 0, index, op, "'block' is 1-based");
        if (target.Blocks is not null)
        {
            _ = PageRange.Parse(target.Blocks);
        }

        Require(target.Nth is null or > 0, index, op, "'nth' is 1-based");
    }

    private static void ValidateInsert(WordsTarget target, string position, int index, WordsOp op)
    {
        ValidateTarget(target, index, op);
        Require(position is "before" or "after", index, op, "'position' must be before or after");
    }

    private static void ValidateHeader(int? section, string kind, IReadOnlyList<string>? paragraphs, string? markdown, int index, WordsOp op)
    {
        Require(section is null or > 0, index, op, "'section' is 1-based");
        Require(kind is "primary" or "first" or "even", index, op, "'kind' must be primary, first or even");
        Require((paragraphs is null) != (markdown is null), index, op, "give exactly one of paragraphs or markdown");
    }

    private static void ValidatePageSetup(PageSetupInput setup, int index, WordsOp op)
    {
        Require(setup.Size is null or "a3" or "a4" or "a5" or "letter" or "legal", index, op, "unknown page size");
        Require(setup.Orientation is null or "portrait" or "landscape", index, op, "'orientation' must be portrait or landscape");
        Require(setup.Columns is null or > 0, index, op, "'columns' must be positive");
        if (setup.Margins is not null)
        {
            Require(setup.Margins.Top is null or >= 0, index, op, "top margin must not be negative");
            Require(setup.Margins.Right is null or >= 0, index, op, "right margin must not be negative");
            Require(setup.Margins.Bottom is null or >= 0, index, op, "bottom margin must not be negative");
            Require(setup.Margins.Left is null or >= 0, index, op, "left margin must not be negative");
        }
    }

    private static void Require(bool condition, int index, WordsOp op, string reason)
    {
        if (!condition)
        {
            throw Invalid($"op {index} ({op.OpName}): {reason}");
        }
    }

    private static CliException Invalid(string reason) => new(
        ErrorCodes.OpsInvalid,
        $"Invalid Words ops batch: {reason}.",
        hint: "Fix the named op using 'aspose-cli schema v2/words/ops'.");
}
