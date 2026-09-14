using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Product-owned canonical result samples.</summary>
internal static class PdfContractSamples
{
    private static LicenseInfo Licensed { get; } = new() { Mode = LicenseModes.Licensed };

    private static LicenseInfo Evaluation { get; } = new() { Mode = LicenseModes.Evaluation };

    private static FileFingerprint Fingerprint { get; } = new()
    {
        Sha256 = new string('a', 64),
    };

    public static PdfInfoResult PdfInfo { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000, Fingerprint = Fingerprint },
        Pdf = new PdfSummary
        {
            Pages = 3,
            DistinctPageSizes =
            [
                new PdfPageSizeSummary { WidthPoints = 612, HeightPoints = 792, Count = 2 },
                new PdfPageSizeSummary { WidthPoints = 792, HeightPoints = 612, Count = 1 },
            ],
            Version = "1.7",
            Encrypted = true,
            Linearized = false,
            Tagged = true,
            PdfaCompliant = false,
            FormType = "acro",
            Attachments = 1,
            Signed = true,
            PasswordType = "owner",
        },
        Pages =
        [
            new PdfPageInfo
            {
                Number = 1,
                WidthPoints = 612,
                HeightPoints = 792,
                Rotation = 0,
                MediaBox = new PdfBox { Left = 0, Bottom = 0, Right = 612, Top = 792 },
                CropBox = new PdfBox { Left = 0, Bottom = 0, Right = 612, Top = 792 },
            },
        ],
        PageLabels =
        [
            new PdfPageLabelInfo
            {
                StartPage = 1,
                NumberingStyle = "roman-lower",
                Prefix = "A-",
                StartingValue = 1,
            },
        ],
        Outline = [new PdfOutlineItem { Title = "Executive summary", Level = 1, Destination = "page:1" }],
        Forms = new PdfFormSummary { Type = "acro", Fields = 2, ReadOnly = false },
        Attachments = [new PdfAttachmentInfo { Name = "source.csv", MimeType = "text/csv", SizeBytes = 1200 }],
        Fonts = [new PdfFontInfo { Name = "Arial", Embedded = true, Subset = true }],
        Permissions = new PdfPermissionInfo
        {
            HasOpenPassword = true,
            HasOwnerPassword = true,
            OwnerAccess = true,
            Print = true,
            Copy = true,
            Modify = true,
            Annotate = true,
            FillForms = true,
            ExtractAccessibility = true,
            Assemble = true,
            PrintHighResolution = true,
        },
        Signatures = [new PdfSignatureInfo { Name = "Approval", Signed = true, Valid = true }],
        Layers = ["Review"],
        Metadata = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["author"] = "Analyst",
            ["title"] = "Quarterly report",
        },
        License = Licensed,
    };

    public static PdfReadResult PdfRead { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Mode = "plain",
        Window = new PdfPageWindow { Pages = "1,2", Of = 3, Truncated = true },
        Pages =
        [
            new PdfPageText { Number = 1, Text = "Executive summary", Truncated = false },
            new PdfPageText { Number = 2, Text = "Revenue grew 12%.", Truncated = false },
        ],
        ScannedPagesSuspected = [2],
        Next = "aspose-cli pdf query pages \"D:/data/report.pdf\" --pages 3 --mode plain --max-chars 20000 --output json",
        License = Evaluation,
        Warnings =
        [
            new Warning
            {
                Code = "SCANNED_PAGES_SUSPECTED",
                Message = "Page 2 has no extractable text and appears image-dominated.",
                Hint = "Inspect the rendered page.",
            },
        ],
    };

    public static PdfConvertResult PdfConvert { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Outputs =
        [
            new OutputInfo { Path = "D:/data/report.p1.png", Format = "png", SizeBytes = 32000 },
            new OutputInfo { Path = "D:/data/report.p2.png", Format = "png", SizeBytes = 34000 },
        ],
        Pages = "1-2",
        License = Evaluation,
        Warnings = [new Warning { Code = WarningCodes.EvalMode, Message = "Evaluation mode is active." }],
    };

    public static PdfRenderResult PdfRender { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Outputs =
        [
            new PdfPageOutput
            {
                Page = 1,
                Output = new OutputInfo { Path = "D:/data/report.p1.png", Format = "png", SizeBytes = 32000 },
            },
        ],
        Dpi = 192,
        License = Licensed,
    };

    public static PdfWriteResult PdfWrite { get; } = new()
    {
        Action = "merge",
        Output = new OutputInfo { Path = "D:/data/merged.pdf", Format = "pdf", SizeBytes = 90000 },
        Inputs =
        [
            new SourceInfo { Path = "D:/data/a.pdf", Format = "pdf", SizeBytes = 40000 },
            new SourceInfo { Path = "D:/data/b.pdf", Format = "pdf", SizeBytes = 45000 },
        ],
        License = Licensed,
    };

    public static PdfSplitResult PdfSplit { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Outputs =
        [
            new PdfSplitOutput
            {
                Index = 1,
                Pages = "1,2",
                Bookmark = "Overview",
                Output = new OutputInfo { Path = "D:/data/parts/report.001.pdf", Format = "pdf", SizeBytes = 24000 },
            },
        ],
        License = Licensed,
    };

    public static PdfExtractResult PdfExtract { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        What = "images",
        Items =
        [
            new PdfExtractedItem
            {
                Path = "D:/data/assets/image-001.png",
                Kind = "image",
                SizeBytes = 4000,
                Page = 1,
            },
        ],
        License = Licensed,
    };

    public static PdfOpsBatch PdfOpsBatch { get; } = new()
    {
        Schema = PdfSchemaIds.Ops,
        SchemaVersion = 2,
        Ops =
        [
            new RotatePagesOp { Pages = "1", Angle = 90 },
            new DeletePagesOp { Pages = "3" },
            new MovePagesOp { Pages = "2", To = 1 },
            new InsertPagesFromOp { Path = "D:/data/append.pdf", Pages = "1", At = 2, PasswordEnv = "PDF_PASSWORD" },
            new InsertBlankPageOp { At = 2, Size = "A4" },
            new CropPagesOp { Pages = "1", Box = "crop", Rect = new PdfRectInput { X = 10, Y = 20, Width = 500, Height = 700 } },
            new SetPageSizeOp { Pages = "1", Size = "LETTER", ScaleContent = true },
            new AddWatermarkTextOp { Pages = "1-", Text = "DRAFT" },
            new AddWatermarkImageOp { Path = "D:/data/logo.png" },
            new AddPageNumbersOp(),
            new AddHeaderTextOp { Text = "Quarterly report" },
            new AddFooterTextOp { Text = "Confidential" },
            new AddStampImageOp { Page = 1, Path = "D:/data/sign.png", Rect = new PdfRectInput { X = 20, Y = 30, Width = 120, Height = 60 } },
            new AddLinkOp { Page = 1, Rect = new PdfRectInput { X = 20, Y = 100, Width = 160, Height = 20 }, Url = "https://example.com" },
            new RedactTextOp { Pattern = "secret", Regex = false },
            new RedactAreaOp { Page = 1, Rect = new PdfRectInput { X = 20, Y = 140, Width = 160, Height = 20 } },
            new SetMetadataOp { Title = "Quarterly report", Custom = new SortedDictionary<string, string> { ["Department"] = "Finance" } },
            new RemoveMetadataOp(),
            new AddBookmarkOp { Title = "Overview", Page = 1 },
            new DeleteBookmarksOp { All = true },
            new Aspose.Cli.Product.Pdf.Contracts.AddAttachmentOp { Path = "D:/data/source.csv", Name = "source.csv" },
            new Aspose.Cli.Product.Pdf.Contracts.RemoveAttachmentOp { Name = "old.csv" },
            new SetPageLabelsOp { Ranges = [new PdfPageLabelRange { StartPage = 1, Style = "roman-lower" }] },
            new SetFormFieldOp { Name = "Customer", Value = "Contoso" },
            new FlattenFormsOp(),
            new EncryptPdfOp { OwnerPasswordEnv = "PDF_OWNER_PASSWORD" },
            new DecryptPdfOp(),
            new OptimizePdfOp { DownsampleImagesDpi = 150, ImageQuality = 75 },
        ],
    };

    public static PdfEditResult PdfEdit { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000, Fingerprint = Fingerprint },
        Output = new OutputInfo { Path = "D:/data/report.out.pdf", Format = "pdf", SizeBytes = 50000, Fingerprint = Fingerprint },
        DryRun = false,
        Applied = [new BoundedOperationOutcome { Id = "op-0001", Index = 0, Op = "rotate_pages", Status = OpStatuses.Ok, ItemsAffected = 1, Targets = ["pdf/page/1"] }],
        Mutation = new MutationReceipt { Verification = "reopened" },
        PagesTouched = [1],
        Verification = new PdfVerification
        {
            Ok = true,
            VisualReviewRequired = false,
            Pages = 2,
            ReadBackPages = [1],
            Renders = [],
            Issues = [],
        },
        License = Licensed,
    };

    public static PdfFormResult PdfForm { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/form.pdf", Format = "pdf", SizeBytes = 12000 },
        Type = "acro",
        ReadOnly = false,
        Fields =
        [
            new PdfFormField
            {
                Name = "Customer",
                Type = "TextBoxField",
                Value = "Contoso",
                ReadOnly = false,
                Required = true,
                Page = 1,
            },
        ],
        License = Licensed,
    };

    public static PdfFormExportResult PdfFormExport { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/form.pdf", Format = "pdf", SizeBytes = 12000 },
        Output = new OutputInfo { Path = "D:/data/form.json", Format = "json", SizeBytes = 120 },
        License = Licensed,
    };

    public static PdfSearchResult PdfSearch { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Pattern = "revenue",
        Hits =
        [
            new PdfSearchHit
            {
                Page = 1,
                Snippet = "Revenue",
                Rect = new PdfRect { X = 24, Y = 42, Width = 60, Height = 12 },
                Occurrence = 1,
            },
        ],
        Truncated = false,
        License = Licensed,
    };

    public static PdfValidateResult PdfValidate { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Profile = "pdfa-2b",
        Valid = false,
        Issues = ["Document metadata does not satisfy the requested profile."],
        Truncated = false,
        License = Licensed,
    };

    public static PdfSignResult PdfSign { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 52000 },
        Output = new OutputInfo { Path = "D:/data/report.signed.pdf", Format = "pdf", SizeBytes = 78000 },
        Signature = new PdfSignatureInfo { Name = "Signature1", Signed = true, Valid = true },
        Visible = true,
        Page = 1,
        Rect = new PdfRect { X = 36, Y = 36, Width = 180, Height = 60 },
        License = Licensed,
    };


    public static IReadOnlyList<ResultEnvelope> Results { get; } =
    [
        PdfInfo,
        PdfRead,
        PdfConvert,
        PdfRender,
        PdfWrite,
        PdfSplit,
        PdfExtract,
        PdfEdit,
        PdfForm,
        PdfFormExport,
        PdfSearch,
        PdfValidate,
        PdfSign,
    ];

    public static IReadOnlyList<ProductSchemaSample> Inputs { get; } =
    [
        new(PdfSchemaIds.Ops, PdfOpsBatch),
    ];
}
