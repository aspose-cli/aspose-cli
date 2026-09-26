using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Contracts;
using SlidesContracts = Aspose.Cli.Product.Slides.Contracts;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Product-owned canonical result samples.</summary>
internal static class SlidesContractSamples
{
    private static LicenseInfo Licensed { get; } = new() { Mode = LicenseModes.Licensed };

    private static LicenseInfo Evaluation { get; } = new() { Mode = LicenseModes.Evaluation };

    public static PresentationInfoResult PresentationInfo { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/briefing.pptx", Format = "pptx", SizeBytes = 24000 },
        Presentation = new PresentationSummary
        {
            Slides = 3,
            WidthPoints = 960,
            HeightPoints = 540,
            Orientation = "landscape",
            Masters = 1,
            Layouts = 3,
            Sections = 1,
            Comments = 1,
            Media = 2,
            HasMacros = false,
        },
        Slides =
        [
            new SlideInfo
            {
                Slide = 1,
                SlideId = 256,
                Name = "Overview",
                Layout = "Title and Content",
                Title = "Quarterly review",
                PreviewText = "Quarterly review · Revenue grew 12%.",
                Shapes = 4,
                Hidden = false,
                HasNotes = true,
                Comments = 1,
            },
        ],
        Sections = [new PresentationSectionInfo { Name = "Summary", SectionId = "01234567-89ab-cdef-0123-456789abcdef", StartSlide = 1 }],
        Masters = [new PresentationMasterInfo { Name = "Office Theme", SlideCount = 3 }],
        Layouts = [new PresentationLayoutInfo { Name = "Title and Content", Master = "Office Theme", SlideCount = 2 }],
        Media = [new PresentationMediaInfo { Index = 1, Type = "image", ContentType = "image/png", SizeBytes = 1024 }],
        Notes = [new PresentationNotesInfo { Slide = 1, Present = true, Characters = 27 }],
        Comments = [new PresentationCommentInfo { Slide = 1, Author = "Reviewer", Text = "Check this value." }],
        Fonts = ["Aptos"],
        Properties = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["author"] = "Analyst",
            ["title"] = "Quarterly review",
        },
        License = Licensed,
    };

    public static PresentationReadResult PresentationRead { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/briefing.pptx", Format = "pptx", SizeBytes = 24000 },
        Scope = "full",
        SlideCount = 3,
        Slides =
        [
            new SlideData
            {
                Slide = 1,
                SlideId = 256,
                Name = "Overview",
                Layout = "Title and Content",
                Title = "Quarterly review",
                Shapes =
                [
                    new SlideShapeData
                    {
                        ShapeId = 2,
                        ShapeName = "Title 1",
                        Type = "AutoShape",
                        Placeholder = "title",
                        Text = "Quarterly review",
                        Runs =
                        [
                            new SlideTextRunData
                            {
                                Text = "Quarterly review",
                                Font = "Aptos Display",
                                Size = 24,
                                Bold = true,
                                Italic = false,
                            },
                        ],
                        Rect = new SlideRect { X = 40, Y = 24, Width = 880, Height = 72 },
                    },
                ],
                Notes = "Discuss the audited numbers.",
                Comments = [new SlideCommentData { Author = "Reviewer", Text = "Check this value." }],
                ContentTruncated = false,
            },
        ],
        Window = new ResultWindow
        {
            Unit = "slide",
            Returned = 1,
            Total = 3,
            Truncated = true,
            Next = "aspose-cli slides query slides \"D:/data/briefing.pptx\" --slides 2- --scope full --max-chars 20000 --output json",
        },
        License = Licensed,
    };

    public static SlidesConvertResult SlidesConvert { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/briefing.pptx", Format = "pptx", SizeBytes = 24000 },
        Outputs = [new OutputInfo { Path = "D:/data/briefing.pdf", Format = "pdf", SizeBytes = 68000 }],
        Slides = "1-2",
        License = Evaluation,
        Warnings = [new Warning { Code = WarningCodes.EvalMode, Message = "Evaluation mode is active." }],
    };

    public static SlidesRenderResult SlidesRender { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/briefing.pptx", Format = "pptx", SizeBytes = 24000 },
        Outputs =
        [
            new SlideRenderOutput
            {
                Slide = 3,
                SlideId = 258,
                Output = new OutputInfo { Path = "D:/data/briefing.s3.png", Format = "png", SizeBytes = 72000 },
            },
        ],
        Dpi = 192,
        License = Licensed,
    };

    public static SlidesCreateResult SlidesCreate { get; } = new()
    {
        Output = new OutputInfo { Path = "D:/data/briefing.pptx", Format = "pptx", SizeBytes = 48000 },
        Slides = 4,
        Template = new SourceInfo { Path = "D:/data/theme.pptx", Format = "pptx", SizeBytes = 32000 },
        Markdown = new SourceInfo { Path = "D:/data/briefing.md", Format = "md", SizeBytes = 1200 },
        License = Licensed,
    };

    public static SlidesExtractResult SlidesExtract { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/briefing.pptx", Format = "pptx", SizeBytes = 24000 },
        What = "notes",
        Items =
        [
            new SlidesExtractedItem
            {
                Path = "D:/data/extract/slide.s1.notes.txt",
                Kind = "notes",
                SizeBytes = 83,
                Slide = 1,
                SlideId = 256,
                Name = "Overview",
                ContentType = "text/plain",
            },
        ],
        License = Licensed,
    };

    public static SlidesOpsBatch SlidesOpsBatch { get; } = new()
    {
        Schema = SlidesSchemaIds.Ops,
        SchemaVersion = 2,
        Ops =
        [
            new AddSlideOp { Layout = "Title and Content", At = 2 },
            new DeleteSlidesOp { Slides = "4" },
            new MoveSlideOp { Slide = 2, To = 1 },
            new DuplicateSlideOp { SlideId = 256, At = 3 },
            new SetSlideHiddenOp { Slides = "3", Hidden = true },
            new ApplyLayoutOp { Slides = "2", Layout = "Title Only" },
            new SetBackgroundOp { Slides = "1-2", Color = "#F8FAFC" },
            new SlidesContracts.AddSectionOp { Name = "Results", StartSlide = 2 },
            new AppendPresentationOp { Path = "D:/data/append.pptx", MasterPolicy = "keep-source" },
            new SetTitleOp { Slide = 1, Text = "Q3 Review" },
            new SlidesContracts.SetBodyOp
            {
                Slide = 1,
                Paragraphs = [new SlidesParagraphInput { Text = "Revenue grew", Level = 0 }],
            },
            new SlidesContracts.SetTextOp { Slide = 1, ShapeId = 4, Text = "Updated" },
            new SlidesReplaceTextOp { Find = "Q3", Replace = "Q4", Scope = "all" },
            new SetNotesOp { Slide = 1, Text = "Open with the headline." },
            new SlidesInsertImageOp
            {
                Slide = 2,
                Path = "D:/data/chart.png",
                Rect = new SlidesRectInput { X = 72, Y = 120, Width = 300, Height = 180 },
            },
            new InsertShapeOp
            {
                Slide = 2,
                Kind = "rounded-rectangle",
                Rect = new SlidesRectInput { X = 400, Y = 120, Width = 200, Height = 80 },
                Text = "Key point",
                Style = new SlidesShapeStyleInput { Fill = "#E0F2FE", Color = "#0F172A", Bold = true },
            },
            new SlidesInsertTableOp
            {
                Slide = 2,
                Rect = new SlidesRectInput { X = 72, Y = 320, Width = 540, Height = 120 },
                Rows = 2,
                Cols = 2,
                Data = [new[] { "Metric", "Value" }, new[] { "ARR", "$2M" }],
            },
            new SlidesSetTableCellOp { Slide = 2, ShapeId = 8, Row = 2, Col = 2, Text = "$2.1M" },
            new InsertChartOp
            {
                Slide = 3,
                Kind = "column",
                Rect = new SlidesRectInput { X = 72, Y = 120, Width = 560, Height = 300 },
                Categories = ["Q1", "Q2"],
                Series = [new SlidesChartSeriesInput { Name = "Revenue", Values = [10, 12] }],
                Title = "Quarterly revenue",
            },
            new UpdateChartDataOp
            {
                Slide = 3,
                ShapeId = 9,
                Categories = ["Q1", "Q2"],
                Series = [new SlidesChartSeriesInput { Name = "Revenue", Values = [11, 14] }],
            },
            new DeleteShapeOp { Slide = 3, ShapeId = 10 },
            new SetShapeStyleOp
            {
                Slide = 1,
                Placeholder = "title",
                Style = new SlidesShapeStyleInput { Font = "Aptos Display", Size = 30, Color = "#0F172A" },
            },
            new SlidesContracts.SetFooterOp { Slides = "1-3", Text = "Confidential", ShowNumber = true },
            new SetTransitionOp { Slides = "1-3", Kind = "fade", DurationMs = 500 },
            new SlidesSetPropertiesOp { Title = "Q3 Review", Author = "Finance", Company = "Aspose" },
            new SetSlideSizeOp { Size = "16x9", ScaleContent = true },
        ],
    };

    public static SlidesEditResult SlidesEdit { get; } = new()
    {
        Input =new SourceInfo { Path = "D:/data/deck.pptx", Format = "pptx", SizeBytes = 24000 },
        Output = new OutputInfo { Path = "D:/data/deck.out.pptx", Format = "pptx", SizeBytes = 24500 },
        DryRun = false,
        Applied =
        [
            new BoundedOperationOutcome
            {
                Id = "op-0001",
                Index = 0,
                Op = "set_title",
                Status = "ok",
                ItemsAffected = 1,
                Targets = ["slide/256"],
            },
        ],
        SlidesTouched = [256],
        License = Licensed,
    };

    public static SlidesSearchResult SlidesSearch { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/deck.pptx", Format = "pptx", SizeBytes = 24000 },
        Pattern = "revenue",
        Scope = "all",
        Hits =
        [
            new SlidesSearchHit
            {
                Slide = 2,
                SlideId = 257,
                Scope = "shapes",
                ShapeId = 7,
                ShapeName = "Content Placeholder 2",
                Text = "Revenue grew 20%.",
                Start = 0,
                Length = 7,
            },
        ],
        Window = new ResultWindow { Unit = "hit", Returned = 1, Truncated = false },
        License = Licensed,
    };

    public static IReadOnlyList<ResultEnvelope> Results { get; } =
    [
        PresentationInfo,
        PresentationRead,
        SlidesConvert,
        SlidesRender,
        SlidesCreate,
        SlidesExtract,
        SlidesEdit,
        SlidesSearch,
    ];

    public static IReadOnlyList<ProductSchemaSample> Inputs { get; } =
    [
        new(SlidesSchemaIds.Ops, SlidesOpsBatch),
    ];
}
