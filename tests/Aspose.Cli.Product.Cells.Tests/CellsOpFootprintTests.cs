using Aspose.Cells;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// What each Cells operation addresses, one row per sample: the target the footprint spotlights,
/// whether the verification diff sees the operation, the cells it is charged for, and what a
/// failed operation reports it attempted. Every operation of the vocabulary has a row, so a new
/// operation fails here until it is placed.
/// </summary>
public sealed class CellsOpFootprintTests
{
    /// <summary>
    /// The sample of every operation, then the variants whose address takes another branch:
    /// no sheet, no range, no name, an unparsable destination.
    /// </summary>
    private static readonly CellsOp[] Samples =
    [
        .. CellsOpsSample.Batch.Ops,
        new SetValuesOp { Range = "B2", Values = [[1d, 2d], [3d, 4d]] },
        new SetValuesOp { Sheet = "Sales", Range = "C3", Values = [] },
        new CopyRangeOp { Sheet = "Sales", From = "A1:B2", To = "D4" },
        new CopyRangeOp { Sheet = "Sales", From = "A1:B2", To = "not a cell" },
        new ImportRangeOp { Path = "D:/data/eu_raw.xlsx", From = "A1:C2", To = "Summary!B2" },
        // Clipped at the grid's corner, the target is one cell and the source six.
        new ImportRangeOp { Sheet = "Sales", Path = "D:/data/eu_raw.xlsx", From = "A1:C2", To = "XFD1048576" },
        new ImportSheetOp { Path = "D:/data/eu_raw.xlsx", SourceSheet = "Totals" },
        new ImportSheetOp { Path = "D:/data/eu_raw.xlsx" },

        // Lists of cells and ranges are spotlighted by the one range that covers them.
        new SetValidationOp { Sheet = "Sales", Range = "D5,D7,D8", Kind = CellsValidationTypes.WholeNumber, Operator = CellsValidationOperators.Between, Value1 = "0", Value2 = "9" },
        new ClearValidationOp { Sheet = "Sales", Range = "D5,D7,D8" },
        new AddConditionalFormatOp
        {
            Sheet = "Sales",
            Range = "D5,D7,D8",
            Rule = new CellsConditionalRule { Kind = CellsConditionalRuleKinds.CellValue, Operator = CellsValidationOperators.GreaterThan, Value1 = "1" },
        },
        new ClearConditionalFormatsOp { Sheet = "Sales", Range = "D5,D7,D8" },
        new SetPrintAreaOp { Sheet = "Sales", Range = "D5,D7,D8" },
        new AddSparklineOp { Sheet = "Sales", DataRange = "B2:E5", At = "F2,F4,F5", Kind = CellsSparklineTypes.Line },

        // Without a sheet, an embedded object or a removal names no target, while a sheet-level
        // operation runs on the active sheet.
        new InsertChartOp { Kind = CellsChartTypes.Column, DataRange = "A1:C5", At = "E2:L18" },
        new SetChartOp { Index = 1, Title = "Revised" },
        new DeleteChartOp { Index = 1 },
        new InsertPivotOp { SourceRange = "Sales!A1:D100", At = "A1", Values = [new CellsPivotValueField { Field = "Q1" }] },
        new RefreshPivotOp { Name = "PivotTable1" },
        new InsertImageOp { Path = "assets/logo.png", At = "H1" },
        new ClearTabColorOp(),
        new UnfreezePanesOp(),
        new ClearAutoFilterOp(),
        new ClearPrintAreaOp(),
        new GroupRowsOp { First = 2, Last = 5 },
        new UngroupRowsOp { First = 2, Last = 5 },
        new GroupColumnsOp { First = "B", Last = "D" },
        new UngroupColumnsOp { First = "B", Last = "D" },
        new ProtectSheetOp(),
        new UnprotectSheetOp(),
        new SetPageSetupOp { Orientation = "landscape" },
        new SetTabColorOp { Color = "#4472C4" },
        new MoveSheetOp { To = 2 },
        new SetSheetVisibilityOp { Visibility = CellsSheetVisibilities.Hidden },
        new SetActiveSheetOp(),
        new SetSheetViewOp { ZoomPercent = 90 },
        new DeleteSheetOp(),
    ];

    /// <summary>
    /// Each row: the operation's name, its target as sheet!range (<c>-</c> for none, an empty
    /// part for null), whether the diff sees it, its cell cost, and its attempted targets on a
    /// workbook whose active sheet is Sheet1.
    /// </summary>
    private static readonly string[] Expected =
    [
        "set_values | Sales!A6:D6 | diffed | 4 | cell/Sales!A6:D6",
        "set_formula | !E2:E6 | diffed | 5 | cell/Sheet1!E2:E6",
        "clear_range | !F1:F10 | diffed | 10 | cell/Sheet1!F1:F10",
        "replace_text | Sales!A2:A6 | diffed | 5 | cell/Sales!A2:A6",
        "copy_range | Summary!B2 | diffed | 9 | cell/Summary!B2",
        "import_range | Summary!F2:I8 | diffed | 28 | cell/Summary!F2:I8",
        "import_sheet | EU! | diffed | 0 | sheet/EU",
        "format_range | !A1:E1 | unseen | 5 | cell/Sheet1!A1:E1",
        "merge_cells | !A1:E1 | unseen | 5 | cell/Sheet1!A1:E1",
        "unmerge_cells | !A1:E1 | unseen | 5 | cell/Sheet1!A1:E1",
        "insert_rows | ! | diffed | 0 | sheet/Sheet1",
        "delete_rows | ! | diffed | 0 | sheet/Sheet1",
        "insert_columns | ! | diffed | 0 | sheet/Sheet1",
        "delete_columns | ! | diffed | 0 | sheet/Sheet1",
        "resize_columns | ! | unseen | 0 | sheet/Sheet1",
        "resize_rows | ! | unseen | 0 | sheet/Sheet1",
        "insert_sheet | Summary! | diffed | 0 | sheet/Summary",
        "rename_sheet | Archive! | diffed | 0 | sheet/Archive",
        "delete_sheet | Obsolete! | diffed | 0 | sheet/Obsolete",
        "set_sheet_visibility | Archive! | unseen | 0 | sheet/Archive",
        "freeze_panes | ! | unseen | 0 | sheet/Sheet1",
        "unfreeze_panes | Sales! | unseen | 0 | sheet/Sales",
        "clear_autofilter | Sales! | unseen | 0 | sheet/Sales",
        "clear_tab_color | Sales! | unseen | 0 | sheet/Sales",
        "clear_print_area | Sales! | unseen | 0 | sheet/Sales",
        "insert_chart | Sales! | unseen | 0 | sheet/Sales",
        "insert_pivot | Summary! | unseen | 0 | sheet/Summary",
        "set_page_setup | Sales! | unseen | 0 | sheet/Sales",
        "set_print_area | Sales!A1:H50 | unseen | 400 | cell/Sales!A1:H50",
        "insert_image | Sales! | unseen | 0 | sheet/Sales",
        "refresh_pivot | Summary! | unseen | 0 | sheet/Summary",
        "define_table | Sales!A1:D20 | unseen | 80 | cell/Sales!A1:D20",
        "resize_table | Sales!A1:D25 | unseen | 100 | cell/Sales!A1:D25",
        "set_autofilter | Sales!A1:D20 | unseen | 80 | cell/Sales!A1:D20",
        "sort_range | Sales!A2:D20 | diffed | 76 | cell/Sales!A2:D20",
        "set_validation | Sales!C2:C100 | unseen | 99 | cell/Sales!C2:C100",
        "define_name | - | unseen | 0 | name/TaxRate",
        "delete_name | - | unseen | 0 | name/ObsoleteRange",
        "add_comment | Sales!B2 | unseen | 1 | cell/Sales!B2",
        "set_comment | Sales!B2 | unseen | 1 | cell/Sales!B2",
        "delete_comment | Sales!C3 | unseen | 1 | cell/Sales!C3",
        "protect_sheet | Sales! | unseen | 0 | sheet/Sales",
        "unprotect_sheet | Archive! | unseen | 0 | sheet/Archive",
        "group_rows | Sales! | unseen | 0 | sheet/Sales",
        "ungroup_rows | Sales! | unseen | 0 | sheet/Sales",
        "group_columns | Sales! | unseen | 0 | sheet/Sales",
        "ungroup_columns | Sales! | unseen | 0 | sheet/Sales",
        "clear_validation | Sales!C2:C100 | unseen | 99 | cell/Sales!C2:C100",
        "delete_duplicates | Sales!A1:D100 | diffed | 400 | cell/Sales!A1:D100",
        "protect_workbook | - | unseen | 0 | workbook/protection",
        "unprotect_workbook | - | unseen | 0 | workbook/protection",
        "set_hyperlink | Sales!A1 | unseen | 1 | cell/Sales!A1",
        "delete_hyperlink | Sales!B2 | unseen | 1 | cell/Sales!B2",
        "set_chart | Sales! | unseen | 0 | sheet/Sales",
        "add_conditional_format | Sales!C2:C100 | unseen | 99 | cell/Sales!C2:C100",
        "clear_conditional_formats | Sales!D2:D100 | unseen | 99 | cell/Sales!D2:D100",
        "move_sheet | Summary! | unseen | 0 | sheet/Summary",
        "set_borders | Sales!A1:D5 | unseen | 20 | cell/Sales!A1:D5",
        "set_default_font | - | unseen | 0 | workbook/default-font",
        "set_tab_color | Sales! | unseen | 0 | sheet/Sales",
        "set_sheet_view | Sales! | unseen | 0 | sheet/Sales",
        "delete_chart | Sales! | unseen | 0 | sheet/Sales",
        "add_sparkline | Sales!F2:F5 | unseen | 4 | cell/Sales!F2:F5",
        "set_active_sheet | Sales! | unseen | 0 | sheet/Sales",
        "set_values | !B2:C3 | diffed | 4 | cell/Sheet1!B2:C3",
        "set_values | Sales!C3 | diffed | 1 | cell/Sales!C3",
        "copy_range | Sales!D4 | diffed | 4 | cell/Sales!D4",
        "copy_range | Sales! | diffed | 4 | sheet/Sales",
        "import_range | Summary!B2:D3 | diffed | 6 | cell/Summary!B2:D3",
        "import_range | Sales!XFD1048576 | diffed | 6 | cell/Sales!XFD1048576",
        "import_sheet | Totals! | diffed | 0 | sheet/Totals",
        "import_sheet | ! | diffed | 0 | sheet/eu_raw",
        "set_validation | Sales!D5:D8 | unseen | 4 | cell/Sales!D5:D8",
        "clear_validation | Sales!D5:D8 | unseen | 4 | cell/Sales!D5:D8",
        "add_conditional_format | Sales!D5:D8 | unseen | 4 | cell/Sales!D5:D8",
        "clear_conditional_formats | Sales!D5:D8 | unseen | 4 | cell/Sales!D5:D8",
        "set_print_area | Sales!D5:D8 | unseen | 4 | cell/Sales!D5:D8",
        "add_sparkline | Sales!F2:F5 | unseen | 4 | cell/Sales!F2:F5",
        "insert_chart | - | unseen | 0 | sheet/Sheet1",
        "set_chart | - | unseen | 0 | sheet/Sheet1",
        "delete_chart | - | unseen | 0 | sheet/Sheet1",
        "insert_pivot | - | unseen | 0 | sheet/Sheet1",
        "refresh_pivot | - | unseen | 0 | sheet/Sheet1",
        "insert_image | - | unseen | 0 | sheet/Sheet1",
        "clear_tab_color | - | unseen | 0 | sheet/Sheet1",
        "unfreeze_panes | - | unseen | 0 | sheet/Sheet1",
        "clear_autofilter | - | unseen | 0 | sheet/Sheet1",
        "clear_print_area | - | unseen | 0 | sheet/Sheet1",
        "group_rows | ! | unseen | 0 | sheet/Sheet1",
        "ungroup_rows | ! | unseen | 0 | sheet/Sheet1",
        "group_columns | ! | unseen | 0 | sheet/Sheet1",
        "ungroup_columns | ! | unseen | 0 | sheet/Sheet1",
        "protect_sheet | ! | unseen | 0 | sheet/Sheet1",
        "unprotect_sheet | ! | unseen | 0 | sheet/Sheet1",
        "set_page_setup | ! | unseen | 0 | sheet/Sheet1",
        "set_tab_color | ! | unseen | 0 | sheet/Sheet1",
        "move_sheet | ! | unseen | 0 | sheet/Sheet1",
        "set_sheet_visibility | ! | unseen | 0 | sheet/Sheet1",
        "set_active_sheet | ! | unseen | 0 | sheet/Sheet1",
        "set_sheet_view | ! | unseen | 0 | sheet/Sheet1",
        "delete_sheet | ! | diffed | 0 | sheet/Sheet1",
    ];

    [Fact]
    public void EveryOperationOfTheVocabularyHasARow()
    {
        Assert.Equal(
            CellsOp.Catalog.Names.Order(StringComparer.Ordinal),
            Samples.Select(static op => CellsOp.Catalog.NameOf(op)).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Footprint_PlacesEachOperation()
    {
        // One new workbook stays within what evaluation mode opens, so the rows run unlicensed too.
        using var fixture = new CellsFixture();
        CellsSession session = ProductTestBudgets.StartEngine<CellsModule, CellsSession>(
            (budgets, writer) => new CellsSession(fixture.Outputs(writer), budgets, new CellsWorkbookLoader(budgets, fixture.Outputs(writer))));
        using var workbook = new Workbook();
        using InputResourceScope inputs = session.Budgets.Inputs.CreateScope();
        using var sources = new CellsImportSources(session.Loader, session.Budgets, secrets: null);
        var model = new CellsEditModel(session.Budgets, workbook, secrets: null, inputs, sources, new CellsProtectionTracker());

        string[] actual = [.. Samples.Select(op => Row(op, model))];

        Assert.Equal(Expected, actual);
    }

    /// <summary>
    /// What each operation changes under protection when every sheet and the structure are
    /// protected and only Sales!Z1 is unlocked: a sheet, the structure, or nothing.
    /// </summary>
    [Fact]
    public void Protection_ObservesEachOperation()
    {
        using var workbook = new Workbook();
        foreach (string name in (string[])["Sales", "Summary", "Archive", "Obsolete", "Old"])
        {
            workbook.Worksheets.Add(name);
        }

        foreach (Worksheet sheet in workbook.Worksheets)
        {
            sheet.Protect(ProtectionType.All);
        }

        Style unlocked = workbook.Worksheets["Sales"].Cells["Z1"].GetStyle();
        unlocked.IsLocked = false;
        workbook.Worksheets["Sales"].Cells["Z1"].SetStyle(unlocked);
        workbook.Protect(ProtectionType.Structure, null);
        CellsOp[] unlockedOnly =
        [
            new SetValuesOp { Sheet = "Sales", Range = "Z1", Values = [[1d]] },
            new SetFormulaOp { Sheet = "Sales", Range = "Z1", Formula = "=1" },
            new ClearRangeOp { Sheet = "Sales", Range = "Z1", What = CellsClearTargets.Contents },
            new ClearRangeOp { Sheet = "Sales", Range = "Z1", What = CellsClearTargets.Formats },
            new ClearRangeOp { Sheet = "Sales", Range = "Z1", What = CellsClearTargets.Everything },
            new ReplaceTextOp { Sheet = "Sales", Range = "Z1", Find = "a", Replace = "b" },
            new SortRangeOp { Sheet = "Sales", Range = "Z1", By = [new CellsSortKey { Column = "Z" }] },
            new DeleteDuplicatesOp { Sheet = "Sales", Range = "Z1" },
            new CopyRangeOp { Sheet = "Sales", From = "A1", To = "Z1" },
            new ImportRangeOp { Sheet = "Sales", Path = "D:/data/eu_raw.xlsx", From = "A1", To = "Z1" },
            new FormatRangeOp { Sheet = "Sales", Range = "Z1", Style = new CellsStyleData { Bold = true } },
            new SetBordersOp { Sheet = "Sales", Range = "Z1", Edges = [CellsBorderEdges.Outline] },
            new MergeCellsOp { Sheet = "Sales", Range = "Z1" },
            new UnmergeCellsOp { Sheet = "Sales", Range = "Z1" },
            new SetValidationOp { Sheet = "Sales", Range = "Z1", Kind = CellsValidationTypes.WholeNumber, Operator = CellsValidationOperators.Between, Value1 = "0", Value2 = "9" },
            new ClearValidationOp { Sheet = "Sales", Range = "Z1" },
            new AddConditionalFormatOp
            {
                Sheet = "Sales",
                Range = "Z1",
                Rule = new CellsConditionalRule { Kind = CellsConditionalRuleKinds.CellValue, Operator = CellsValidationOperators.GreaterThan, Value1 = "1" },
            },
            new ClearConditionalFormatsOp { Sheet = "Sales", Range = "Z1" },
            new DefineTableOp { Sheet = "Sales", Range = "Z1", Name = "Unlocked" },
            new ResizeTableOp { Sheet = "Sales", Name = "Unlocked", Range = "Z1" },
            new SetAutoFilterOp { Sheet = "Sales", Range = "Z1" },
            new AddSparklineOp { Sheet = "Sales", DataRange = "B2:E2", At = "Z1", Kind = CellsSparklineTypes.Line },
            new AddCommentOp { Sheet = "Sales", Cell = "Z1", Text = "Note" },
            new SetCommentOp { Sheet = "Sales", Cell = "Z1", Text = "Note" },
            new DeleteCommentOp { Sheet = "Sales", Cell = "Z1" },
            new SetHyperlinkOp { Sheet = "Sales", Cell = "Z1", Target = "Summary!A1" },
            new DeleteHyperlinkOp { Sheet = "Sales", Cell = "Z1" },
        ];

        string[] actual = [.. Samples.Concat(unlockedOnly).Select(op => $"{CellsOp.Catalog.NameOf(op)} | {Observed(CellsProtectionTracker.Observe(workbook, op))}")];

        Assert.Equal(ExpectedProtection, actual);
    }

    private static readonly string[] ExpectedProtection =
    [
        "set_values | sheet Sales",
        "set_formula | sheet Sheet1",
        "clear_range | sheet Sheet1",
        "replace_text | sheet Sales",
        "copy_range | sheet Summary",
        "import_range | sheet Summary",
        "import_sheet | structure",
        "format_range | sheet Sheet1",
        "merge_cells | sheet Sheet1",
        "unmerge_cells | sheet Sheet1",
        "insert_rows | sheet Sheet1",
        "delete_rows | sheet Sheet1",
        "insert_columns | sheet Sheet1",
        "delete_columns | sheet Sheet1",
        "resize_columns | sheet Sheet1",
        "resize_rows | sheet Sheet1",
        "insert_sheet | structure",
        "rename_sheet | structure",
        "delete_sheet | structure",
        "set_sheet_visibility | structure",
        "freeze_panes | none",
        "unfreeze_panes | sheet Sales",
        "clear_autofilter | sheet Sales",
        "clear_tab_color | sheet Sales",
        "clear_print_area | sheet Sales",
        "insert_chart | sheet Sales",
        "insert_pivot | sheet Summary",
        "set_page_setup | none",
        "set_print_area | none",
        "insert_image | sheet Sales",
        "refresh_pivot | sheet Summary",
        "define_table | sheet Sales",
        "resize_table | sheet Sales",
        "set_autofilter | sheet Sales",
        "sort_range | sheet Sales",
        "set_validation | sheet Sales",
        "define_name | none",
        "delete_name | none",
        "add_comment | sheet Sales",
        "set_comment | sheet Sales",
        "delete_comment | sheet Sales",
        "protect_sheet | sheet Sales",
        "unprotect_sheet | none",
        "group_rows | sheet Sales",
        "ungroup_rows | sheet Sales",
        "group_columns | sheet Sales",
        "ungroup_columns | sheet Sales",
        "clear_validation | sheet Sales",
        "delete_duplicates | sheet Sales",
        "protect_workbook | structure",
        "unprotect_workbook | none",
        "set_hyperlink | sheet Sales",
        "delete_hyperlink | sheet Sales",
        "set_chart | sheet Sales",
        "add_conditional_format | sheet Sales",
        "clear_conditional_formats | sheet Sales",
        "move_sheet | structure",
        "set_borders | sheet Sales",
        "set_default_font | none",
        "set_tab_color | structure",
        "set_sheet_view | none",
        "delete_chart | sheet Sales",
        "add_sparkline | sheet Sales",
        "set_active_sheet | none",
        "set_values | sheet Sheet1",
        "set_values | sheet Sales",
        "copy_range | sheet Sales",
        "copy_range | none",
        "import_range | sheet Summary",
        "import_range | sheet Sales",
        "import_sheet | structure",
        "import_sheet | structure",
        "set_validation | sheet Sales",
        "clear_validation | sheet Sales",
        "add_conditional_format | sheet Sales",
        "clear_conditional_formats | sheet Sales",
        "set_print_area | none",
        "add_sparkline | sheet Sales",
        "insert_chart | sheet Sheet1",
        "set_chart | sheet Sheet1",
        "delete_chart | sheet Sheet1",
        "insert_pivot | sheet Sheet1",
        "refresh_pivot | sheet Sheet1",
        "insert_image | sheet Sheet1",
        "clear_tab_color | sheet Sheet1",
        "unfreeze_panes | sheet Sheet1",
        "clear_autofilter | sheet Sheet1",
        "clear_print_area | sheet Sheet1",
        "group_rows | sheet Sheet1",
        "ungroup_rows | sheet Sheet1",
        "group_columns | sheet Sheet1",
        "ungroup_columns | sheet Sheet1",
        "protect_sheet | sheet Sheet1",
        "unprotect_sheet | none",
        "set_page_setup | none",
        "set_tab_color | structure",
        "move_sheet | structure",
        "set_sheet_visibility | structure",
        "set_active_sheet | none",
        "set_sheet_view | none",
        "delete_sheet | structure",
        "set_values | none",
        "set_formula | none",
        "clear_range | none",
        "clear_range | sheet Sales",
        "clear_range | sheet Sales",
        "replace_text | sheet Sales",
        "sort_range | sheet Sales",
        "delete_duplicates | sheet Sales",
        "copy_range | sheet Sales",
        "import_range | sheet Sales",
        "format_range | sheet Sales",
        "set_borders | sheet Sales",
        "merge_cells | sheet Sales",
        "unmerge_cells | sheet Sales",
        "set_validation | sheet Sales",
        "clear_validation | sheet Sales",
        "add_conditional_format | sheet Sales",
        "clear_conditional_formats | sheet Sales",
        "define_table | sheet Sales",
        "resize_table | sheet Sales",
        "set_autofilter | sheet Sales",
        "add_sparkline | sheet Sales",
        "add_comment | sheet Sales",
        "set_comment | sheet Sales",
        "delete_comment | sheet Sales",
        "set_hyperlink | sheet Sales",
        "delete_hyperlink | sheet Sales",
    ];

    private static string Observed(ProtectedChange change) =>
        change.Sheet is { } sheet ? $"sheet {sheet}{(change.KeptPassword ? " kept password" : string.Empty)}"
            : change.Structure ? "structure"
            : "none";

    private static string Row(CellsOp op, CellsEditModel model)
    {
        var batch = new CellsOpsBatch { Ops = [op] };
        string target = OpsFootprint.Collect(batch) is [var found] ? $"{found.Sheet}!{found.Range}" : "-";
        bool diffed = OpsFootprint.Collect(batch, cellsOnly: true).Count > 0;
        return $"{CellsOp.Catalog.NameOf(op)} | {target} | {(diffed ? "diffed" : "unseen")} | {OpsFootprint.CellCost(op)} | {string.Join(", ", model.AttemptedTargets(op))}";
    }
}
