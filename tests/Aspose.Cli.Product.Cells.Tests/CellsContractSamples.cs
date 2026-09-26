using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using AddCommentOp = Aspose.Cli.Product.Cells.Contracts.AddCommentOp;
using InsertImageOp = Aspose.Cli.Product.Cells.Contracts.InsertImageOp;
using SetPageSetupOp = Aspose.Cli.Product.Cells.Contracts.SetPageSetupOp;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Product-owned canonical result samples.</summary>
internal static class CellsContractSamples
{
    private static FileFingerprint Fingerprint { get; } = new()
    {
        Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    };

    private static LicenseInfo Licensed { get; } = new() { Mode = LicenseModes.Licensed };

    private static LicenseInfo Evaluation { get; } = new() { Mode = LicenseModes.Evaluation };

    private static Warning EvalWarning { get; } = new()
    {
        Code = WarningCodes.EvalMode,
        Message = "Evaluation mode: the produced file contains an Aspose evaluation watermark.",
        Hint = "Tell the user about the watermark.",
        Docs = "licensing",
    };

    public static WorkbookInfoResult WorkbookInfo { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Workbook = new WorkbookSummary
        {
            Name = "report.xlsx",
            SheetCount = 2,
            Sheets =
            [
                new SheetInfo
                {
                    Name = "Sales",
                    Index = 0,
                    UsedRange = "A1:G120",
                    RowCount = 120,
                    ColumnCount = 7,
                    Hidden = false,
                    ChartCount = 1,
                    PivotTableCount = 0,
                    Preview = [["Region", "Q1"], ["East", "1200"]],
                },
                new SheetInfo
                {
                    Name = "Empty",
                    Index = 1,
                    UsedRange = null,
                    RowCount = 0,
                    ColumnCount = 0,
                    Hidden = true,
                    ChartCount = 0,
                    PivotTableCount = 0,
                },
            ],
            HasVba = false,
            DefinedNameCount = 2,
            Author = "Finance Team",
            DefinedNames =
            [
                new DefinedNameInfo { Name = "TaxRate", RefersTo = "=Summary!$B$1" },
                new DefinedNameInfo { Name = "SalesData", RefersTo = "=Sales!$A$1:$C$5" },
            ],
            FormulaErrors =
            [
                new CellError { Sheet = "Summary", Cell = "B7", Error = "#DIV/0!" },
            ],
            Fonts = ["Arial", "Calibri"],
            Tables = [new TableInfo { Sheet = "Sales", Name = "SalesTable", Range = "A1:C5" }],
            Charts = [new ChartInfo { Sheet = "Sales", Name = "Chart 1", Type = "Column" }],
            Pivots = [new PivotInfo { Sheet = "Summary", Name = "PivotTable1", Range = "A1:D10" }],
            Validations = [new ValidationInfo { Sheet = "Sales", Range = "C2:C100", Type = "List" }],
        },
        License = Licensed,
    };

    public static WorkbookReadResult WorkbookRead { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Scope = ReadScopes.Full,
        Sheet = new SheetProjection
        {
            Name = "Sales",
            Index = 0,
            UsedRange = "A1:C4",
            Window = "A1:C3",
            Truncated = false,
            Cells =
            [
                [
                    new CellData { V = "Region", T = CellValueTypes.String, StyleId = "s0" },
                    new CellData { V = "Q1", T = CellValueTypes.String, StyleId = "s0" },
                    new CellData { V = "Audited", T = CellValueTypes.String, StyleId = "s0" },
                ],
                [
                    new CellData { V = "East", T = CellValueTypes.String },
                    new CellData { V = 1250.5, T = CellValueTypes.Number },
                    new CellData { V = true, T = CellValueTypes.Boolean },
                ],
                [
                    new CellData { V = "2026-07-02", T = CellValueTypes.DateTime },
                    new CellData { V = 0.18, T = CellValueTypes.Number, F = "=(B2-B1)/B1", StyleId = "s1" },
                    new CellData { T = CellValueTypes.Empty },
                ],
            ],
        },
        Styles = new Dictionary<string, StyleData>
        {
            ["s0"] = new StyleData { Font = "Calibri", Size = 11, Bold = true, Color = "#FFFFFF", Bg = "#1F4E79" },
            ["s1"] = new StyleData { Font = "Calibri", Size = 11, NumberFormat = "0.0%" },
        },
        Next = "aspose-cli cells query range \"D:/data/report.xlsx\" --sheet \"Sales\" --range A4:C4 --scope full --max-cells 9 --output json",
        License = Licensed,
    };

    public static CellsOpsBatch Ops { get; } = new()
    {
        Schema = CellsSchemaIds.Ops,
        Ops =
        [
            new SetValuesOp { Sheet = "Sales", Range = "A6", Values = [["South", 500d, true, null]] },
            new SetFormulaOp { Range = "E2:E6", Formula = "=SUM(B2:D2)" },
            new ClearRangeOp { Range = "F1:F10", What = "formats" },
            new CopyRangeOp { From = "Sales!A1:C3", To = "Summary!B2" },
            new FormatRangeOp { Range = "A1:E1", Style = new StyleData { Bold = true, Bg = "#1F4E79" } },
            new MergeCellsOp { Range = "A1:E1" },
            new UnmergeCellsOp { Range = "A1:E1" },
            new InsertRowsOp { At = 2, Count = 3 },
            new DeleteRowsOp { At = 10, Count = 2 },
            new InsertColumnsOp { At = "B", Count = 1 },
            new DeleteColumnsOp { At = "F" },
            new ResizeColumnsOp { From = "B", To = "E" },
            new ResizeRowsOp { From = 1, Height = 24 },
            new AddSheetOp { Name = "Summary", Position = 0 },
            new RenameSheetOp { Sheet = "Old", To = "Archive" },
            new DeleteSheetOp { Sheet = "Obsolete" },
            new SetSheetVisibilityOp { Sheet = "Archive", Hidden = true },
            new FreezePanesOp { Cell = "B2" },
            new CreateChartOp
            {
                Sheet = "Sales",
                Type = ChartTypes.Column,
                DataRange = "A1:C5",
                At = "E2:L18",
                Title = "Quarterly Sales",
            },
            new CreatePivotOp
            {
                Sheet = "Summary",
                SourceRange = "Sales!A1:D100",
                At = "A1",
                Rows = ["Region"],
                Values =
                [
                    new PivotValueField { Field = "Q1" },
                    new PivotValueField { Field = "Q2", Function = PivotFunctions.Average },
                ],
            },
            new SetPageSetupOp
            {
                Sheet = "Sales",
                Orientation = "landscape",
                PaperSize = "a4",
                FitToWidth = 1,
                Margins = new Margins { Top = 0.75, Bottom = 0.75 },
                Header = "&CQuarterly Report",
                Footer = "&CPage &P of &N",
            },
            new SetPrintAreaOp { Sheet = "Sales", Range = "A1:H50", TitleRows = "1:1" },
            new InsertImageOp { Sheet = "Sales", Path = "assets/logo.png", At = "H1", Width = 180, Height = 60 },
            new RefreshPivotOp { Sheet = "Summary", Name = "PivotTable1" },
            new CreateTableOp { Sheet = "Sales", Range = "A1:D20", Name = "SalesTable", Style = "TableStyleMedium2", TotalsRow = true },
            new SetAutoFilterOp { Sheet = "Sales", Range = "A1:D20" },
            new SortRangeOp
            {
                Sheet = "Sales",
                Range = "A2:D20",
                By = [new SortKey { Column = "B", Order = SortOrders.Desc }, new SortKey { Column = "A" }],
                HasHeader = false,
            },
            new SetValidationOp
            {
                Sheet = "Sales",
                Range = "C2:C100",
                Type = ValidationTypes.WholeNumber,
                Operator = ValidationOperators.Between,
                Value1 = "0",
                Value2 = "1000000",
                ErrorMessage = "Enter a positive amount",
                AllowBlank = false,
            },
            new DefineNameOp { Name = "TaxRate", RefersTo = "Config!$B$2" },
            new DeleteNameOp { Name = "ObsoleteRange" },
            new AddCommentOp { Sheet = "Sales", Cell = "B2", Text = "Verify Q1", Author = "Finance" },
            new EditCommentOp { Sheet = "Sales", Cell = "B2", Text = "Verified" },
            new DeleteCommentOp { Sheet = "Sales", Cell = "C3" },
            new ProtectSheetOp { Sheet = "Sales", PasswordEnv = "SHEET_PWD", Allow = [ProtectActions.Sort, ProtectActions.AutoFilter] },
            new UnprotectSheetOp { Sheet = "Archive", PasswordEnv = "SHEET_PWD" },
            new GroupRowsOp { Sheet = "Sales", From = 2, To = 5, Collapse = true },
            new UngroupRowsOp { Sheet = "Sales", From = 2, To = 5 },
            new GroupColumnsOp { Sheet = "Sales", From = "B", To = "D" },
            new UngroupColumnsOp { Sheet = "Sales", From = "B", To = "D" },
            new ClearValidationOp { Sheet = "Sales", Range = "C2:C100" },
            new RemoveDuplicatesOp { Sheet = "Sales", Range = "A1:D100", Columns = ["A", "B"], HasHeader = true },
            new ProtectWorkbookOp { PasswordEnv = "BOOK_PWD" },
            new UnprotectWorkbookOp { PasswordEnv = "BOOK_PWD" },
            new SetHyperlinkOp { Sheet = "Sales", Cell = "A1", Target = "Summary!A1", Display = "Go to summary" },
            new RemoveHyperlinkOp { Sheet = "Sales", Cell = "B2" },
            new UpdateChartOp { Sheet = "Sales", Index = 0, Title = "Revised", Type = ChartTypes.Bar },
            new AddConditionalFormatOp
            {
                Sheet = "Sales",
                Range = "C2:C100",
                Rule = new ConditionalRule { Kind = ConditionalRuleKinds.CellValue, Operator = ValidationOperators.GreaterThan, Value1 = "1000" },
                Style = new ConditionalStyle { Bg = "#FFC7CE" },
            },
            new ClearConditionalFormatsOp { Sheet = "Sales", Range = "D2:D100" },
            new MoveSheetOp { Sheet = "Summary", Position = 1 },
            new SetBordersOp
            {
                Sheet = "Sales",
                Range = "A1:D5",
                Edges = [BorderEdges.Outline, BorderEdges.Inside],
                Style = BorderLineStyles.Thin,
                Color = "#000000",
            },
            new SetDefaultFontOp { Name = "Aptos", Size = 11 },
            new SetTabColorOp { Sheet = "Sales", Color = "#4472C4" },
            new SetSheetViewOp { Sheet = "Sales", Gridlines = false, Zoom = 90, Headings = true },
            new DeleteChartOp { Sheet = "Sales", Index = 0 },
            new AddSparklineOp
            {
                Sheet = "Sales",
                DataRange = "B2:E5",
                Location = "F2:F5",
                Type = SparklineTypes.Line,
            },
            new SetActiveSheetOp { Sheet = "Sales" },
        ],
    };

    public static EditResult Edit { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Output = new OutputInfo { Path = "D:/data/report.out.xlsx", Format = "xlsx", SizeBytes = 25010, Fingerprint = Fingerprint },
        DryRun = false,
        Recalculated = true,
        Applied =
        [
            new BoundedOperationOutcome { Id = "values", Index = 0, Op = "set_values", Status = "ok", ItemsAffected = 4 },
            new BoundedOperationOutcome { Id = "freeze", Index = 1, Op = "freeze_panes", Status = "ok", ItemsAffected = 0 },
            new BoundedOperationOutcome
            {
                Id = "delete",
                Index = 2,
                Op = "delete_sheet",
                Status = "failed",
                ItemsAffected = 0,
                Error = new OpError
                {
                    Code = "SHEET_NOT_FOUND",
                    Message = "Worksheet 'Ghost' not found.",
                    Hint = "Use an existing worksheet name.",
                },
            },
        ],
        Backup = new BackupInfo { Path = "D:/data/report.backup.xlsx", Created = true, SizeBytes = 24576 },
        Verification = new EditVerification
        {
            Ok = true,
            RequestedTargets = [new VerificationTarget { Sheet = "Sales", Range = "B2:B5" }],
            DirectChanges =
            [
                new VerifiedCellChange
                {
                    Sheet = "Sales",
                    Cell = "B2",
                    Left = new CellSide { T = CellValueTypes.Number, V = 10d },
                    Right = new CellSide { T = CellValueTypes.Number, V = 12d },
                },
            ],
            FormulaResultChanges =
            [
                new VerifiedCellChange
                {
                    Sheet = "Sales",
                    Cell = "C2",
                    Left = new CellSide { T = CellValueTypes.Number, V = 20d, F = "=B2*2" },
                    Right = new CellSide { T = CellValueTypes.Number, V = 24d, F = "=B2*2" },
                },
            ],
            OtherChanges = [],
            FormulaErrors = [],
            Truncated = false,
        },
        License = Licensed,
    };

    public static CreateResult Create { get; } = new()
    {
        Output = new OutputInfo { Path = "D:/data/created.xlsx", Format = "xlsx", SizeBytes = 6120, Fingerprint = Fingerprint },
        Sheets = ["Data", "Summary"],
        License = Evaluation,
        Warnings = [EvalWarning],
    };

    public static ConvertResult Convert { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Output = new OutputInfo { Path = "D:/data/report.pdf", Format = "pdf", SizeBytes = 68213 },
        Sheet = "Sales",
        License = Evaluation,
        Warnings = [EvalWarning],
    };

    public static RenderResult Render { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Output = new OutputInfo { Path = "D:/data/sales.png", Format = "png", SizeBytes = 15320 },
        Sheet = "Sales",
        Range = "A1:G20",
        Dpi = 192,
        License = Licensed,
    };

    /// <summary>
    /// The <c>--all-sheets</c> shape of the render result: <c>outputs</c>
    /// lists every rendered sheet, <c>output</c>/<c>sheet</c> summarize the
    /// first, and a skipped sheet rides the <c>SHEETS_SKIPPED</c> warning.
    /// </summary>
    public static RenderResult RenderAllSheets { get; } = new()
    {
        Input = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Output = new OutputInfo { Path = "D:/data/report.Sales.png", Format = "png", SizeBytes = 15320 },
        Sheet = "Sales",
        Dpi = 192,
        Outputs =
        [
            new SheetRenderOutput { Sheet = "Sales", Path = "D:/data/report.Sales.png", SizeBytes = 15320 },
            new SheetRenderOutput { Sheet = "Summary", Path = "D:/data/report.Summary.png", SizeBytes = 9204 },
        ],
        License = Licensed,
        Warnings =
        [
            new Warning
            {
                Code = "SHEETS_SKIPPED",
                Message = "Skipped 1 sheet(s): 'Notes' (RENDER_EMPTY).",
                Hint = "Render a skipped sheet alone with --sheet to get the full error.",
            },
        ],
    };

    public static DiffResult Diff { get; } = new()
    {
        Left = new SourceInfo { Path = "D:/data/report.v2.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Right = new SourceInfo { Path = "D:/data/report.v2.xlsx", Format = "xlsx", SizeBytes = 24812, Fingerprint = Fingerprint },
        Identical = false,
        Summary = new DiffSummary { SheetsAdded = 1, SheetsRemoved = 0, SheetsModified = 1, CellsDiffering = 2 },
        Sheets =
        [
            new SheetDiff { Name = "Notes", Status = "added" },
            new SheetDiff
            {
                Name = "Sales",
                Status = "modified",
                Cells =
                [
                    new CellDiff { Cell = "B2", Left = new CellSide { T = CellValueTypes.Number, V = 1200d }, Right = new CellSide { T = CellValueTypes.Number, V = 1500d } },
                    new CellDiff { Cell = "C2", Left = null, Right = new CellSide { T = CellValueTypes.Number, V = 42d, F = "=A2*2" } },
                ],
            },
        ],
        Truncated = false,
        License = Licensed,
    };

    public static SearchResult Search { get; } = new()
    {
        Source = new SourceInfo { Path = "D:/data/report.xlsx", Format = "xlsx", SizeBytes = 24576, Fingerprint = Fingerprint },
        Pattern = "Q3",
        Hits =
        [
            new SearchHit { Sheet = "Sales", Cell = "A7", Value = "Q3 Adjustment" },
            new SearchHit { Sheet = "Summary", Cell = "D2", Value = "1250.5", Formula = "=Sales!E7" },
        ],
        Truncated = false,
        License = Licensed,
    };

    public static IReadOnlyList<ResultEnvelope> Results { get; } =
    [
        WorkbookInfo,
        WorkbookRead,
        Edit,
        Create,
        Convert,
        Render,
        RenderAllSheets,
        Diff,
        Search,
    ];

    public static IReadOnlyList<ProductSchemaSample> Inputs { get; } =
    [
        new(CellsSchemaIds.Ops, Ops),
    ];
}
