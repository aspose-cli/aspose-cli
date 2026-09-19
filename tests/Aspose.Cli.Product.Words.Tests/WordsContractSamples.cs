using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Product-owned canonical result samples.</summary>
internal static class WordsContractSamples
{
    private static LicenseInfo Licensed { get; } = new() { Mode = LicenseModes.Licensed };

    private static LicenseInfo Evaluation { get; } = new() { Mode = LicenseModes.Evaluation };

    private static Warning EvalWarning { get; } = new()
    {
        Code = WarningCodes.EvalMode,
        Message = "Evaluation mode: the produced file contains an Aspose evaluation watermark.",
        Hint = "Tell the user about the watermark.",
        Docs = "licensing",
    };

    public static DocumentInfoResult DocumentInfo { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Document = new DocumentSummary
        {
            Sections = 2,
            Blocks = 14,
            Paragraphs = 12,
            Tables = 2,
            Pages = 3,
            Words = 420,
            RevisionsPresent = true,
            RevisionCount = 2,
            RevisionAuthors = ["Reviewer"],
            Protection = "NoProtection",
            Signed = false,
        },
        Sections =
        [
            new SectionData
            {
                Index = 1,
                Orientation = "Portrait",
                WidthPoints = 612,
                HeightPoints = 792,
                Margins = new MarginData { Top = 72, Right = 72, Bottom = 72, Left = 72 },
            },
        ],
        Outline = [new OutlineItem { Block = 1, Level = 1, Text = "Executive summary" }],
        Styles = ["Normal", "Heading 1"],
        Bookmarks = ["Summary"],
        License = Licensed,
    };

    public static DocumentReadResult DocumentRead { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Scope = "full",
        Window = new BlockWindow { Blocks = "1-2", Of = 14, Truncated = true },
        Blocks =
        [
            new BlockData
            {
                I = 1,
                Type = "paragraph",
                Section = 1,
                Text = "Executive summary",
                Style = "Heading 1",
                HeadingLevel = 1,
                Runs = [new RunData { Text = "Executive summary", Font = "Arial", Size = 16, Bold = true }],
                ContentTruncated = false,
            },
            new BlockData
            {
                I = 2,
                Type = "table",
                Section = 1,
                Rows = 2,
                Columns = 2,
                Cells = [["Metric", "Value"], ["Revenue", "120"]],
                ContentTruncated = false,
            },
        ],
        Next = "aspose-cli words query blocks \"D:/data/report.docx\" --blocks 3-14 --scope full --output json",
        License = Licensed,
    };

    public static WordsConvertResult WordsConvert { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Output = new OutputInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Pages = "1-2",
        License = Licensed,
    };

    public static WordsRenderResult WordsRender { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Outputs =
        [
            new PageOutput
            {
                Page = 1,
                Output = new OutputInfo { Path = "D:/data/report.p1.png", Format = "png", SizeBytes = 32000 },
            },
        ],
        Dpi = 192,
        License = Licensed,
    };

    public static WordsCreateResult WordsCreate { get; } = new()
    {
        Output = new OutputInfo { Path = "D:/data/new.docx", Format = "docx", SizeBytes = 7100 },
        License = Licensed,
    };

    public static WordsEditResult WordsEdit { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Output = new OutputInfo { Path = "D:/data/report.out.docx", Format = "docx", SizeBytes = 18250 },
        DryRun = false,
        Applied = [new BoundedOperationOutcome { Id = "op-0001", Index = 0, Op = "set_text", Status = OpStatuses.Ok, ItemsAffected = 1, Targets = ["block/1"] }],
        PagesTouched = [1],
        Verification = new WordsVerification
        {
            Ok = true,
            Issues = [],
        },
        License = Licensed,
    };

    public static WordsCompareResult WordsCompare { get; } = new()
    {
        Left = new SourceInfo { Path = "D:/data/a.docx", Format = "docx", SizeBytes = 10000 },
        Right = new SourceInfo { Path = "D:/data/b.docx", Format = "docx", SizeBytes = 10100 },
        Identical = false,
        Revisions = new RevisionCounts { Insertions = 1, Deletions = 0, FormatChanges = 0, Moves = 0 },
        Samples = [new RevisionSample { Type = "Insertion", Text = "new clause" }],
        License = Licensed,
    };

    public static WordsSearchResult WordsSearch { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Pattern = "revenue",
        Hits = [new WordsSearchHit { Block = 4, Section = 1, Scope = "body", Snippet = "Revenue increased." }],
        Truncated = false,
        License = Licensed,
    };

    public static WordsSplitResult WordsSplit { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        Outputs =
        [
            new SplitOutput
            {
                Index = 1,
                Source = "section-1",
                Output = new OutputInfo { Path = "D:/data/parts/part-001.docx", Format = "docx", SizeBytes = 9000 },
            },
        ],
        License = Licensed,
    };

    public static WordsExtractResult WordsExtract { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.docx", Format = "docx", SizeBytes = 18000 },
        What = "images",
        Items = [new ExtractedItem { Path = "D:/data/assets/image-001.png", Kind = "image", SizeBytes = 1024, Block = 3 }],
        License = Licensed,
    };

    public static WordsOpsBatch Ops { get; } = new()
    {
        Schema = WordsSchemaIds.Ops,
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
                MaxReplacements = 2,
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
                Rows = 2,
                Cols = 2,
                Data = [["Metric", "Value"], ["Revenue", "120"]],
                Style = "Table Grid",
            },
            new SetTableCellOp
            {
                At = new WordsTarget { Block = 4 },
                Row = 2,
                Col = 2,
                Text = "125",
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
                    Columns = 2,
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
                    Columns = 1,
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
            new SetDefaultFontOp { Font = "Arial", Size = 11 },
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
                Opacity = 0.25,
                Color = "#808080",
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
                    new Dictionary<string, string?> { ["FirstName"] = "Ava" },
                ],
                Regions = false,
            },
            new UpdateFieldsOp { What = "all" },
        ],
    };


    public static IReadOnlyList<ResultEnvelope> Results { get; } =
    [
        DocumentInfo,
        DocumentRead,
        WordsConvert,
        WordsRender,
        WordsCreate,
        WordsEdit,
        WordsCompare,
        WordsSearch,
        WordsSplit,
        WordsExtract,
    ];

    public static IReadOnlyList<ProductSchemaSample> Inputs { get; } =
    [
        new(WordsSchemaIds.Ops, Ops),
    ];
}
