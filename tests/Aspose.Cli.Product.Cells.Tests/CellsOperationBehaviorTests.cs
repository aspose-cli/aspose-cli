using Aspose.Cells;
using Aspose.Cells.Pivot;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// One persisted behavior per operation: each case applies its ops to the sales fixture
/// (Data!A1:C3 with a bold header and SUM totals, a Second sheet and a hidden Backstage
/// sheet), reopens the saved workbook and asserts the documented change.
/// </summary>
public sealed class CellsOperationBehaviorTests : IClassFixture<CellsFixture>
{
    private static readonly Dictionary<string, (string Ops, Action<Workbook> Assert)> Cases = new(StringComparer.Ordinal)
    {
        ["set_formula"] = (
            """
            { "op": "set_formula", "sheet": "Data", "range": "D2:D3", "formula": "=B2+C2" },
            { "op": "set_formula", "sheet": "Data", "range": "E2", "formula": "=SUM(B2:C2)" },
            { "op": "set_formula", "sheet": "Second", "range": "A3", "formula": "=FILTER(Data!A1:C3,Data!A1:A3<>\"East\")" },
            { "op": "set_formula", "sheet": "Second", "range": "E1", "formula": "=FILTER(Data!B2:B3,Data!A2:A3=\"East\")" },
            { "op": "set_values", "sheet": "Second", "range": "G2", "values": [["keep"]] },
            { "op": "set_formula", "sheet": "Second", "range": "G1", "formula": "=FILTER(Data!A1:A3,Data!A1:A3<>\"East\")" },
            { "op": "set_values", "sheet": "Second", "range": "I1", "values": [[0.03, 0], [0.1, 210], [0.2, 1410], [0.25, 2660], [0.3, 4410]] },
            { "op": "set_values", "sheet": "Second", "range": "L1", "values": [[41092.29]] },
            { "op": "set_values", "sheet": "Second", "range": "L7", "values": [[9400]] },
            { "op": "set_formula", "sheet": "Second", "range": "M1", "formula": "=MAX(L1*$I$1:$I$5-$J$1:$J$5)" },
            { "op": "set_formula", "sheet": "Second", "range": "M6:M7", "formula": "=MAX(L6*$I$1:$I$5-$J$1:$J$5)" }
            """,
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
                Assert.Equal("=B2+C2", cells["D2"].Formula);
                Assert.Equal("=B3+C3", cells["D3"].Formula);
                Assert.Equal(2700d, cells["D2"].DoubleValue);
                Assert.Equal(2700d, cells["E2"].DoubleValue);
                // Like Excel 365, a result that is an array spills...
                Aspose.Cells.Cells second = workbook.Worksheets["Second"].Cells;
                Assert.True(second["A3"].IsDynamicArrayFormula);
                Assert.Equal("Region", second["A3"].StringValue);
                Assert.Equal("Total", second["A4"].StringValue);
                Assert.Equal(1500d, second["C4"].DoubleValue);
                // A function whose result follows the data stays dynamic while it returns one cell.
                Assert.True(second["E1"].IsDynamicArrayFormula);
                Assert.Equal(1200d, second["E1"].DoubleValue);
                // A spill onto a cell that holds a value keeps the value and shows #SPILL!.
                Assert.Equal("keep", second["G2"].StringValue);
                Assert.Equal("#SPILL!", second["G1"].StringValue);
                // ...and array arithmetic inside a function covers whole ranges, without implicit
                // intersection, on a single cell and on every cell of a filled range.
                Assert.Equal(7917.687, second["M1"].DoubleValue, 6);
                Assert.Equal(0d, second["M6"].DoubleValue);
                Assert.Equal(730d, second["M7"].DoubleValue, 6);
                Assert.Equal("=MAX(L7*$I$1:$I$5-$J$1:$J$5)", second["M7"].Formula);
            }),
        ["clear_range"] = (
            """{ "op": "clear_range", "sheet": "Data", "range": "A1:C1", "what": "formats" }""",
            static workbook =>
            {
                Cell header = workbook.Worksheets["Data"].Cells["A1"];
                Assert.Equal("Region", header.StringValue);
                Assert.False(header.GetStyle().Font.IsBold);
            }),
        ["copy_range"] = (
            """{ "op": "copy_range", "sheet": "Data", "from": "A1:C2", "to": "Second!B5" }""",
            static workbook =>
            {
                Aspose.Cells.Cells second = workbook.Worksheets["Second"].Cells;
                Assert.Equal("Region", second["B5"].StringValue);
                Assert.True(second["B5"].GetStyle().Font.IsBold);
                Assert.Equal(1500d, second["D6"].DoubleValue);
                Assert.Equal("second-sheet-marker", second["A1"].StringValue);
            }),
        ["merge_cells"] = (
            """{ "op": "merge_cells", "sheet": "Data", "range": "A5:C6" }""",
            static workbook =>
            {
                CellArea merged = Assert.Single(workbook.Worksheets["Data"].Cells.GetMergedAreas());
                Assert.Equal((4, 0, 5, 2), (merged.StartRow, merged.StartColumn, merged.EndRow, merged.EndColumn));
            }),
        ["unmerge_cells"] = (
            """{ "op": "merge_cells", "sheet": "Data", "range": "A5:C6" }, { "op": "unmerge_cells", "sheet": "Data", "range": "A5:C6" }""",
            static workbook => Assert.Empty(workbook.Worksheets["Data"].Cells.GetMergedAreas())),
        ["insert_rows"] = (
            """{ "op": "insert_rows", "sheet": "Data", "at": 2, "count": 2 }""",
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
                Assert.Equal("East", cells["A4"].StringValue);
                Assert.Equal("=SUM(B4:B4)", cells["B5"].Formula);
            }),
        ["delete_rows"] = (
            """{ "op": "delete_rows", "sheet": "Data", "at": 2 }""",
            static workbook => Assert.Equal("Total", workbook.Worksheets["Data"].Cells["A2"].StringValue)),
        ["insert_columns"] = (
            """{ "op": "insert_columns", "sheet": "Data", "at": "B" }""",
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
                Assert.Equal("Q1", cells["C1"].StringValue);
                Assert.Equal("=SUM(C2:C2)", cells["C3"].Formula);
            }),
        ["delete_columns"] = (
            """{ "op": "delete_columns", "sheet": "Data", "at": "B" }""",
            static workbook => Assert.Equal("Q2", workbook.Worksheets["Data"].Cells["B1"].StringValue)),
        ["resize_rows"] = (
            """{ "op": "resize_rows", "sheet": "Data", "from": 1, "to": 2, "height": 30 }""",
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
                Assert.Equal(30d, cells.GetRowHeight(0));
                Assert.Equal(30d, cells.GetRowHeight(1));
                Assert.NotEqual(30d, cells.GetRowHeight(2));
            }),
        ["resize_columns"] = (
            """{ "op": "resize_columns", "sheet": "Data", "from": "B", "to": "C", "width": 20 }""",
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
                Assert.Equal(20d, cells.GetColumnWidth(1));
                Assert.Equal(20d, cells.GetColumnWidth(2));
                Assert.NotEqual(20d, cells.GetColumnWidth(0));
            }),
        ["add_sheet"] = (
            """{ "op": "add_sheet", "name": "Summary", "position": 0 }""",
            static workbook => Assert.Equal(["Summary", "Data", "Second", "Backstage"], SheetNames(workbook))),
        ["rename_sheet"] = (
            """{ "op": "set_formula", "sheet": "Data", "range": "D1", "formula": "=Second!A1" }, { "op": "rename_sheet", "sheet": "Second", "to": "Notes" }""",
            static workbook =>
            {
                Assert.Equal(["Data", "Notes", "Backstage"], SheetNames(workbook));
                Assert.Equal("=Notes!A1", workbook.Worksheets["Data"].Cells["D1"].Formula);
            }),
        ["delete_sheet"] = (
            """{ "op": "delete_sheet", "sheet": "Second" }""",
            static workbook => Assert.Equal(["Data", "Backstage"], SheetNames(workbook))),
        ["set_sheet_visibility"] = (
            """{ "op": "set_sheet_visibility", "sheet": "Backstage", "hidden": false }, { "op": "set_sheet_visibility", "sheet": "Second", "hidden": true }""",
            static workbook =>
            {
                Assert.True(workbook.Worksheets["Backstage"].IsVisible);
                Assert.False(workbook.Worksheets["Second"].IsVisible);
            }),
        ["move_sheet"] = (
            """{ "op": "move_sheet", "sheet": "Backstage", "position": 0 }""",
            static workbook => Assert.Equal(["Backstage", "Data", "Second"], SheetNames(workbook))),
        ["set_active_sheet"] = (
            """{ "op": "set_active_sheet", "sheet": "Second" }""",
            static workbook => Assert.Equal("Second", workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex].Name)),
        ["freeze_panes"] = (
            """{ "op": "freeze_panes", "sheet": "Data", "cell": "B2" }""",
            static workbook =>
            {
                Assert.True(workbook.Worksheets["Data"].GetFreezedPanes(out int row, out int column, out _, out _));
                Assert.Equal((1, 1), (row, column));
            }),
        ["sort_range"] = (
            """
            { "op": "set_values", "sheet": "Second", "range": "A1", "values": [["Name","Score"],["Ann",2],["Bob",9],["Cid",5]] },
            { "op": "sort_range", "sheet": "Second", "range": "A1:B4", "hasHeader": true, "by": [{ "column": "B", "order": "desc" }] }
            """,
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Second"].Cells;
                Assert.Equal(["Name", "Bob", "Cid", "Ann"], Enumerable.Range(0, 4).Select(row => cells[row, 0].StringValue));
            }),
        ["set_autofilter"] = (
            """{ "op": "set_autofilter", "sheet": "Data", "range": "A1:C3" }""",
            static workbook => Assert.Equal("A1:C3", workbook.Worksheets["Data"].AutoFilter.Range)),
        ["set_autofilter off"] = (
            """{ "op": "set_autofilter", "sheet": "Data", "range": "A1:C3" }, { "op": "set_autofilter", "sheet": "Data", "off": true }""",
            static workbook => Assert.True(string.IsNullOrEmpty(workbook.Worksheets["Data"].AutoFilter.Range))),
        ["remove_duplicates"] = (
            """
            { "op": "set_values", "sheet": "Second", "range": "A1", "values": [["Name","Team"],["Ann","A"],["Ann","A"],["Bob","B"]] },
            { "op": "remove_duplicates", "sheet": "Second", "range": "A1:B4", "hasHeader": true }
            """,
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Second"].Cells;
                Assert.Equal(["Name", "Ann", "Bob"], Enumerable.Range(0, 3).Select(row => cells[row, 0].StringValue));
                Assert.Equal(string.Empty, cells["A4"].StringValue);
            }),
        ["set_validation"] = (
            """{ "op": "set_validation", "sheet": "Data", "range": "B2:C9", "type": "wholeNumber", "operator": "between", "value1": "0", "value2": "100", "errorMessage": "0 to 100" }""",
            static workbook =>
            {
                Validation validation = Assert.Single(workbook.Worksheets["Data"].Validations.Cast<Validation>());
                Assert.Equal(ValidationType.WholeNumber, validation.Type);
                Assert.Equal(OperatorType.Between, validation.Operator);
                Assert.Equal(("=0", "=100"), (validation.Formula1, validation.Formula2));
                Assert.Equal("0 to 100", validation.ErrorMessage);
                CellArea area = Assert.Single(validation.Areas);
                Assert.Equal((1, 1, 8, 2), (area.StartRow, area.StartColumn, area.EndRow, area.EndColumn));
            }),
        ["clear_validation"] = (
            """{ "op": "set_validation", "sheet": "Data", "range": "B2:B9", "type": "decimal", "operator": "greaterThan", "value1": "0" }, { "op": "clear_validation", "sheet": "Data", "range": "B2:B9" }""",
            static workbook => Assert.Empty(workbook.Worksheets["Data"].Validations.Cast<Validation>().SelectMany(static v => v.Areas))),
        ["define_name"] = (
            """{ "op": "define_name", "name": "Rate", "refersTo": "Data!$B$2" }""",
            static workbook => Assert.Equal("=Data!$B$2", workbook.Worksheets.Names["Rate"].RefersTo)),
        ["delete_name"] = (
            """{ "op": "define_name", "name": "Rate", "refersTo": "Data!$B$2" }, { "op": "delete_name", "name": "Rate" }""",
            static workbook => Assert.Null(workbook.Worksheets.Names["Rate"])),
        ["add_comment"] = (
            """{ "op": "add_comment", "sheet": "Data", "cell": "B2", "text": "Check Q1", "author": "Finance" }""",
            static workbook =>
            {
                Comment comment = workbook.Worksheets["Data"].Comments["B2"];
                Assert.Equal(("Check Q1", "Finance"), (comment.Note, comment.Author));
            }),
        ["edit_comment"] = (
            """{ "op": "add_comment", "sheet": "Data", "cell": "B2", "text": "Check Q1" }, { "op": "edit_comment", "sheet": "Data", "cell": "B2", "text": "Checked" }""",
            static workbook => Assert.Equal("Checked", workbook.Worksheets["Data"].Comments["B2"].Note)),
        ["delete_comment"] = (
            """{ "op": "add_comment", "sheet": "Data", "cell": "B2", "text": "Check Q1" }, { "op": "delete_comment", "sheet": "Data", "cell": "B2" }""",
            static workbook => Assert.Empty(workbook.Worksheets["Data"].Comments)),
        ["group_rows"] = (
            """{ "op": "group_rows", "sheet": "Data", "from": 2, "to": 3, "collapse": true }""",
            static workbook =>
            {
                Aspose.Cells.Cells cells = workbook.Worksheets["Data"].Cells;
                Assert.Equal((1, true), (cells.Rows[1].GroupLevel, cells.Rows[1].IsHidden));
                Assert.Equal(1, cells.Rows[2].GroupLevel);
                Assert.Equal(0, cells.Rows[0].GroupLevel);
            }),
        ["ungroup_rows"] = (
            """{ "op": "group_rows", "sheet": "Data", "from": 2, "to": 3 }, { "op": "ungroup_rows", "sheet": "Data", "from": 2, "to": 3 }""",
            static workbook => Assert.Equal(0, workbook.Worksheets["Data"].Cells.Rows[1].GroupLevel)),
        ["group_columns"] = (
            """{ "op": "group_columns", "sheet": "Data", "from": "B", "to": "C" }""",
            static workbook =>
            {
                ColumnCollection columns = workbook.Worksheets["Data"].Cells.Columns;
                Assert.Equal((0, 1, 1), (columns[0].GroupLevel, columns[1].GroupLevel, columns[2].GroupLevel));
            }),
        ["ungroup_columns"] = (
            """{ "op": "group_columns", "sheet": "Data", "from": "B", "to": "C" }, { "op": "ungroup_columns", "sheet": "Data", "from": "B", "to": "C" }""",
            static workbook => Assert.Equal(0, workbook.Worksheets["Data"].Cells.Columns[1].GroupLevel)),
        ["set_hyperlink"] = (
            """{ "op": "set_hyperlink", "sheet": "Data", "cell": "E1", "url": "https://example.com/report", "display": "Open" }""",
            static workbook =>
            {
                Worksheet data = workbook.Worksheets["Data"];
                Hyperlink link = Assert.Single(data.Hyperlinks.Cast<Hyperlink>());
                Assert.Equal("https://example.com/report", link.Address);
                Assert.Equal("Open", data.Cells["E1"].StringValue);
            }),
        ["remove_hyperlink"] = (
            """{ "op": "set_hyperlink", "sheet": "Data", "cell": "E1", "url": "https://example.com" }, { "op": "remove_hyperlink", "sheet": "Data", "cell": "E1" }""",
            static workbook => Assert.Empty(workbook.Worksheets["Data"].Hyperlinks)),
        ["protect_workbook"] = (
            """{ "op": "protect_workbook" }""",
            static workbook => Assert.True(workbook.Settings.IsProtected)),
        ["unprotect_workbook"] = (
            """{ "op": "protect_workbook" }, { "op": "unprotect_workbook" }""",
            static workbook => Assert.False(workbook.Settings.IsProtected)),
        ["unprotect_sheet"] = (
            """{ "op": "protect_sheet", "sheet": "Data" }, { "op": "unprotect_sheet", "sheet": "Data" }""",
            static workbook => Assert.False(workbook.Worksheets["Data"].IsProtected)),
        ["set_default_font"] = (
            """{ "op": "set_default_font", "font": "Georgia", "size": 12 }""",
            static workbook =>
            {
                Assert.Equal(("Georgia", 12d), (workbook.DefaultStyle.Font.Name, workbook.DefaultStyle.Font.DoubleSize));
                Assert.Equal("Georgia", workbook.Worksheets["Data"].Cells["B2"].GetStyle().Font.Name);
            }),
        ["set_tab_color"] = (
            """{ "op": "set_tab_color", "sheet": "Data", "color": "#1F4E79" }""",
            static workbook =>
            {
                System.Drawing.Color color = workbook.Worksheets["Data"].TabColor;
                Assert.Equal((0x1F, 0x4E, 0x79), (color.R, color.G, color.B));
            }),
        ["set_sheet_view"] = (
            """{ "op": "set_sheet_view", "sheet": "Data", "gridlines": false, "zoom": 90, "headings": false }""",
            static workbook =>
            {
                Worksheet data = workbook.Worksheets["Data"];
                Assert.Equal((false, 90, false), (data.IsGridlinesVisible, data.Zoom, data.IsRowColumnHeadersVisible));
            }),
        ["set_page_setup"] = (
            """{ "op": "set_page_setup", "sheet": "Data", "orientation": "landscape", "paperSize": "a4", "fitToWidth": 1, "fitToHeight": 0, "footer": "Page &P" }""",
            static workbook =>
            {
                PageSetup setup = workbook.Worksheets["Data"].PageSetup;
                Assert.Equal((PageOrientationType.Landscape, PaperSizeType.PaperA4), (setup.Orientation, setup.PaperSize));
                Assert.Equal((1, 0), (setup.FitToPagesWide, setup.FitToPagesTall));
                Assert.Equal("Page &P", setup.GetFooter(1));
            }),
        ["delete_chart"] = (
            """
            { "op": "create_chart", "sheet": "Data", "type": "column", "dataRange": "A1:C2", "at": "E2:K12" },
            { "op": "create_chart", "sheet": "Data", "type": "line", "dataRange": "A1:C2", "at": "E14:K24" },
            { "op": "delete_chart", "sheet": "Data", "index": 0 }
            """,
            static workbook =>
            {
                Aspose.Cells.Charts.Chart chart = Assert.Single(workbook.Worksheets["Data"].Charts);
                Assert.Equal(Aspose.Cells.Charts.ChartType.Line, chart.Type);
            }),
        ["refresh_pivot"] = (
            """
            { "op": "create_pivot", "sheet": "Second", "sourceRange": "Data!A1:C2", "at": "D1", "name": "Sales", "rows": ["Region"], "values": [{ "field": "Q1" }] },
            { "op": "set_values", "sheet": "Data", "range": "B2", "values": [[4000]] },
            { "op": "refresh_pivot", "sheet": "Second", "name": "sales" }
            """,
            static workbook =>
            {
                PivotTable pivot = workbook.Worksheets["Second"].PivotTables[0];
                CellArea area = pivot.TableRange1;
                Assert.Equal(4000d, workbook.Worksheets["Second"].Cells[area.EndRow, area.EndColumn].DoubleValue);
            }),
    };

    private readonly CellsFixture _fixture;

    public CellsOperationBehaviorTests(CellsFixture fixture) => _fixture = fixture;

    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Operation_ChangesTheWorkbookAsDocumented(string name)
    {
        (string ops, Action<Workbook> assert) = Cases[name];
        string source = _fixture.CreateSalesWorkbook("behavior.xlsx");

        EditResult result = Apply(source, ops, "behavior.out.xlsx");

        Assert.All(result.Applied, static outcome => Assert.Equal(OpStatuses.Ok, outcome.Status));
        using var workbook = new Workbook(result.Output!.Path);
        assert(workbook);
    }

    [Fact]
    public void FormatRangeOverAPivot_ChangesOnlyTheRequestedField()
    {
        string source = _fixture.CreateSalesWorkbook("pivot-format.xlsx");
        string output = Apply(
            source,
            """
            { "op": "create_pivot", "sheet": "Second", "sourceRange": "Data!A1:C2", "at": "D1", "rows": ["Region"], "values": [{ "field": "Q1" }] },
            { "op": "format_range", "sheet": "Second", "range": "D1:E1", "style": { "bg": "#1F4E79", "color": "#FFFFFF" } },
            { "op": "format_range", "sheet": "Second", "range": "D1:E1", "style": { "bold": true } }
            """,
            "pivot-format.out.xlsx").Output!.Path;

        using var workbook = new Workbook(output);
        Style style = workbook.Worksheets["Second"].Cells["D1"].GetStyle();
        Assert.True(style.Font.IsBold);
        Assert.Equal((0x1F, 0x4E, 0x79), (style.ForegroundColor.R, style.ForegroundColor.G, style.ForegroundColor.B));
        Assert.Equal((0xFF, 0xFF, 0xFF), (style.Font.Color.R, style.Font.Color.G, style.Font.Color.B));
    }

    [Fact]
    public void BestEffort_ARejectedPivotLeavesNoPivotBehind()
    {
        string source = _fixture.CreateSalesWorkbook("pivot-rollback.xlsx");
        EditResult result = CellsEdit.Run(_fixture.Session,
            new EditRequest
            {
                Input = source,
                Batch = Parse("""
                { "ops": [
                  { "op": "create_pivot", "sheet": "Second", "sourceRange": "Data!A1:C2", "at": "D1", "rows": ["Region"], "values": [{ "field": "Ghost" }] },
                  { "op": "set_values", "sheet": "Second", "range": "A2", "values": [["after"]] }
                ] }
                """),
                Output = TestOutput.At(_fixture.Temp.File("pivot-rollback.out.xlsx"), overwrite: true),
                Options = new EditCommandOptions { BestEffort = true },
            });

        Assert.Equal([OpStatuses.Failed, OpStatuses.Ok], result.Applied.Select(static outcome => outcome.Status));
        using var workbook = new Workbook(result.Output!.Path);
        Worksheet second = workbook.Worksheets["Second"];
        Assert.Empty(second.PivotTables);
        Assert.Equal(string.Empty, second.Cells["D1"].StringValue);
        Assert.Equal("after", second.Cells["A2"].StringValue);
    }

    [Fact]
    public void BestEffort_AxisTitlesOnAnExistingPieChangeNothing()
    {
        string source = _fixture.CreateSalesWorkbook("pie.xlsx");
        EditResult result = CellsEdit.Run(_fixture.Session,
            new EditRequest
            {
                Input = source,
                Batch = Parse("""
                { "ops": [
                  { "op": "create_chart", "sheet": "Data", "type": "pie", "dataRange": "A1:B2", "at": "E2:K12", "title": "Share" },
                  { "op": "update_chart", "sheet": "Data", "index": 0, "title": "Changed", "axisTitles": { "value": "USD" } }
                ] }
                """),
                Output = TestOutput.At(_fixture.Temp.File("pie.out.xlsx"), overwrite: true),
                Options = new EditCommandOptions { BestEffort = true },
            });

        Assert.Equal([OpStatuses.Ok, OpStatuses.Failed], result.Applied.Select(static outcome => outcome.Status));
        using var workbook = new Workbook(result.Output!.Path);
        Assert.Equal("Share", workbook.Worksheets["Data"].Charts[0].Title.Text);
    }

    [Theory]
    [InlineData("""
        { "op": "add_comment", "sheet": "Data", "cell": "C3", "text": "x" },
        { "op": "edit_comment", "sheet": "Data", "cell": "B2", "text": "x" }
        """, "COMMENT_NOT_FOUND", "B2", "C3", null)]
    [InlineData("""
        { "op": "add_comment", "sheet": "Data", "cell": "C3", "text": "x" },
        { "op": "delete_comment", "sheet": "Data", "cell": "B2" }
        """, "COMMENT_NOT_FOUND", "B2", "C3", null)]
    [InlineData("""
        { "op": "set_hyperlink", "sheet": "Data", "cell": "E1", "url": "https://example.com" },
        { "op": "remove_hyperlink", "sheet": "Data", "cell": "B2" }
        """, "HYPERLINK_NOT_FOUND", "B2", "E1", null)]
    [InlineData("""
        { "op": "define_name", "name": "Rate", "refersTo": "Data!$B$2" },
        { "op": "delete_name", "name": "Rates" }
        """, "NAME_NOT_FOUND", "Rates", "Rate", "Rate")]
    [InlineData("""
        { "op": "create_pivot", "sheet": "Second", "sourceRange": "Data!A1:C2", "at": "D1", "name": "Sales", "rows": ["Region"], "values": [{ "field": "Q1" }] },
        { "op": "refresh_pivot", "sheet": "Second", "name": "Ghost" }
        """, "PIVOT_NOT_FOUND", "Ghost", "Sales", null)]
    public void AnOperationWithoutItsTarget_ListsTheTargetsTheDocumentHas(
        string operations, string code, string requested, string available, string? suggestion)
    {
        CliException error = Assert.Throws<CliException>(() =>
            Apply(_fixture.CreateSalesWorkbook("missing-target.xlsx"), operations, "missing-target.out.xlsx"));

        Assert.Equal(code, error.Code.Name);
        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.Equal(requested, error.Details["requested"]!.GetValue<string>());
        Assert.Equal(available, Assert.Single(error.Details["available"]!.AsArray())!.GetValue<string>());
        Assert.Equal(suggestion, error.Details["suggestions"]?.AsArray().Single()!.GetValue<string>());
    }

    [Fact]
    public void AChartIndexPastTheSheetsCharts_ReportsHowManyTheSheetHas()
    {
        CliException error = Assert.Throws<CliException>(() => Apply(
            _fixture.CreateSalesWorkbook("missing-chart.xlsx"),
            """
            { "op": "create_chart", "sheet": "Data", "type": "column", "dataRange": "A1:C2", "at": "E2:K12" },
            { "op": "delete_chart", "sheet": "Data", "index": 1 }
            """,
            "missing-chart.out.xlsx"));

        Assert.Equal(CellsDiagnostics.ChartNotFound, error.Code);
        Assert.Equal("1", error.Details!["requested"]!.GetValue<string>());
        Assert.Equal(1, error.Details["availableCount"]!.GetValue<int>());
        Assert.Contains("0 through 0", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void AChartNameSeveralChartsShare_IsRefusedAndAMissingNameListsTheCharts()
    {
        string source = _fixture.CreateSalesWorkbook("chart-names.xlsx");
        using (var workbook = new Workbook(source))
        {
            Aspose.Cells.Charts.ChartCollection charts = workbook.Worksheets["Data"].Charts;
            foreach (string name in new[] { "Sales", "Costs", "Sales" })
            {
                charts[charts.Add(Aspose.Cells.Charts.ChartType.Column, 5, 5, 15, 10)].Name = name;
            }

            workbook.Save(source);
        }

        CliException ambiguous = Assert.Throws<CliException>(() => Apply(
            source, """{ "op": "delete_chart", "sheet": "Data", "name": "Sales" }""", "chart-names.out.xlsx"));
        CliException missing = Assert.Throws<CliException>(() => Apply(
            source, """{ "op": "delete_chart", "sheet": "Data", "name": "Revenue" }""", "chart-names.out.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, ambiguous.Code);
        Assert.Contains("at indexes 0, 2", ambiguous.Details!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(CellsDiagnostics.ChartNotFound, missing.Code);
        Assert.Equal(["Sales", "Costs", "Sales"], missing.Details!["available"]!.AsArray().Select(static name => name!.GetValue<string>()));
    }

    [Fact]
    public void ActivatingAHiddenSheet_IsAnOperationsError()
    {
        CliException error = Assert.Throws<CliException>(() => Apply(
            _fixture.CreateSalesWorkbook("hidden-active.xlsx"),
            """{ "op": "set_active_sheet", "sheet": "Backstage" }""",
            "hidden-active.out.xlsx"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    private static string[] SheetNames(Workbook workbook) =>
        workbook.Worksheets.Cast<Worksheet>().Select(static sheet => sheet.Name).ToArray();

    private static CellsOpsBatch Parse(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);

    private EditResult Apply(string path, string operations, string output) =>
        CellsEdit.Run(_fixture.Session,
            new EditRequest { Input = path, Batch = Parse($$"""{ "ops": [ {{operations}} ] }"""), Output = TestOutput.At(_fixture.Temp.File(output), overwrite: true) });
}
