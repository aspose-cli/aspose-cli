using System.Drawing;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Pivot;
using Aspose.Cli.TestKit.Tables;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The Cells table's round trip, one case per row: a settable row is set through an operation
/// document to a value other than the one it holds, the edit saves and reopens the file, and the
/// value its report reads back equals the one written; a read-only row is read. The workbook is
/// made by the engine with one object of every kind; a row the engine does not keep is listed
/// with its known issue, and the case checks that the engine still does not keep it.
/// </summary>
public sealed class CellsTableRoundTripTests
{
    public static TheoryData<string, string> Settable => TableRoundTrip.Cases(CellsTable.Definition, settable: true);

    public static TheoryData<string, string> ReadOnly => TableRoundTrip.Cases(CellsTable.Definition, settable: false);

    /// <summary>
    /// The rows whose sample is the workbook's or the member's, not the value kind's: a sheet's
    /// name, a formula, an area, a number format, a value in the range the engine takes.
    /// </summary>
    private static readonly Dictionary<(string Kind, string Row), JsonNode> Samples = new()
    {
        [("workbook", "activeSheetName")] = "Other",
        [("workbook", "defaultStyle.font.doubleSize")] = 12,
        [("sheet", "autoFilter.range")] = "A1:C5",
        [("sheet", "zoom")] = 150,
        [("cell", "value")] = 42.5,
        [("cell", "formula")] = "=1+1",
        [("cell", "formulaR1C1")] = "=R1C1+2",
        [("style", "custom")] = "0.000",
        [("style", "number")] = 3,
        [("style", "font.doubleSize")] = 12.5,
        [("style", "verticalAlignment")] = "Top",
        [("style", "indentLevel")] = 3,
        [("sheet", "tabColor")] = "#12AB34",
        [("sparklineGroup", "seriesColor")] = "#12AB34",
        [("rule", "top10.rank")] = 5,
        [("pageSetup", "paperSize")] = "PaperA3",
        [("range", "value")] = new JsonArray(new JsonArray(1, "a"), new JsonArray(true, null)),
        [("name", "refersTo")] = "=Data!$A$1",
        [("pageSetup", "zoom")] = 150,
        [("pageSetup", "fitToPagesWide")] = 2,
        [("pageSetup", "fitToPagesTall")] = 3,
        [("pageSetup", "printArea")] = "A1:C5",
        [("pageSetup", "printTitleRows")] = "$1:$1",
        [("pageSetup", "printTitleColumns")] = "$A:$A",
        [("row", "height")] = 30,
        [("column", "width")] = 20,
        [("hyperlink", "address")] = "https://example.com/trip",
        [("validation", "formula1")] = "=1",
        [("validation", "formula2")] = "=10",
        [("rule", "formula1")] = "=5",
        [("rule", "formula2")] = "=10",
        [("rule", "style.custom")] = "0.000",
        [("table", "tableStyleName")] = "TableStyleMedium2",
        [("chart", "nSeries.categoryData")] = "=Data!$A$2:$A$5",
        [("chart", "gapWidth")] = 150,
        [("chart", "valueAxis.minValue")] = 5,
        [("series", "values")] = "=Data!$C$2:$C$5",
        [("series", "xValues")] = "=Data!$C$2:$C$5",
        [("series", "dataLabels.numberFormat")] = "0.0",
        [("dataField", "numberFormat")] = "0.0",
        [("picture", "widthPt")] = 30,
        [("picture", "heightPt")] = 15,
    };

    /// <summary>The rows the same operation sets before the row under test, so it is written where the engine keeps it.</summary>
    private static readonly Dictionary<(string Kind, string Row), JsonObject> Setup = new()
    {
        [("style", "foregroundColor")] = new() { ["pattern"] = "Solid" },
        [("style", "backgroundColor")] = new() { ["pattern"] = "Gray50" },
        [("style", "borders.top.color")] = new() { ["borders.top.lineStyle"] = "Thin" },
        [("style", "borders.bottom.color")] = new() { ["borders.bottom.lineStyle"] = "Thin" },
        [("style", "borders.left.color")] = new() { ["borders.left.lineStyle"] = "Thin" },
        [("style", "borders.right.color")] = new() { ["borders.right.lineStyle"] = "Thin" },
        [("chart", "valueAxis.minValue")] = new() { ["valueAxis.isAutomaticMinValue"] = false },
        [("series", "area.foregroundColor")] = new() { ["area.formatting"] = "Custom" },
        [("series", "marker.area.foregroundColor")] = new() { ["marker.area.formatting"] = "Custom" },
        [("point", "area.foregroundColor")] = new() { ["area.formatting"] = "Custom" },
        [("rule", "style.foregroundColor")] = new() { ["style.pattern"] = "Solid" },
        [("rule", "style.backgroundColor")] = new() { ["style.pattern"] = "Solid" },
        [("rule", "formula2")] = new() { ["operator"] = "Between" },
    };

    /// <summary>The rule a rule's row is written on: the rule of the type whose member the row names.</summary>
    private static readonly Dictionary<string, int> Rules = new(StringComparer.Ordinal)
    {
        ["colorScale."] = 1,
        ["dataBar."] = 2,
        ["top10."] = 3,
        ["iconSet."] = 4,
    };

    /// <summary>The rows the engine does not keep as written, each with the known issue that says so.</summary>
    private static readonly Dictionary<(string Kind, string Row), string> EngineDifferences = new()
    {
        [("chart", "valueAxis.isAutomaticMinValue")] = "CELLS-AXIS-AUTOMATIC-MIN-SAVE",
        [("sparklineGroup", "seriesColor")] = "CELLS-COLOR-TINT-KEPT",
    };

    [Theory]
    [MemberData(nameof(Settable))]
    public void SettableRow_ReadsBackFromTheSavedFile(string kind, string row)
    {
        CellsFixture.RequireLicense();
        using var fixture = new CellsFixture();
        string input = Book(fixture);
        string at;
        JsonNode? current;
        JsonNode write;
        using (var workbook = new Workbook(input))
        {
            at = Address(workbook, kind, row);
            current = TableRoundTrip.Read(CellsTable.Definition, workbook, at)[row]?.DeepClone();
            write = Samples.TryGetValue((kind, row), out JsonNode? special)
                ? special.DeepClone()
                : TableRoundTrip.Sample(TableRoundTrip.ValueOf(CellsTable.Definition, kind, row), current);
        }

        JsonObject props = Setup.TryGetValue((kind, row), out JsonObject? setup) ? setup.DeepClone().AsObject() : [];
        props[row] = write.DeepClone();
        string ops = new JsonObject { ["ops"] = new JsonArray(new JsonObject { ["set"] = at, ["props"] = props }) }.ToJsonString();
        JsonNode? now = null;
        Exception? failed = null;
        try
        {
            CellsEditResult result = CellsEdit.Run(fixture.Session, CellsFixture.Edit(input, fixture.File("out.xlsx"), ops));
            now = Assert.Single(result.Ops).Now![row];
        }
        catch (Exception exception) when (EngineDifferences.ContainsKey((kind, row)))
        {
            failed = exception;
        }

        bool kept = failed is null && JsonNode.DeepEquals(now, write);
        if (EngineDifferences.TryGetValue((kind, row), out string? issue))
        {
            Assert.False(kept, $"{kind}.{row} now reads back as written; {issue} may be fixed.");
            return;
        }

        Assert.True(kept, $"{kind}.{row} at {at}: wrote {write.ToJsonString()}, read back {now?.ToJsonString() ?? "null"} (was {current?.ToJsonString() ?? "null"}).");
    }

    [Theory]
    [MemberData(nameof(ReadOnly))]
    public void ReadOnlyRow_IsRead(string kind, string row)
    {
        CellsFixture.RequireLicense();
        using var fixture = new CellsFixture();
        using var workbook = new Workbook(Book(fixture));

        Assert.True(TableRoundTrip.Read(CellsTable.Definition, workbook, Address(workbook, kind, row)).ContainsKey(row));
    }

    /// <summary>
    /// A workbook with one object of every kind: two sheets, a defined name, values, a formula, a
    /// merged area, a comment, a hyperlink, a list validation, a conditional formatting with a rule
    /// of each type a row names, a table, column, line and pie charts, a pivot table with a value
    /// field, a picture and a sparkline group.
    /// </summary>
    internal static string Book(CellsFixture fixture)
    {
        string path = fixture.File("round-trip.xlsx");
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        workbook.Worksheets.Add("Other");
        Aspose.Cells.Cells cells = sheet.Cells;
        string[] header = ["Region", "Q1", "Q2"];
        for (int column = 0; column < header.Length; column++)
        {
            cells[0, column].PutValue(header[column]);
        }

        object[][] rows = [["East", 10.0, 20.0], ["West", 30.0, 40.0], ["North", 50.0, 60.0], ["South", 70.0, 80.0]];
        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                cells[row + 1, column].PutValue(rows[row][column]);
            }
        }

        cells["D2"].Formula = "=B2+C2";
        cells.SetColumnWidth(1, 12);
        cells.Merge(9, 0, 1, 2);
        int name = workbook.Worksheets.Names.Add("Rate");
        workbook.Worksheets.Names[name].RefersTo = "=Data!$B$2";
        sheet.Comments[sheet.Comments.Add("B2")].Note = "Note";
        sheet.Hyperlinks.Add("C2", 1, 1, "https://example.com");
        Validation validation = sheet.Validations[sheet.Validations.Add(CellArea.CreateCellArea("A2", "A5"))];
        validation.Type = ValidationType.WholeNumber;
        validation.Operator = OperatorType.Between;
        validation.Formula1 = "=0";
        validation.Formula2 = "=100";
        Validation list = sheet.Validations[sheet.Validations.Add(CellArea.CreateCellArea("B7", "B8"))];
        list.Type = ValidationType.List;
        list.Formula1 = "East,West";
        FormatConditionCollection formatting = sheet.ConditionalFormattings[sheet.ConditionalFormattings.Add()];
        formatting.AddArea(CellArea.CreateCellArea("B2", "C5"));
        formatting.AddCondition(FormatConditionType.CellValue, OperatorType.GreaterThan, "=20", null!);
        formatting.AddCondition(FormatConditionType.ColorScale);
        formatting.AddCondition(FormatConditionType.DataBar);
        formatting.AddCondition(FormatConditionType.Top10);
        formatting.AddCondition(FormatConditionType.IconSet);
        sheet.ListObjects.Add(20, 0, 23, 2, true);
        for (int row = 21; row <= 23; row++)
        {
            cells[row, 0].PutValue($"Item {row}");
            cells[row, 1].PutValue(row);
            cells[row, 2].PutValue(row * 2);
        }

        Chart bars = sheet.Charts[sheet.Charts.Add(ChartType.Column, 30, 0, 45, 6)];
        bars.NSeries.Add("=Data!$B$2:$B$5", true);
        bars.NSeries.CategoryData = "=Data!$A$2:$A$5";
        Chart line = sheet.Charts[sheet.Charts.Add(ChartType.LineWithDataMarkers, 50, 0, 65, 6)];
        line.NSeries.Add("=Data!$C$2:$C$5", true);
        Chart pie = sheet.Charts[sheet.Charts.Add(ChartType.Pie, 70, 0, 85, 6)];
        pie.NSeries.Add("=Data!$B$2:$B$5", true);
        PivotTable pivot = sheet.PivotTables[sheet.PivotTables.Add("=Data!A1:C5", "H2", "Pivot")];
        pivot.AddFieldToArea(PivotFieldType.Row, "Region");
        pivot.AddFieldToArea(PivotFieldType.Data, "Q1");
        pivot.CalculateData();
        using (var image = new MemoryStream(Png()))
        {
            sheet.Pictures.Add(90, 0, image);
        }

        sheet.SparklineGroups.Add(SparklineType.Line, "Data!B2:C5", false, CellArea.CreateCellArea("F2", "F5"));
        workbook.CalculateFormula();
        workbook.Save(path, SaveFormat.Xlsx);
        return path;
    }

    /// <summary>The address of an object of the kind that has the row.</summary>
    private static string Address(Workbook workbook, string kind, string row)
    {
        string Chart(ChartType type) => $"sheet/Data/chart/{Enumerable.Range(0, workbook.Worksheets[0].Charts.Count).Select(index => workbook.Worksheets[0].Charts[index]).First(chart => chart.Type == type).ChartObject.Id}";
        return (kind, row) switch
        {
            ("range", _) => "sheet/Data/range/H30:I31",
            ("validation", "inCellDropDown") => "sheet/Data/validation/1",
            ("cell", "formula" or "formulaR1C1") => "sheet/Data/cell/E2",
            ("series", _) when row.StartsWith("marker.", StringComparison.Ordinal) => $"{Chart(ChartType.LineWithDataMarkers)}/series/0",
            ("point", _) => $"{Chart(ChartType.Pie)}/series/0/point/0",
            ("rule", _) => $"sheet/Data/conditionalFormatting/0/rule/{Rules.FirstOrDefault(rule => row.StartsWith(rule.Key, StringComparison.Ordinal)).Value}",
            _ => TableRoundTrip.AddressOf(CellsTable.Definition, workbook, kind, row)
                ?? throw new InvalidOperationException($"The round-trip workbook has no {kind} with the row {row}."),
        };
    }

    /// <summary>A small red PNG, drawn by the engine.</summary>
    internal static byte[] Png()
    {
        using var workbook = new Workbook();
        Cell cell = workbook.Worksheets[0].Cells["A1"];
        Style style = cell.GetStyle();
        style.ForegroundColor = Color.Red;
        style.Pattern = BackgroundType.Solid;
        cell.SetStyle(style);
        cell.PutValue(" ");
        using var stream = new MemoryStream();
        new Aspose.Cells.Rendering.SheetRender(workbook.Worksheets[0], new Aspose.Cells.Rendering.ImageOrPrintOptions
        {
            ImageType = Aspose.Cells.Drawing.ImageType.Png,
            OnePagePerSheet = true,
        }).ToImage(0, stream);
        return stream.ToArray();
    }
}
