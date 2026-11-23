using System.Drawing;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;
using Aspose.Cli.Sdk.Text;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The table's addresses, actions and values on real workbooks: what an edit reads back from the
/// saved file, what it refuses and that a refused batch writes nothing.
/// </summary>
public sealed class CellsTableBehaviorTests
{
    [Fact]
    public void SetSheetName_ReadsTheSheetBackAtItsNewAddress()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("renamed.xlsx");

        CellsEditResult result = Edit(fixture, input, output, """{"ops":[{"set":"sheet/Data","props":{"name":"My Data"}}]}""");

        TableOpReport op = Assert.Single(result.Ops);
        Assert.Equal("sheet/My Data", op.At);
        Assert.True(op.Exists);
        Assert.Equal("My Data", op.Now!["name"]!.GetValue<string>());
        using var saved = new Workbook(output);
        Assert.Equal("My Data", saved.Worksheets[0].Name);
    }

    /// <summary>
    /// Inserted before the last sheet, the engine takes a name another sheet has, in any case, and
    /// the new sheet's address matches both; at the end it refuses the name itself.
    /// </summary>
    [Theory]
    [InlineData("Data", 0, "ADDRESS_AMBIGUOUS")]
    [InlineData("DATA", 1, "ADDRESS_AMBIGUOUS")]
    [InlineData("Data", 2, "ENGINE_ERROR")]
    [InlineData("DATA", 2, "ENGINE_ERROR")]
    public void AddSheet_WithTheNameOfAnotherSheet_IsRefusedAndWritesNothing(string name, int index, string code)
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("duplicate.xlsx");
        string ops = new JsonObject
        {
            ["ops"] = new JsonArray(new JsonObject
            {
                ["add"] = "sheet",
                ["args"] = new JsonObject { ["index"] = index, ["sheetType"] = "Worksheet", ["sheetName"] = name },
            }),
        }.ToJsonString();

        CliException error = Assert.Throws<CliException>(() => Edit(fixture, input, output, ops));

        Assert.Equal(code, error.Code.Name);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void SetRangeValue_WritesTheMatrixAndReadsItBack()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("matrix.xlsx");

        CellsEditResult result = Edit(fixture, input, output,
            """{"ops":[{"set":"sheet/Other/range/A1:C2","props":{"value":[["Name",1,true],["#N/A","'007",null]]}}]}""");

        JsonNode value = Assert.Single(result.Ops).Now!["value"]!;
        Assert.Equal("""[["Name",1,true],["#N/A","007",null]]""", value.ToJsonString());
        using var saved = new Workbook(output);
        Aspose.Cells.Cells cells = saved.Worksheets["Other"].Cells;
        Assert.Equal(CellValueType.IsError, cells["A2"].Type);
        Assert.Equal(CellValueType.IsString, cells["B2"].Type);
        Assert.True(cells["B2"].GetStyle().QuotePrefix);
    }

    [Theory]
    [InlineData("""[["a","b"]]""", "the value is 1 row by 2 columns and the range 2 rows by 3 columns")]
    [InlineData("""[["a","b","c"],["d"]]""", "the value's rows have 3, 1 values")]
    public void SetRangeValue_OfAnotherSize_IsRefusedStatingBoth(string matrix, string fact)
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("matrix.xlsx");

        CliException error = Assert.Throws<CliException>(() => Edit(fixture, input, output,
            $$$"""{"ops":[{"set":"sheet/Other/range/A1:C2","props":{"value":{{{matrix}}}}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(fact, error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ApplyStyle_SetsOnlyTheFlaggedMembers()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("styled.xlsx");

        CellsEditResult result = Edit(fixture, input, output, """
            {"ops":[{"do":"apply_style","args":{
              "range":"sheet/Data/range/A1:C1",
              "style":{"font.isBold":true,"font.isItalic":true,"foregroundColor":"#FFFF00","pattern":"Solid"},
              "flag":["FontBold","CellShading"]}}]}
            """);

        TableOpReport op = Assert.Single(result.Ops);
        Assert.Equal("apply_style", op.Do);
        Assert.Null(op.At);
        using var saved = new Workbook(output);
        Style style = saved.Worksheets["Data"].Cells["B1"].GetStyle();
        Assert.True(style.Font.IsBold);
        Assert.False(style.Font.IsItalic);
        Assert.Equal(BackgroundType.Solid, style.Pattern);
        Assert.Equal(Color.FromArgb(255, 255, 0).ToArgb(), style.ForegroundColor.ToArgb());
    }

    [Fact]
    public void ApplyStyle_WithNoFlag_IsRefusedSayingTheListIsEmpty()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);

        CliException error = Assert.Throws<CliException>(() => Edit(fixture, input, fixture.File("styled.xlsx"),
            """{"ops":[{"do":"apply_style","args":{"range":"sheet/Data/range/A1","style":{"font.isBold":true},"flag":[]}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("the list is empty", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_TakesNoArgs_AndSaysSo()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);

        CliException error = Assert.Throws<CliException>(() => Edit(fixture, input, fixture.File("calculated.xlsx"),
            """{"ops":[{"do":"calculate","args":{"sheet":"sheet/Data"}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("'calculate' takes no args", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormulaActions_ReturnTheAreaTheyWrote()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("formulas.xlsx");

        CellsEditResult result = Edit(fixture, input, output, """
            {"ops":[
              {"do":"set_shared_formula","args":{"cell":"sheet/Data/cell/E2","sharedFormula":"=B2*2","rowNumber":4,"columnNumber":1}},
              {"do":"set_dynamic_array_formula","args":{"cell":"sheet/Other/cell/A1","arrayFormula":"=SEQUENCE(3)","calculateValue":true}},
              {"do":"calculate"}]}
            """);

        Assert.Equal("sheet/Data/cell/E2", result.Ops[0].At);
        Assert.Equal("=B2*2", result.Ops[0].Now!["formula"]!.GetValue<string>());
        Assert.Equal("sheet/Other/range/A1:A3", result.Ops[1].At);
        using var saved = new Workbook(output);
        Assert.Equal("=B5*2", saved.Worksheets["Data"].Cells["E5"].Formula);
        Assert.Equal(3, saved.Worksheets["Other"].Cells["A3"].IntValue);
    }

    [Fact]
    public void Sort_UsesOnlyItsOwnKeys()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string output = fixture.File("sorted.xlsx");

        CellsEditResult result = Edit(fixture, input, output, """
            {"ops":[
              {"do":"sort","args":{"range":"sheet/Data/range/A1:C5","hasHeaders":true,"keys":[{"column":"A","order":"Ascending"}]}},
              {"do":"sort","args":{"range":"sheet/Data/range/A1:C5","hasHeaders":true,"keys":[{"column":"B","order":"Descending"}]}}]}
            """);

        Assert.Equal(2, result.Ops.Count);
        using var saved = new Workbook(output);
        Aspose.Cells.Cells cells = saved.Worksheets["Data"].Cells;
        Assert.Equal(["South", "North", "West", "East"], Enumerable.Range(1, 4).Select(row => cells[row, 0].StringValue));
    }

    [Fact]
    public void CopyRangeAndSheet_FromAnotherFile_CopyWhatItHolds()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string source = fixture.CreateSales();
        string output = fixture.File("copied.xlsx");

        CellsEditResult result = Edit(fixture, input, output, $$$"""
            {"ops":[
              {"do":"copy_range","args":{"range":"sheet/Other/range/A1:B2","sourceRange":"sheet/销售 Sales/range/A1:B2","pasteType":"Values","file":{{{JsonValue.Create(source).ToJsonString()}}}}},
              {"add":"sheet","args":{"index":2,"sheetType":"Worksheet","sheetName":"Copy"}},
              {"do":"copy_sheet","args":{"sheet":"sheet/Copy","sourceSheet":"sheet/汇总 Summary","file":{{{JsonValue.Create(source).ToJsonString()}}}}}]}
            """);

        Assert.Equal("sheet/Other/range/A1:B2", result.Ops[0].At);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""[["区域 Region","Q1"],["华东 East",1250]]"""), result.Ops[0].Now!["value"]));
        Assert.Equal("sheet/Copy", result.Ops[2].At);
        using var saved = new Workbook(output);
        Assert.Equal("Revenue", saved.Worksheets["Copy"].Cells["A1"].StringValue);
    }

    [Fact]
    public void CopyRange_FromAFileWithoutTheRangesSheet_IsNotFound()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);
        string source = fixture.CreateSales();
        string output = fixture.File("copied.xlsx");

        CliException error = Assert.Throws<CliException>(() => Edit(fixture, input, output, $$$"""
            {"ops":[{"do":"copy_range","args":{"range":"sheet/Other/range/A1","sourceRange":"sheet/Data/range/A1","pasteType":"All","file":{{{JsonValue.Create(source).ToJsonString()}}}}}]}
            """));

        Assert.Equal(ErrorCodes.AddressNotFound, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Read_ListsNoGridKindUnlessNamed_AndSaysWhichItLeftOut()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);

        CellsReadResult all = CellsRead.Run(fixture.Session, new TableReadRequest { Input = input });
        CellsReadResult cells = CellsRead.Run(fixture.Session, new TableReadRequest { Input = input, At = "sheet/Data", Kinds = ["cell"], MaxObjects = 1000 });

        Assert.DoesNotContain(all.Objects, item => CellsKinds.ListedWhenNamed.Contains(item.Kind));
        Assert.Equal(CellsKinds.ListedWhenNamed.Order(StringComparer.Ordinal), all.Window!.NotListedUnlessNamed!.Order(StringComparer.Ordinal));
        Assert.Contains(cells.Objects, item => item.At == "sheet/Data/cell/D2" && item.Props["formula"]!.GetValue<string>() == "=B2+C2");
        Assert.Null(cells.Window?.NotListedUnlessNamed);
    }

    [Fact]
    public void Sheet_ReadsTheAreaThatHoldsItsData()
    {
        using var fixture = new CellsFixture();
        string input = CellsTableRoundTripTests.Book(fixture);

        CellsReadResult sheet = CellsRead.Run(fixture.Session, new TableReadRequest { Input = input, At = "sheet/Data" });

        string range = sheet.Objects.Single(item => item.At == "sheet/Data").Props["maxDisplayRange"]!.GetValue<string>();
        Assert.StartsWith("A1:", range, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    public void ReadSearchAndEdit_OfTextOnlyConvertTakes_AreFormatUnsupported(string format)
    {
        using var fixture = new CellsFixture();
        string input = fixture.File("book." + format);
        File.WriteAllText(input, format == "csv" ? "a,b\n1,2\n" : "a\tb\n1\t2\n");

        CliException read = Assert.Throws<CliException>(() => CellsRead.Run(fixture.Session, new TableReadRequest { Input = input }));
        CliException search = Assert.Throws<CliException>(() => CellsSearch.Run(fixture.Session, new TableSearchRequest
        {
            Input = input,
            Query = new SearchQuery(TextSearch.Create("a", regex: false, caseSensitive: true), 10),
        }));
        CliException edit = Assert.Throws<CliException>(() => Edit(fixture, input, fixture.File("out.xlsx"), """{"ops":[{"do":"calculate"}]}"""));

        Assert.All([read, search, edit], error => Assert.Equal(ErrorCodes.FormatUnsupported, error.Code));
    }

    [Fact]
    public void EachFormat_HasTheEngineMappingItsUsesNeed()
    {
        var log = new EngineWarningLog();
        foreach (FormatDescriptor format in CellsFormats.Definitions)
        {
            if (format.Uses.HasFlag(FormatUse.Convert))
            {
                Assert.NotNull(CellsEngineFormats.Options(format.Id, log));
            }

            if (format.Uses.HasFlag(FormatUse.Render))
            {
                _ = CellsEngineFormats.Image(format.Id);
            }
        }

        Assert.All(CellsFormats.Editable, format => Assert.Contains(
            Enum.GetValues<FileFormatType>(), type => CellsEngineFormats.ReadAs(type) == format.Id));
    }

    [Fact]
    public void Result_StatesTheLicenseModeTheEngineRanIn()
    {
        using var fixture = new CellsFixture();
        string input = fixture.CreateSales();

        CellsReadResult result = CellsRead.Run(fixture.Session, new TableReadRequest { Input = input });

        Assert.Equal(fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed, result.License is null);
    }

    private static CellsEditResult Edit(CellsFixture fixture, string input, string output, string ops) =>
        CellsEdit.Run(fixture.Session, CellsFixture.Edit(input, output, ops));
}
