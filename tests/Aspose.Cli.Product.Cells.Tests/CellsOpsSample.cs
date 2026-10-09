using System.Text.Json.Nodes;
using Aspose.Cli.Product.Cells.Contracts;
using AddCommentOp = Aspose.Cli.Product.Cells.Contracts.AddCommentOp;
using InsertImageOp = Aspose.Cli.Product.Cells.Contracts.InsertImageOp;
using SetPageSetupOp = Aspose.Cli.Product.Cells.Contracts.SetPageSetupOp;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>An operation document that uses every Cells operation, for the operation contract tests.</summary>
internal static class CellsOpsSample
{
    public static CellsOpsBatch Batch { get; } = new()
    {
        Schema = CellsOp.Catalog.SchemaId,
        Ops =
        [
            new SetValuesOp { Sheet = "Sales", Range = "A6", Values = [["South", 500d, true, null]] },
            new SetFormulaOp { Range = "E2:E6", Formula = "=SUM(B2:D2)" },
            new ClearRangeOp { Range = "F1:F10", What = "formats" },
            new CopyRangeOp { From = "Sales!A1:C3", To = "Summary!B2" },
            new ImportRangeOp { Sheet = "Summary", Path = "D:/data/eu_raw.xlsx", From = "Totals!A2:D8", To = "F2" },
            new ImportSheetOp { Sheet = "Totals", Path = "D:/data/eu_raw.xlsx", Name = "EU", Position = 1 },
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
            new SetDefaultFontOp { Font = "Aptos", Size = 11 },
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
}
