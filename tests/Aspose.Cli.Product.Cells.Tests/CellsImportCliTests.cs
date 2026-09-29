using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// import_range and import_sheet through the built CLI. Every child process runs in evaluation
/// mode, so these cover the import operations where the licensed engine suite skips.
/// </summary>
public sealed class CellsImportCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ImportRange_BringsValuesOrEverythingFromAnotherWorkbook()
    {
        CreateSource();
        CreateReport();
        byte[] source = File.ReadAllBytes(_workspace.File("eu.xlsx"));

        JsonNode edited = _workspace.Run(
            "cells", "edit", "report.xlsx", "--in-place", "--output", "json", "--ops",
            """
            {"ops":[
              {"op":"import_range","sheet":"Report","path":"eu.xlsx","from":"Totals!A1:B5","to":"B2"},
              {"op":"import_range","sheet":"Report","path":"eu.xlsx","from":"Totals!A1:B5","to":"E2","content":"all"},
              {"op":"import_range","sheet":"Report","path":"eu.xlsx","from":"A1","to":"H1"}
            ]}
            """).Json();

        Assert.Equal(
            ["Report!B2:C6", "Report!E2:F6", "Report!H1"],
            edited["applied"]!.AsArray().Select(static item => item!["targets"]![0]!.GetValue<string>()));
        Assert.Equal(source, File.ReadAllBytes(_workspace.File("eu.xlsx")));
        JsonNode read = _workspace.Run(
            "cells", "query", "range", "report.xlsx", "--sheet", "Report", "--range", "A1:H6",
            "--scope", "full", "--output", "json").Json();
        JsonArray rows = read["sheet"]!["cells"]!.AsArray();
        JsonNode Cell(string address)
        {
            CellRef cell = A1.ParseCell(address);
            return rows[cell.Row]![cell.Column]!;
        }

        JsonNode StyleOf(string address) => read["styles"]![Cell(address)["styleId"]!.GetValue<string>()]!;

        // values: the SUM arrives as its result with its number format, without the fill.
        Assert.Equal(2000.5, Cell("C5")["v"]!.GetValue<double>());
        Assert.Null(Cell("C5")["f"]);
        Assert.Equal("#,##0.00", StyleOf("C3")["numberFormat"]!.GetValue<string>());
        Assert.Null(StyleOf("C3")["bg"]);
        // all: formulas as written and the fill; the Rates reference points at this workbook,
        // which has no Rates sheet.
        Assert.Equal("=SUM(F3:F4)", Cell("F5")["f"]!.GetValue<string>());
        Assert.Equal(2000.5, Cell("F5")["v"]!.GetValue<double>());
        Assert.Equal("#FFF2CC", StyleOf("F3")["bg"]!.GetValue<string>());
        Assert.Contains("#REF!", Cell("F6")["f"]!.GetValue<string>(), StringComparison.Ordinal);
        // An unqualified source range lies on the source's first sheet.
        Assert.Equal("first-sheet", Cell("H1")["v"]!.GetValue<string>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ImportSheet_AddsANamedSheetFromAWorkbookOrCsvAndRefusesATakenName()
    {
        CreateSource();
        CreateReport();
        File.WriteAllText(_workspace.File("it.csv"), "Region,Sales\nIT,5\nES,7\n");

        _workspace.Run(
            "cells", "edit", "report.xlsx", "--in-place", "--output", "json", "--ops",
            """
            {"ops":[
              {"op":"import_sheet","sheet":"Rates","path":"eu.xlsx"},
              {"op":"import_sheet","sheet":"Totals","path":"eu.xlsx","name":"EU","position":0},
              {"op":"import_sheet","path":"it.csv","name":"IT"}
            ]}
            """).Succeeded();

        JsonArray sheets = _workspace.Run("cells", "inspect", "report.xlsx", "--output", "json")
            .Json()["workbook"]!["sheets"]!.AsArray();
        string[] names = [.. sheets.Select(static sheet => sheet!["name"]!.GetValue<string>())];
        Assert.Equal("EU", names[0]);
        Assert.Contains("Rates", names);
        Assert.Contains("IT", names);
        JsonArray eu = _workspace.Run(
            "cells", "query", "range", "report.xlsx", "--sheet", "EU", "--range", "B4:B5",
            "--scope", "formulas", "--output", "json").Json()["sheet"]!["cells"]!.AsArray();
        Assert.Equal("=SUM(B2:B3)", eu[0]![0]!["f"]!.GetValue<string>());
        // The imported Rates sheet is the one the formula now reads: 2000.5 * 2.
        Assert.Equal("=B4*Rates!A1", eu[1]![0]!["f"]!.GetValue<string>());
        Assert.Equal(4001, eu[1]![0]!["v"]!.GetValue<double>());
        JsonArray it = _workspace.Run(
            "cells", "query", "range", "report.xlsx", "--sheet", "IT", "--range", "A3:B3", "--output", "json")
            .Json()["sheet"]!["cells"]!.AsArray();
        Assert.Equal("ES", it[0]![0]!["v"]!.GetValue<string>());
        Assert.Equal(7, it[0]![1]!["v"]!.GetValue<double>());

        CliResult taken = _workspace.Run(
            "cells", "edit", "report.xlsx", "--in-place", "--output", "json", "--ops",
            """{"ops":[{"op":"import_sheet","sheet":"Totals","path":"eu.xlsx","name":"eu"}]}""");
        JsonNode error = JsonNode.Parse(taken.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("'EU'", error["details"]!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"import_sheet","sheet":"Ghost","path":"eu.xlsx"}]}""")]
    [InlineData("""{"ops":[{"op":"import_range","sheet":"Report","path":"eu.xlsx","from":"Ghost!A1:B2","to":"A1"}]}""")]
    public void AMissingSourceSheet_ListsTheSourcesSheets(string operations)
    {
        CreateSource();
        CreateReport();

        CliResult missing = _workspace.Run(
            "cells", "edit", "report.xlsx", "--out", "report.out.xlsx", "--output", "json", "--ops", operations);

        JsonNode error = JsonNode.Parse(missing.StdErr)!["error"]!;
        Assert.Equal("SHEET_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Equal("Ghost", error["details"]!["requested"]!.GetValue<string>());
        string[] available = [.. error["details"]!["available"]!.AsArray().Select(static name => name!.GetValue<string>())];
        Assert.Equal(["Notes", "Totals", "Rates"], available.Take(3));
        Assert.False(File.Exists(_workspace.File("report.out.xlsx")));
    }

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// eu.xlsx: a Notes first sheet, a Totals sheet (formatted numbers, a SUM and a formula that
    /// reads the Rates sheet) and a Rates sheet.
    /// </summary>
    private void CreateSource()
    {
        _workspace.Run("cells", "create", "eu.xlsx", "--sheets", "Notes,Totals,Rates").Succeeded();
        _workspace.Run(
            "cells", "edit", "eu.xlsx", "--in-place", "--output", "json", "--ops",
            """
            {"ops":[
              {"op":"set_values","sheet":"Notes","range":"A1","values":[["first-sheet"]]},
              {"op":"set_values","sheet":"Totals","range":"A1","values":[["Region","Sales"],["DE",1200.5],["FR",800]]},
              {"op":"set_formula","sheet":"Totals","range":"B4","formula":"=SUM(B2:B3)"},
              {"op":"set_formula","sheet":"Totals","range":"B5","formula":"=B4*Rates!A1"},
              {"op":"set_values","sheet":"Rates","range":"A1","values":[[2]]},
              {"op":"format_range","sheet":"Totals","range":"B2:B4","style":{"numberFormat":"#,##0.00","bg":"#FFF2CC"}}
            ]}
            """).Succeeded();
    }

    private void CreateReport() =>
        _workspace.Run("cells", "create", "report.xlsx", "--sheets", "Report").Succeeded();
}
