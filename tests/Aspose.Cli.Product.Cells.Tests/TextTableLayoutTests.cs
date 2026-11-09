using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The TEXT_TABLE_LAYOUT detector over row shapes, and <c>cells inspect</c> and
/// <c>cells convert</c> on CSV inputs through the built CLI in evaluation mode.
/// </summary>
public sealed class TextTableLayoutTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void Detect_FindsTheHeaderAfterATitleAndAQueryConditionRow()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((1, "销售明细报表"), (1, "查询条件: 2026-01"), (4, "日期"), (4, "北京"), (4, "上海")), []));

        Assert.Equal(TextTableFindingKind.Preamble, finding.Kind);
        Assert.Equal(2, finding.HeaderRow);
        Assert.Equal([0, 1], finding.Rows);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public void Detect_FindsTheHeaderAfterAnExportRowOfSeveralValuesAndAnEmptyRow(int width)
    {
        // An ERP export: a title, an "export time, query" row of two values, an empty row, then the header.
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((1, "标题 Title"), (2, "导出时间: 2026-10-02"), (0, null), (width, "编码"), (width, "A1"), (width, "A2")), []));

        Assert.Equal(TextTableFindingKind.Preamble, finding.Kind);
        Assert.Equal(3, finding.HeaderRow);
        Assert.Equal([0, 1, 2], finding.Rows);
    }

    [Fact]
    public void Detect_FindsTheHeaderAfterANoteRowOfALabelAndANumberAndAnEmptyRow()
    {
        // A supplier statement: a title, then "record count" and its number in two cells, an empty row, then the header.
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
        [
            new TextRowShape(0, 1, "供应商对账单"), new TextRowShape(1, 2, "记录数", 1), new TextRowShape(2, 0, null),
            new TextRowShape(3, 6, "供应商编码"), new TextRowShape(4, 6, "S001", 2), new TextRowShape(5, 6, "S002", 2),
        ], []));

        Assert.Equal(TextTableFindingKind.Preamble, finding.Kind);
        Assert.Equal(3, finding.HeaderRow);
        Assert.Equal([0, 1, 2], finding.Rows);
    }

    [Fact]
    public void Detect_KeepsAHeaderWhoseBlockAfterAnEmptyRowStartsWithNumbers()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
        [
            new TextRowShape(0, 2, "Name"), new TextRowShape(1, 2, "Ann", 1), new TextRowShape(2, 0, null),
            new TextRowShape(3, 3, "Bob", 1), new TextRowShape(4, 3, "Eve", 1),
        ], []));

        Assert.Equal(TextTableFindingKind.BlankRows, finding.Kind);
        Assert.Equal(0, finding.HeaderRow);
    }

    [Fact]
    public void Detect_ListsEmptyRowsInsideTheTableButNotAfterIt()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((3, "Region"), (3, "North"), (0, null), (3, "South"), (0, null), (3, "East")), []));

        Assert.Equal(TextTableFindingKind.BlankRows, finding.Kind);
        Assert.Equal([2, 4], finding.Rows);
        Assert.Equal(5, finding.LastRow);
    }

    [Theory]
    [InlineData("合计")]
    [InlineData("总计：")]
    [InlineData("小计")]
    [InlineData("总和")]
    [InlineData("Total")]
    [InlineData(" GRAND TOTAL: ")]
    [InlineData("Subtotal")]
    [InlineData("sum")]
    [InlineData("合计 Total")]
    [InlineData("合计Total")]
    [InlineData("总计（Grand Total）")]
    [InlineData("Grand Total")]
    public void Detect_FindsATrailingTotalRowByItsLabel(string label)
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((3, "Region"), (3, "North"), (3, "South"), (2, label)), []));

        Assert.Equal(TextTableFindingKind.TotalRows, finding.Kind);
        Assert.Equal([3], finding.Rows);
        Assert.Equal([label], finding.Labels);
    }

    [Fact]
    public void Detect_FindsATotalRowInTheTailOfALongSheet()
    {
        TextRowShape[] head = [.. Enumerable.Range(0, 5).Select(static row => new TextRowShape(row, 3, row == 0 ? "Region" : "North"))];
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            head, [new TextRowShape(20_000, 3, "South"), new TextRowShape(20_001, 2, "Grand Total")]));

        Assert.Equal(TextTableFindingKind.TotalRows, finding.Kind);
        Assert.Equal([20_001], finding.Rows);
    }

    [Fact]
    public void Detect_TakesTheEvaluationNoticeRowForNoticeNotData()
    {
        IReadOnlyList<TextTableFinding> findings = TextTableLayout.Detect(
            Shapes((3, "Region"), (3, "North"), (2, "Total"),
                (1, "Evaluation Only. Created with Aspose.Cells for .NET. Copyright 2003 - 2026 Aspose Pty Ltd.")), []);

        Assert.Equal([TextTableFindingKind.TotalRows, TextTableFindingKind.EvaluationNotice], findings.Select(static finding => finding.Kind));
        Assert.Equal([2], findings[0].Rows);
        Assert.Equal(2, findings[0].LastRow);
        Assert.Equal([3], findings[1].Rows);
        Assert.StartsWith("Row 4 is not data: it is the evaluation notice", TextTableLayout.Describe(findings, "Data", 2, capped: false)[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_GivesNoSumExampleForATotalRowWithoutNumbers()
    {
        Warning warning = Assert.Single(TextTableLayout.Describe(
            TextTableLayout.Detect(Shapes((3, "Region"), (3, "North"), (3, "South"), (1, "Total")), []), "Data", 2, capped: false));

        Assert.StartsWith("End data ranges at row 3, and leave", warning.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Totalizer")]
    [InlineData("Grand")]
    [InlineData("Total Solutions Ltd")]
    [InlineData("合计单位")]
    public void Detect_TakesNoLabelWithAWordBesidesTotalWordsForATotal(string label)
    {
        Assert.Empty(TextTableLayout.Detect(Shapes((3, "Region"), (3, "North"), (3, "South"), (3, label)), []));
    }

    /// <summary>
    /// A bilingual total label, as Chinese reports write it, is a total row in a CSV input and in
    /// each sheet a JSON export writes; a product name that only starts with "Total" is not.
    /// </summary>
    [Fact]
    public void Inspect_AndJsonExport_FindABilingualTotalRow()
    {
        File.WriteAllText(_workspace.File("tot.csv"),
            "日期,数量,金额\n2026-09-01,5,120\n2026-09-02,7,180\n合计 Total,12,300\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.Equal(0, _workspace.Run("cells", "create", "tot.xlsx", "--sheet-names", "Bilingual,Grand,Meters").ExitCode);
        CliResult filled = _workspace.Run("cells", "edit", "tot.xlsx", "--in-place", "--ops", """
            [{"op":"set_values","sheet":"Bilingual","range":"A1","values":[["Date","Units"],["a",5],["b",7],["合计 Total",12]]},
             {"op":"set_values","sheet":"Grand","range":"A1","values":[["Date","Units"],["a",5],["b",7],["Grand Total",12]]},
             {"op":"set_values","sheet":"Meters","range":"A1","values":[["Model","Units"],["Meter",5],["Gauge",7],["Totalizer",12]]}]
            """);
        Assert.True(filled.ExitCode == 0, filled.StdErr);

        JsonNode[] inspected = LayoutWarnings(_workspace.Run("cells", "inspect", "tot.csv", "--output-format", "json").Json());
        JsonNode[] exported = LayoutWarnings(_workspace.Run("cells", "convert", "tot.xlsx", "--to", "json", "--out", "tot.json", "--output-format", "json").Json());

        Assert.Contains("Row 4 is a total row ('合计 Total')", Text(Assert.Single(inspected), "message"), StringComparison.Ordinal);
        Assert.Equal(2, exported.Length);
        Assert.Contains(exported, static warning => Text(warning, "message").Contains("sheet 'Bilingual' is a total row ('合计 Total')", StringComparison.Ordinal));
        Assert.Contains(exported, static warning => Text(warning, "message").Contains("sheet 'Grand' is a total row ('Grand Total')", StringComparison.Ordinal));
    }

    [Fact]
    public void Detect_ReportsNothingForAPlainTable()
    {
        Assert.Empty(TextTableLayout.Detect(
            Shapes((4, "Region"), (4, "North"), (3, "South"), (4, "Total Solutions Ltd"), (4, "East")), []));
    }

    [Fact]
    public void Detect_TakesAHeaderNarrowerThanItsDataRowsForTheHeader()
    {
        Assert.Empty(TextTableLayout.Detect(Shapes((2, "Name"), (5, "Ann"), (5, "Bob")), []));
    }

    [Fact]
    public void Detect_StartsTheTableAtANarrowHeaderAfterATitle()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((1, "Staff"), (2, "Name"), (5, "Ann"), (5, "Bob")), []));

        Assert.Equal(TextTableFindingKind.Preamble, finding.Kind);
        Assert.Equal(1, finding.HeaderRow);
        Assert.Equal([0], finding.Rows);
    }

    [Fact]
    public void Detect_KeepsAHeaderNarrowerThanItsDataWhenAnEmptyRowFollowsIt()
    {
        TextTableFinding finding = Assert.Single(TextTableLayout.Detect(
            Shapes((2, "Name"), (0, null), (5, "Ann"), (5, "Bob")), []));

        Assert.Equal(TextTableFindingKind.BlankRows, finding.Kind);
        Assert.Equal(0, finding.HeaderRow);
    }

    [Fact]
    public void Describe_EndsTheTableToAddressWhereTheTotalRowsSayTheDataEnds()
    {
        IReadOnlyList<Warning> warnings = TextTableLayout.Describe(TextTableLayout.Detect(
            Shapes((3, "Region"), (3, "North"), (0, null), (3, "South"), (0, null), (2, "Total")), []), "Data", 2, capped: false);

        Assert.Equal(2, warnings.Count);
        Assert.Contains("(A1:C4)", warnings[0].Hint, StringComparison.Ordinal);
        Assert.StartsWith("Row 3 is empty inside the table", warnings[0].Message, StringComparison.Ordinal);
        Assert.StartsWith("End data ranges at row 4", warnings[1].Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_ReportsNoPreambleForASingleColumn()
    {
        Assert.Empty(TextTableLayout.Detect(Shapes((1, "Name"), (1, "Ann"), (1, "Bob")), []));
    }

    [Fact]
    public void Inspect_WarnsAboutThePreambleTheEmptyRowAndTheTotalRow()
    {
        File.WriteAllText(_workspace.File("erp.csv"),
            "销售明细报表\n查询条件: 2026-01-01 至 2026-03-31\n日期,地区,产品,金额\n2026-01-05,北京,甲,100\n2026-01-06,上海,乙,200\n\n2026-02-01,广州,甲,300\n合计,,,600\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonNode[] warnings = LayoutWarnings(_workspace.Run("cells", "inspect", "erp.csv", "--output-format", "json").Json());

        Assert.Equal(3, warnings.Length);
        Assert.Contains("header is probably row 3; rows 1-2", Text(warnings[0], "message"), StringComparison.Ordinal);
        Assert.Contains("'cells read <file> --sheet \"erp\" --range A3:D3'", Text(warnings[0], "hint"), StringComparison.Ordinal);
        Assert.Equal("row/erp!1:2", Text(warnings[0], "location"));
        Assert.Contains("Row 6 is empty", Text(warnings[1], "message"), StringComparison.Ordinal);
        Assert.Equal("row/erp!6", Text(warnings[1], "location"));
        Assert.Contains("Row 8 is a total row ('合计')", Text(warnings[2], "message"), StringComparison.Ordinal);
        // The example sums the column the total row holds a number in, not a text column.
        Assert.Contains("End data ranges at row 7, for example =SUM(D4:D7) for column D", Text(warnings[2], "hint"), StringComparison.Ordinal);
        Assert.Equal("row/erp!8", Text(warnings[2], "location"));

        JsonNode converted = _workspace.Run(
            "cells", "convert", "erp.csv", "--to", "xlsx", "--out", "erp.xlsx", "--output-format", "json").Json();
        Assert.Equal(3, LayoutWarnings(converted).Length);
        // The hinted command works as written on the converted workbook too.
        JsonNode header = _workspace.Run(
            "cells", "read", "erp.xlsx", "--sheet", "erp", "--range", "A3:D3", "--output-format", "json").Json();
        Assert.Equal("日期", header["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
    }

    [Fact]
    public void Convert_FindsTheHeaderAfterAnExportTimeRowAndAnEmptyRow()
    {
        File.WriteAllText(_workspace.File("preamble.csv"),
            "标题 Title,,\n导出时间: 2026-10-02,条件: 全部,\n,,\n编码,名称,数量\nA1,螺栓,\"1,200\"\nA2,垫片,300\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonNode[] warnings = LayoutWarnings(_workspace.Run(
            "cells", "convert", "preamble.csv", "--to", "xlsx", "--out", "preamble.xlsx", "--output-format", "json").Json());

        // The empty row lies before the header, so it is part of the preamble, not of the table.
        JsonNode warning = Assert.Single(warnings);
        Assert.Contains("header is probably row 4; rows 1-3", Text(warning, "message"), StringComparison.Ordinal);
        Assert.Contains("--range A4:C4' and start data ranges, formulas and sorts at row 5", Text(warning, "hint"), StringComparison.Ordinal);
        Assert.Equal("row/preamble!1:3", Text(warning, "location"));
    }

    /// <summary>
    /// A supplier statement with a title, an "export time, date" row of two cells and an empty row
    /// above its header: every warning counts from the header in row 4, and the empty row above it
    /// is not inside the table.
    /// </summary>
    [Fact]
    public void Convert_CountsTheTableFromTheHeaderBelowATitleAndAnExportTimeRow()
    {
        File.WriteAllText(_workspace.File("supplier-export.csv"), "供应商对账单,,,,,\n导出时间,2026-10-31,,,,\n,,,,,\n" + SupplierTable,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonNode[] warnings = LayoutWarnings(_workspace.Run(
            "cells", "convert", "supplier-export.csv", "--to", "xlsx", "--out", "supplier-export.xlsx", "--output-format", "json").Json());

        Assert.Equal(3, warnings.Length);
        Assert.Contains("header is probably row 4; rows 1-3", Text(warnings[0], "message"), StringComparison.Ordinal);
        Assert.Contains("--range A4:F4' and start data ranges, formulas and sorts at row 5,", Text(warnings[0], "hint"), StringComparison.Ordinal);
        Assert.Equal("row/supplier-export!1:3", Text(warnings[0], "location"));
        Assert.StartsWith("2 rows (7, 10) are empty inside the table", Text(warnings[1], "message"), StringComparison.Ordinal);
        Assert.Contains("(A4:F12)", Text(warnings[1], "hint"), StringComparison.Ordinal);
        Assert.Equal("row/supplier-export!7,10", Text(warnings[1], "location"));
        Assert.Contains("Row 14 is a total row ('合计')", Text(warnings[2], "message"), StringComparison.Ordinal);
        Assert.Contains("End data ranges at row 12, for example =SUM(C5:C12) for column C", Text(warnings[2], "hint"), StringComparison.Ordinal);
        Assert.Equal("row/supplier-export!14", Text(warnings[2], "location"));
        // The hinted range reads the header of the converted workbook.
        JsonNode header = _workspace.Run(
            "cells", "read", "supplier-export.xlsx", "--sheet", "supplier-export", "--range", "A4:F4", "--output-format", "json").Json();
        Assert.Equal("供应商编码", header["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
    }

    /// <summary>
    /// The same table with its header in row 1 has no header warning, and with one title row
    /// above it the header is row 2.
    /// </summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("供应商对账单,,,,,\n", 2)]
    public void Convert_KeepsTheHeaderOfATableWithoutAnExportRow(string preamble, int headerRow)
    {
        File.WriteAllText(_workspace.File("supplier.csv"), preamble + SupplierTable, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonNode[] warnings = LayoutWarnings(_workspace.Run(
            "cells", "convert", "supplier.csv", "--to", "xlsx", "--out", "supplier.xlsx", "--output-format", "json").Json());

        string[] headers = [.. warnings.Select(static warning => Text(warning, "message"))
            .Where(static message => message.Contains("header is probably", StringComparison.Ordinal))];
        if (headerRow == 0)
        {
            Assert.Empty(headers);
            return;
        }

        Assert.Contains($"header is probably row {headerRow}; row 1 looks", Assert.Single(headers), StringComparison.Ordinal);
        Assert.Contains(warnings, warning => Text(warning, "hint").Contains(
            $"--range A{headerRow}:F{headerRow}' and start data ranges, formulas and sorts at row {headerRow + 1},", StringComparison.Ordinal));
    }

    /// <summary>
    /// An "export time, date" row is a note wherever it stands: after a title and an empty row,
    /// or first in the file, the header is the full row after the empty row below it.
    /// </summary>
    [Theory]
    [InlineData("供应商对账单,,,,,\n,,,,,\n导出时间,2026-10-31,,,,\n,,,,,\n", 5)]
    [InlineData("导出时间,2026-10-31,,,,\n,,,,,\n", 3)]
    public void Convert_FindsTheHeaderAfterAnExportTimeRowWithNoTitleRightAboveIt(string preamble, int headerRow)
    {
        File.WriteAllText(_workspace.File("supplier.csv"), preamble + SupplierTable, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonNode[] warnings = LayoutWarnings(_workspace.Run(
            "cells", "convert", "supplier.csv", "--to", "xlsx", "--out", "supplier.xlsx", "--output-format", "json").Json());

        Assert.Equal(3, warnings.Length);
        Assert.Contains($"header is probably row {headerRow}; rows 1-{headerRow - 1}", Text(warnings[0], "message"), StringComparison.Ordinal);
        Assert.Contains($"--range A{headerRow}:F{headerRow}' and start data ranges, formulas and sorts at row {headerRow + 1},",
            Text(warnings[0], "hint"), StringComparison.Ordinal);
        Assert.Contains($"(A{headerRow}:F{headerRow + 8})", Text(warnings[1], "hint"), StringComparison.Ordinal);
        Assert.Contains($"=SUM(C{headerRow + 1}:C{headerRow + 8})", Text(warnings[2], "hint"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A header of labels only that is narrower than its data rows stays the header below a title
    /// when an empty row follows it, even though the data rows are wider and hold only text.
    /// </summary>
    [Fact]
    public void Inspect_KeepsANarrowHeaderOfLabelsBelowATitleWhenAnEmptyRowFollowsIt()
    {
        File.WriteAllText(_workspace.File("staff.csv"),
            "Staff,,,,\nName,Dept,,,\n,,,,\nAnn,Sales,Rome,Remote,Full\nBob,Ops,Oslo,Office,Part\n");

        JsonNode[] warnings = LayoutWarnings(_workspace.Run("cells", "inspect", "staff.csv", "--output-format", "json").Json());

        JsonNode header = Assert.Single(warnings, static warning => Text(warning, "message").Contains("header is probably", StringComparison.Ordinal));
        Assert.Contains("header is probably row 2; row 1 looks", Text(header, "message"), StringComparison.Ordinal);
        Assert.Contains("--range A2:E2'", Text(header, "hint"), StringComparison.Ordinal);
    }

    private const string NotesTable = "Name,Region,Amount\nAlice,East,700\nBob,West,580\n";

    /// <summary>
    /// Notes an export writes above its table, of text and dates only, however they are laid out:
    /// a text row and a date row in one block, one row of two text values, or two blocks as wide
    /// as each other. The header is the wider row of labels with data below it.
    /// </summary>
    [Theory]
    [InlineData("Report,Sales export\nExported at,2026-10-31\n\n", 4)]
    [InlineData("Exported by,Zhang San\n\n", 3)]
    [InlineData("Exported at,2026-10-31\n\nQuery,East only\n\n", 5)]
    [InlineData("Sales report\n\nExported at,2026-10-31\n\nQuery,East only\n\n", 7)]
    [InlineData("Query,East only\n\nExported at,2026-10-31\n\n", 5)]
    [InlineData("Exported by,Zhang San\n\nQuery,East only\n\n", 5)]
    public void Inspect_FindsTheHeaderBelowNotesOfTextAndDates(string notes, int headerRow)
    {
        File.WriteAllText(_workspace.File("notes.csv"), notes + NotesTable);

        JsonNode warning = Assert.Single(LayoutWarnings(_workspace.Run("cells", "inspect", "notes.csv", "--output-format", "json").Json()));

        Assert.Contains($"header is probably row {headerRow}; rows 1-{headerRow - 1}", Text(warning, "message"), StringComparison.Ordinal);
        Assert.Contains($"--range A{headerRow}:C{headerRow}' and start data ranges, formulas and sorts at row {headerRow + 1},", Text(warning, "hint"), StringComparison.Ordinal);
        Assert.Equal($"row/notes!1:{headerRow - 1}", Text(warning, "location"));
    }

    /// <summary>A header whose column labels are dates is a header, where it starts the file, below a title, or above an empty row.</summary>
    [Theory]
    [InlineData("Region,2026-01-01,2026-02-01\nEast,100,200\nWest,150,250\n", null, null)]
    [InlineData("Sales report\nRegion,2026-01-01,2026-02-01\nEast,100,200\nWest,150,250\n", 2, null)]
    [InlineData("Region,2026-01-01,2026-02-01\nEast,100,200\n\nWest,150,250\nNorth,160,260\n", null, "Row 3 is")]
    public void Inspect_KeepsAHeaderThatHoldsDates(string csv, int? headerRow, string? empty)
    {
        AssertLayout(csv, headerRow, empty);
    }

    /// <summary>
    /// A header narrower than its data keeps its place above an empty row, whether its data is
    /// text or holds numbers, as does one that is just above a wider all-text block.
    /// </summary>
    [Theory]
    [InlineData("Name,Dept\n\nAnn,Sales,Rome,Remote,Full\nBob,Ops,Oslo,Office,Part\n")]
    [InlineData("Name,Amount\n\nAnn,5,a,b,c\nBob,7,d,e,f\n")]
    public void Inspect_KeepsAHeaderNarrowerThanItsDataAboveAnEmptyRow(string csv)
    {
        AssertLayout(csv, headerRow: null, "Row 2 is");
    }

    /// <summary>
    /// Text above an empty row is not taken for notes when it is a table of its own: rows of
    /// values with numbers, several text rows without a date, several rows with a date each, a
    /// table split by empty rows, or a wider block below it with no data under its labels.
    /// </summary>
    [Theory]
    [InlineData("Region,Sales\nEast,100\n\nName,Region,Amount\nAlice,East,700\n", "Row 3 is")]
    [InlineData("Name,Dept\nAnn,Sales\n\nItem,Region,Qty\nBolt,East,5\n", "Row 3 is")]
    [InlineData("Name,Score\nAnn,5\n\nBob,7\n\nTitle,X,Y\nFoo,1,2\n", "2 rows (3, 5) are")]
    [InlineData("Task,Due\nDesign,2026-01-05\nBuild,2026-02-01\n\nItem,Qty,Cost\nBolt,5,1.2\nNut,7,0.4\n", "Row 4 is")]
    [InlineData("Date,Region,Note\n2026-01-05,East\n2026-01-06,West\n\nAlpha,Beta,Gamma,Delta,Epsilon\n1,2,3,4,5\n", "Row 4 is")]
    [InlineData("Date,Region,Note\n2026-01-05,East\n\nAlpha,Beta,Gamma,Delta,Epsilon\n", "Row 3 is")]
    public void Inspect_KeepsTheFirstTableAboveAnEmptyRowAndAWiderBlock(string csv, string empty)
    {
        AssertLayout(csv, headerRow: null, empty);
    }

    /// <summary>
    /// An "export time, date" row above rows without a header stays what it was: that row and a
    /// header labeled with a month have the same shape.
    /// </summary>
    [Fact]
    public void Inspect_StillTakesRowsWithoutAHeaderBelowADateNoteForTheTable()
    {
        AssertLayout("Exported at,2026-10-31\n\nAlice,East,2026-01-05\nBob,West,2026-01-06\nCarol,North,2026-01-07\n", headerRow: 3, empty: null);
    }

    private void AssertLayout(string csv, int? headerRow, string? empty)
    {
        File.WriteAllText(_workspace.File("layout.csv"), csv);

        string[] messages = [.. LayoutWarnings(_workspace.Run("cells", "inspect", "layout.csv", "--output-format", "json").Json())
            .Select(static warning => Text(warning, "message"))];

        string? header = messages.SingleOrDefault(static message => message.Contains("header is probably", StringComparison.Ordinal));
        if (headerRow is null)
        {
            Assert.Null(header);
        }
        else
        {
            Assert.Contains($"header is probably row {headerRow}", header, StringComparison.Ordinal);
        }

        string? emptyRows = messages.SingleOrDefault(static message => message.Contains("empty inside the table", StringComparison.Ordinal));
        if (empty is null)
        {
            Assert.Null(emptyRows);
        }
        else
        {
            Assert.StartsWith($"{empty} empty inside the table", emptyRows, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The rows a JSON export would write under the wrong keys are inferred, so the hint names a
    /// command that shows them before it mentions delete_rows, and gives no operation fields
    /// with their addresses. The command runs as written, on a path with a space in it.
    /// </summary>
    [Theory]
    [InlineData("report.xlsx", "Titled", "Monthly sales by region", "row 2", "A1:C2")]
    [InlineData("my report.xlsx", "Totals", "Total", "Row 4", "A4:C4")]
    public void JsonExport_ShowsTheInferredRowsBeforeNamingDeleteRows(string file, string sheet, string shown, string subject, string range)
    {
        CreateReport(file);
        string[] args = ["cells", "convert", file, "--to", "json", "--out", "report.json", "--output-format", "json"];

        CliResult converted = _workspace.Run(args);

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Empty(ExecutableHints.Problems(new HintRun(args, _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null), converted, variants: true));
        JsonNode warning = Assert.Single(LayoutWarnings(converted.Json()), found => Text(found, "message").Contains($"'{sheet}'", StringComparison.Ordinal));
        Assert.Contains(subject, Text(warning, "message"), StringComparison.Ordinal);
        string hint = Text(warning, "hint");
        string command = ReadCommand(hint);
        Assert.Contains($"--range {range}", command, StringComparison.Ordinal);
        Assert.True(hint.IndexOf(command, StringComparison.Ordinal) < hint.IndexOf("delete_rows", StringComparison.Ordinal), hint);
        Assert.DoesNotContain("\"at\"", hint, StringComparison.Ordinal);
        CliResult read = _workspace.RunCommandLine(command);
        Assert.True(read.ExitCode == 0, $"{command}{Environment.NewLine}{read.StdErr}");
        Assert.Contains(shown, read.StdOut, StringComparison.Ordinal);
    }

    /// <summary>The command that shows the inferred rows opens an encrypted input as the export did, by the variable that holds its password.</summary>
    [Fact]
    public void JsonExport_ShowsTheInferredRowsOfAnEncryptedInputByItsPasswordVariable()
    {
        var password = new Dictionary<string, string?> { ["REPORT_PASSWORD"] = "Layout-7c2e" };
        CreateReport("plain.xlsx");
        CliResult encrypted = _workspace.RunWithEnv(password,
            "cells", "convert", "plain.xlsx", "--to", "xlsx", "--out", "locked.xlsx", "--encrypt-env", "REPORT_PASSWORD", "--output-format", "json");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);

        CliResult converted = _workspace.RunWithEnv(password,
            "cells", "convert", "locked.xlsx", "--password-env", "REPORT_PASSWORD", "--to", "json", "--out", "report.json", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode warning = Assert.Single(LayoutWarnings(converted.Json()), static found => Text(found, "message").Contains("'Titled'", StringComparison.Ordinal));
        string command = ReadCommand(Text(warning, "hint"));
        Assert.Contains("--password-env REPORT_PASSWORD", command, StringComparison.Ordinal);
        CliResult read = _workspace.RunCommandLine(command, password);
        Assert.True(read.ExitCode == 0, $"{command}{Environment.NewLine}{read.StdErr}");
        Assert.Contains("Monthly sales by region", read.StdOut, StringComparison.Ordinal);
    }

    /// <summary>
    /// A text input imported with --encoding or --culture opens only with that option, which the
    /// commands that read and edit do not have, so the hint first converts it to a workbook with
    /// the same option. That command runs as written, and the rows can be read in what it wrote.
    /// </summary>
    [Theory]
    [InlineData("--encoding", "gb18030", "销售报表")]
    [InlineData("--culture", "de-DE", "Bericht")]
    public void JsonExport_OfATextInputWithImportOptions_ConvertsItFirstWithTheSameOption(string option, string value, string title)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllBytes(_workspace.File("data.csv"), option == "--encoding"
            ? Encoding.GetEncoding("gb18030").GetBytes($"{title},,\n编码,名称,数量\nA1,螺栓,1200\nA2,垫片,300\n")
            : Encoding.UTF8.GetBytes($"{title},,\nName,Anzahl,Preis\nA,1,\"1.234,56\"\nB,2,\"2.345,67\"\n"));
        string[] args = ["cells", "convert", "data.csv", option, value, "--to", "json", "--out", "data.json", "--output-format", "json"];

        CliResult converted = _workspace.Run(args);

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Empty(ExecutableHints.Problems(new HintRun(args, _workspace.Path, Path.GetDirectoryName(_workspace.ConfigDirectory)!, Variables: null), converted, variants: true));
        string hint = Text(Assert.Single(LayoutWarnings(converted.Json())), "hint");
        string command = HintCommand(hint, "convert");
        Assert.Contains($"--to xlsx {option} {value}", command, StringComparison.Ordinal);
        Assert.True(hint.IndexOf(command, StringComparison.Ordinal) < hint.IndexOf("delete_rows", StringComparison.Ordinal), hint);
        Assert.DoesNotContain("cells read", hint, StringComparison.Ordinal);
        CliResult imported = _workspace.RunCommandLine(command);
        Assert.True(imported.ExitCode == 0, $"{command}{Environment.NewLine}{imported.StdErr}");
        CliResult read = _workspace.Run("cells", "read", "data.xlsx", "--sheet", "data", "--range", "A1:C2", "--output-format", "json");
        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.Contains(title, read.StdOut, StringComparison.Ordinal);
    }

    private void CreateReport(string file)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", file, "--sheet-names", "Titled,Totals").ExitCode);
        CliResult filled = _workspace.Run("cells", "edit", file, "--in-place", "--ops", """
            [{"op":"set_values","sheet":"Titled","range":"A1","values":[["Monthly sales by region",null,null],["Region","Units","Amount"],["North",5,120],["South",7,180]]},
             {"op":"set_values","sheet":"Totals","range":"A1","values":[["Date","Units","Amount"],["2026-09-01",5,120],["2026-09-02",7,180],["Total",12,300]]}]
            """);
        Assert.True(filled.ExitCode == 0, filled.StdErr);
    }

    private static string ReadCommand(string hint) => HintCommand(hint, "read");

    // The cells command a hint quotes in single quotes.
    private static string HintCommand(string hint, string verb)
    {
        System.Text.RegularExpressions.Match command = System.Text.RegularExpressions.Regex.Match(hint, $@"'(aspose-cli cells {verb} [^']*)'");
        Assert.True(command.Success, hint);
        return command.Groups[1].Value;
    }

    [Fact]
    public void Inspect_ReportsNoLayoutWarningForAPlainCsv()
    {
        File.WriteAllText(_workspace.File("clean.csv"), "Region,Sales\nIT,5\nES,7\n");

        Assert.Empty(LayoutWarnings(_workspace.Run("cells", "inspect", "clean.csv", "--output-format", "json").Json()));
    }

    [Fact]
    public void Inspect_ReportsNoPreambleForAHeaderNarrowerThanItsDataRows()
    {
        File.WriteAllText(_workspace.File("unnamed.csv"), "Name,Amount\nAnn,5,a,b,c\nBob,7,d,e,f\n");

        Assert.Empty(LayoutWarnings(_workspace.Run("cells", "inspect", "unnamed.csv", "--output-format", "json").Json()));
    }

    // A supplier table of six columns with empty rows inside it and a total row after an empty row.
    private const string SupplierTable =
        "供应商编码,供应商名称,采购金额,付款金额,信用额度,余额\n"
        + "S001,深圳市鑫联电子有限公司,\"1,250,300.50\",\"1,100,000.00\",\"2,000,000\",150300.50\n"
        + "S002,上海恒泰精密机械,\"845,120.00\",\"845,120.00\",\"1,200,000\",0\n"
        + ",,,,,\n"
        + "S003,苏州瑞达包装材料,\"312,980.75\",\"250,000.00\",\"500,000\",\"62,980.75\"\n"
        + "S004,广州海川贸易有限公司,\"2,108,450.00\",\"1,900,000.00\",\"2,500,000\",\"208,450.00\"\n"
        + ",,,,,\n"
        + "S005,杭州云岚纺织,\"47,600.00\",\"30,000.00\",\"300,000\",\"17,600.00\"\n"
        + "S006,成都天府塑胶制品,\"589,200.25\",\"400,000.00\",\"800,000\",\"189,200.25\"\n"
        + ",,,,,\n"
        + "合计,,\"5,153,651.50\",\"4,525,120.00\",,\"628,531.50\"\n";

    public void Dispose() => _workspace.Dispose();

    private static TextRowShape[] Shapes(params (int Filled, string? FirstText)[] rows) =>
        [.. rows.Select(static (row, index) => new TextRowShape(index, row.Filled, row.FirstText))];

    private static JsonNode[] LayoutWarnings(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? [])
            .Where(static warning => Text(warning!, "code") == "TEXT_TABLE_LAYOUT")
            .Select(static warning => warning!)];

    private static string Text(JsonNode node, string name) => node[name]!.GetValue<string>();
}
