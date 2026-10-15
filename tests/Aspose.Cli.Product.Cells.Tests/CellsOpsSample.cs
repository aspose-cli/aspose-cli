using System.Text.Json.Nodes;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
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
            new ReplaceTextOp { Sheet = "Sales", Range = "A2:A6", Find = "South", Replace = "North", WholeWord = true, MaxReplacementCount = 1 },
            new CopyRangeOp { From = "Sales!A1:C3", To = "Summary!B2" },
            new ImportRangeOp { Sheet = "Summary", Path = "D:/data/eu_raw.xlsx", From = "Totals!A2:D8", To = "F2" },
            new ImportSheetOp { SourceSheet = "Totals", Path = "D:/data/eu_raw.xlsx", Name = "EU", At = 2 },
            new FormatRangeOp { Range = "A1:E1", Style = new CellsStyleData { Bold = true, FillColor = "#1F4E79" } },
            new MergeCellsOp { Range = "A1:E1" },
            new UnmergeCellsOp { Range = "A1:E1" },
            new InsertRowsOp { At = 2, Count = 3 },
            new DeleteRowsOp { At = 10, Count = 2 },
            new InsertColumnsOp { At = "B", Count = 1 },
            new DeleteColumnsOp { At = "F" },
            new ResizeColumnsOp { First = "B", Last = "E" },
            new ResizeRowsOp { First = 1, Height = 24 },
            new InsertSheetOp { Name = "Summary", At = 1 },
            new RenameSheetOp { Sheet = "Old", Name = "Archive" },
            new DeleteSheetOp { Sheet = "Obsolete" },
            new SetSheetVisibilityOp { Sheet = "Archive", Visibility = CellsSheetVisibilities.Hidden },
            new FreezePanesOp { Cell = "B2" },
            new UnfreezePanesOp { Sheet = "Sales" },
            new ClearAutoFilterOp { Sheet = "Sales" },
            new ClearTabColorOp { Sheet = "Sales" },
            new ClearPrintAreaOp { Sheet = "Sales" },
            new InsertChartOp
            {
                Sheet = "Sales",
                Kind = CellsChartTypes.Column,
                DataRange = "A1:C5",
                At = "E2:L18",
                Title = "Quarterly Sales",
            },
            new InsertPivotOp
            {
                Sheet = "Summary",
                SourceRange = "Sales!A1:D100",
                At = "A1",
                Rows = ["Region"],
                Values =
                [
                    new CellsPivotValueField { Field = "Q1" },
                    new CellsPivotValueField { Field = "Q2", Function = CellsPivotFunctions.Average },
                ],
            },
            new SetPageSetupOp
            {
                Sheet = "Sales",
                Orientation = "landscape",
                PageSize = new PageSizeInput { Preset = PageSizePresets.A4 },
                FitToWidth = 1,
                Margins = new CellsMargins { Top = 54, Bottom = 54 },
                HeaderCenter = "&CQuarterly Report",
                FooterCenter = "&CPage &P of &N",
            },
            new SetPrintAreaOp { Sheet = "Sales", Range = "A1:H50", TitleRows = "1:1" },
            new InsertImageOp { Sheet = "Sales", Path = "assets/logo.png", At = "H1", Width = 135, Height = 45 },
            new RefreshPivotOp { Sheet = "Summary", Name = "PivotTable1" },
            new DefineTableOp { Sheet = "Sales", Range = "A1:D20", Name = "SalesTable", TableStyle = "TableStyleMedium2", TotalsRow = true },
            new SetAutoFilterOp { Sheet = "Sales", Range = "A1:D20" },
            new SortRangeOp
            {
                Sheet = "Sales",
                Range = "A2:D20",
                By = [new CellsSortKey { Column = "B", Order = CellsSortOrders.Desc }, new CellsSortKey { Column = "A" }],
                HasHeader = false,
            },
            new SetValidationOp
            {
                Sheet = "Sales",
                Range = "C2:C100",
                Kind = CellsValidationTypes.WholeNumber,
                Operator = CellsValidationOperators.Between,
                Value1 = "0",
                Value2 = "1000000",
                ErrorMessage = "Enter a positive amount",
                AllowBlank = false,
            },
            new DefineNameOp { Name = "TaxRate", RefersTo = "Config!$B$2" },
            new DeleteNameOp { Name = "ObsoleteRange" },
            new AddCommentOp { Sheet = "Sales", Cell = "B2", Text = "Verify Q1", Author = "Finance" },
            new SetCommentOp { Sheet = "Sales", Cell = "B2", Text = "Verified" },
            new DeleteCommentOp { Sheet = "Sales", Cell = "C3" },
            new ProtectSheetOp { Sheet = "Sales", PasswordEnv = "SHEET_PWD", Allow = [CellsProtectActions.Sort, CellsProtectActions.AutoFilter] },
            new UnprotectSheetOp { Sheet = "Archive", PasswordEnv = "SHEET_PWD" },
            new GroupRowsOp { Sheet = "Sales", First = 2, Last = 5, Collapsed = true },
            new UngroupRowsOp { Sheet = "Sales", First = 2, Last = 5 },
            new GroupColumnsOp { Sheet = "Sales", First = "B", Last = "D" },
            new UngroupColumnsOp { Sheet = "Sales", First = "B", Last = "D" },
            new ClearValidationOp { Sheet = "Sales", Range = "C2:C100" },
            new DeleteDuplicatesOp { Sheet = "Sales", Range = "A1:D100", Columns = ["A", "B"], HasHeader = true },
            new ProtectWorkbookOp { PasswordEnv = "BOOK_PWD" },
            new UnprotectWorkbookOp { PasswordEnv = "BOOK_PWD" },
            new SetHyperlinkOp { Sheet = "Sales", Cell = "A1", Target = "Summary!A1", Display = "Go to summary" },
            new DeleteHyperlinkOp { Sheet = "Sales", Cell = "B2" },
            new SetChartOp { Sheet = "Sales", Index = 1, Title = "Revised", Kind = CellsChartTypes.Bar },
            new AddConditionalFormatOp
            {
                Sheet = "Sales",
                Range = "C2:C100",
                Rule = new CellsConditionalRule { Kind = CellsConditionalRuleKinds.CellValue, Operator = CellsValidationOperators.GreaterThan, Value1 = "1000" },
                MatchStyle = new CellsConditionalStyle { FillColor = "#FFC7CE" },
            },
            new ClearConditionalFormatsOp { Sheet = "Sales", Range = "D2:D100" },
            new MoveSheetOp { Sheet = "Summary", To = 2 },
            new SetBordersOp
            {
                Sheet = "Sales",
                Range = "A1:D5",
                Edges = [CellsBorderEdges.Outline, CellsBorderEdges.Inside],
                LineStyle = CellsBorderLineStyles.Thin,
                Color = "#000000",
            },
            new SetDefaultFontOp { Font = "Aptos", FontSize = 11 },
            new SetTabColorOp { Sheet = "Sales", Color = "#4472C4" },
            new SetSheetViewOp { Sheet = "Sales", Gridlines = false, ZoomPercent = 90, Headings = true },
            new DeleteChartOp { Sheet = "Sales", Index = 1 },
            new AddSparklineOp
            {
                Sheet = "Sales",
                DataRange = "B2:E5",
                At = "F2:F5",
                Kind = CellsSparklineTypes.Line,
            },
            new SetActiveSheetOp { Sheet = "Sales" },
        ],
    };
}
