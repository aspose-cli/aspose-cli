using Aspose.Cli.Product.Words.Contracts;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// One edit batch that uses every Words operation with its optional fields set, so the shared
/// operation contract tests round-trip and validate the whole vocabulary.
/// </summary>
internal static class WordsOperationSample
{
    public static WordsOpsBatch Batch { get; } = new()
    {
        Schema = WordsOp.Catalog.SchemaId,
        SchemaVersion = 2,
        Ops =
        [
            new ReplaceTextOp
            {
                Find = "draft",
                Replace = "final",
                MatchCase = true,
                WholeWord = true,
                Scope = "body",
                MaxReplacementCount = 2,
            },
            new SetTextOp
            {
                At = new WordsTarget { Block = 1 },
                Text = "Executive summary",
            },
            new InsertParagraphsOp
            {
                At = new WordsTarget { Blocks = "2-3" },
                Position = "after",
                Paragraphs =
                [
                    new ParagraphInput { Text = "Inserted paragraph", Style = "Normal", ListLevel = 0 },
                ],
            },
            new InsertMarkdownOp
            {
                At = new WordsTarget { Bookmark = "Summary" },
                Position = "before",
                Markdown = "## Imported section",
            },
            new DeleteBlocksOp
            {
                Target = new WordsTarget { Heading = "Obsolete", Nth = 1 },
            },
            new InsertBreakOp
            {
                At = new WordsTarget { Find = "Appendix", Nth = 1 },
                Position = "before",
                Kind = "page",
            },
            new InsertImageOp
            {
                At = new WordsTarget { Block = 2 },
                Position = "after",
                Path = "assets/chart.png",
                Width = 320,
                Height = 180,
                Inline = false,
            },
            new InsertTableOp
            {
                At = new WordsTarget { Block = 3 },
                Position = "after",
                RowCount = 2,
                ColumnCount = 2,
                Cells = [["Metric", "Value"], ["Revenue", "120"]],
                Style = "Table Grid",
            },
            new SetTableCellOp
            {
                At = new WordsTarget { Block = 4 },
                Row = 2,
                Col = 2,
                Text = "125",
            },
            new RepeatTableRowOp
            {
                At = new WordsTarget { Find = "{{code}}" },
                Row = 2,
                Items =
                [
                    new Dictionary<string, object?> { ["code"] = "A-100", ["name"] = "Widget" },
                ],
            },
            new FormatTableOp
            {
                At = new WordsTarget { Block = 4 },
                KeepTogether = true,
                HeaderRowCount = 1,
                KeepWithNext = true,
            },
            new InsertTocOp
            {
                At = new WordsTarget { Block = 1 },
                Position = "before",
                MaxLevel = 3,
            },
            new InsertBookmarkOp
            {
                At = new WordsTarget { Block = 5 },
                Name = "ReviewPoint",
            },
            new InsertHyperlinkOp
            {
                At = new WordsTarget { Block = 5 },
                Position = "after",
                Text = "Reference",
                Url = "https://example.com/reference",
            },
            new InsertFieldOp
            {
                At = new WordsTarget { Block = 6 },
                Position = "after",
                Code = "DATE",
            },
            new AddSectionOp
            {
                Position = "after",
                After = 1,
                PageSetup = new PageSetupInput
                {
                    Size = "a4",
                    Orientation = "landscape",
                    Margins = new MarginInput { Top = 48, Right = 54, Bottom = 48, Left = 54 },
                    ColumnCount = 2,
                },
            },
            new DeleteSectionOp { Section = 3 },
            new SetPageSetupOp
            {
                Section = 1,
                Setup = new PageSetupInput
                {
                    Size = "letter",
                    Orientation = "portrait",
                    Margins = new MarginInput { Top = 72, Right = 72, Bottom = 72, Left = 72 },
                    ColumnCount = 1,
                },
            },
            new SetHeaderOp
            {
                Section = 1,
                Kind = "primary",
                Paragraphs = ["Quarterly report"],
            },
            new SetFooterOp
            {
                Section = 1,
                Kind = "first",
                Markdown = "_Confidential_",
            },
            new SetPageNumbersOp
            {
                Section = 1,
                Location = "footer",
                Alignment = "center",
                Format = "decimal",
                Start = 1,
            },
            new FormatTextOp
            {
                Target = new WordsTarget { Blocks = "1-2" },
                Bold = true,
                Italic = false,
                Underline = true,
                Size = 12,
                Color = "#1F4E79",
                Font = "Arial",
                LatinFont = "Calibri",
                EastAsianFont = "SimSun",
                Highlight = "yellow",
            },
            new SetStyleOp
            {
                Target = new WordsTarget { Heading = "Summary" },
                Style = "Heading 2",
            },
            new DefineStyleOp
            {
                Name = "Report Callout",
                BasedOn = "Normal",
                Font = "Arial",
                LatinFont = "Calibri",
                EastAsianFont = "SimSun",
                Size = 11,
                Bold = true,
                Color = "#1F4E79",
                SpaceBefore = 6,
                SpaceAfter = 6,
            },
            new ApplyListOp
            {
                Target = new WordsTarget { Blocks = "7-9" },
                Kind = "number",
                Level = 1,
            },
            new SetDefaultFontOp { Font = "Arial", LatinFont = "Calibri", EastAsianFont = "SimSun", Size = 11 },
            new SetPropertiesOp
            {
                Title = "Quarterly report",
                Author = "Finance",
                Subject = "Results",
                Keywords = "quarterly,finance",
                Custom = new Dictionary<string, string?> { ["Status"] = "Final", ["Reviewer"] = null },
            },
            new AddWatermarkOp
            {
                Text = "DRAFT",
                Faded = false,
                Color = "#808080",
                Font = "Arial",
            },
            new RemoveWatermarkOp(),
            new ProtectOp { Mode = "readOnly", PasswordEnv = "WORDS_PROTECT_PASSWORD" },
            new UnprotectOp { PasswordEnv = "WORDS_PROTECT_PASSWORD" },
            new AcceptRevisionsOp { Author = "Reviewer" },
            new RejectRevisionsOp { Author = "External" },
            new AddCommentOp
            {
                At = new WordsTarget { Block = 2 },
                Author = "Reviewer",
                Text = "Verify this figure.",
            },
            new RemoveCommentsOp { Author = "Reviewer" },
            new AppendDocumentOp
            {
                Path = "appendix.docx",
                ImportFormatMode = "keepSource",
            },
            new MailMergeOp
            {
                Inline =
                [
                    new Dictionary<string, object?> { ["FirstName"] = "Ava" },
                ],
                Regions = false,
            },
            new UpdateFieldsOp { What = "all" },
        ],
    };
}
