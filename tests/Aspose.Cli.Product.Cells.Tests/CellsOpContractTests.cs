using System.Text.Json;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The ops schema and the parser agree: what one accepts the other accepts, and a
/// field the contract does not know is rejected by both.
/// </summary>
public sealed class CellsOpContractTests : IClassFixture<CellsFixture>
{
    private static readonly Lazy<JsonSchema> Schema = new(static () => JsonSchema.FromText(
        ProductCatalog.Build([new CellsModule()]).Resources.Read("v2/cells/ops"),
        SchemaTestRegistry.CreateOptions()));

    private readonly CellsFixture _fixture;

    public CellsOpContractTests(CellsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("""{"op":"rename_sheet","to":"Archive"}""")]
    [InlineData("""{"op":"ungroup_rows","from":2,"collapse":true}""")]
    [InlineData("""{"op":"ungroup_columns","from":"B","collapse":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"hAlign":"justify"}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"size":500}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"font":""}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"numberFormat":""}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"size":14}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"font":"Arial","bold":true}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"hAlign":"center"}}""")]
    [InlineData("""{"op":"set_print_area","titleRows":"A1:B2"}""")]
    [InlineData("""{"op":"set_print_area","titleColumns":"B2"}""")]
    [InlineData("""{"op":"set_print_area","titleRows":"1:2:3"}""")]
    [InlineData("""{"op":"resize_columns","from":"B2"}""")]
    [InlineData("""{"op":"set_autofilter"}""")]
    [InlineData("""{"op":"set_page_setup"}""")]
    [InlineData("""{"op":"update_chart","index":0,"name":"Sales","title":"Revised"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"docs/a"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"file:///c:/a.txt"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"javascript:alert(1)"}""")]
    [InlineData("""{"op":"add_comment","cell":"A1","text":""}""")]
    [InlineData("""{"op":"insert_image","path":"logo.png","at":"A1:B2"}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{}}""")]
    [InlineData("""{"op":"set_page_setup","margins":{}}""")]
    [InlineData("""{"op":"create_chart","type":"line","dataRange":"A1:B3","at":"D2:H9","legend":{}}""")]
    [InlineData("""{"op":"set_sheet_view"}""")]
    [InlineData("""{"op":"set_values","range":"A1","values":[[]]}""")]
    [InlineData("""{"op":"update_chart","index":0,"seriesInRows":false}""")]
    [InlineData("""{"op":"import_range","path":"eu.xlsx","from":"A1:B2","to":"C1","content":"formulas"}""")]
    [InlineData("""{"op":"import_range","path":"eu.xlsx","to":"C1"}""")]
    [InlineData("""{"op":"import_sheet","path":" "}""")]
    [InlineData("""{"op":"import_sheet","path":"eu.xlsx","position":-1}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"rename_sheet","sheet":"Data","to":"Archive"}""")]
    [InlineData("""{"op":"group_rows","from":2,"to":4,"collapse":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"size":10.5,"hAlign":"center","indent":2}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"bold":true,"italic":true,"underline":true,"strikethrough":true,"color":"#C00000","bg":"#FFC7CE","numberFormat":"0.0"}}""")]
    [InlineData("""{"op":"set_print_area","titleRows":"$1:$2","titleColumns":"A"}""")]
    [InlineData("""{"op":"set_autofilter","off":true}""")]
    [InlineData("""{"op":"update_chart","name":"Sales","title":"Revised"}""")]
    [InlineData("""{"op":"set_sheet_view","gridlines":false}""")]
    [InlineData("""{"op":"create_chart","type":"line","dataRange":"A1:B3","at":"D2:H9","legend":{"visible":false}}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"https://example.com/a"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"mailto:team@example.com"}""")]
    [InlineData("""{"op":"import_range","path":"eu.xlsx","from":"Totals!A2:D8","to":"Report!F2","content":"all","passwordEnv":"EU_PASSWORD"}""")]
    [InlineData("""{"op":"import_sheet","path":"eu.csv","sheet":"eu","name":"EU","position":0}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
    }

    [Theory]
    [InlineData("""{"op":"merge_cells","range":"A1:B2","unexpected":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"bold":true,"unexpected":true}}""")]
    [InlineData("""{"op":"set_page_setup","margins":{"top":1,"unexpected":1}}""")]
    [InlineData("""{"op":"create_chart","type":"pie","dataRange":"A1:B3","at":"D2:H9","legend":{"visible":true,"unexpected":1}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates","unexpected":1},"style":{"bold":true}}""")]
    [InlineData("""{"op":"sort_range","range":"A1:B9","by":[{"column":"A","unexpected":1}]}""")]
    [InlineData("""{"op":"create_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"X","unexpected":1}]}""")]
    public void Schema_RejectsFieldsTheContractDoesNotKnow(string operation) =>
        Assert.False(IsSchemaValid($$"""{"ops":[{{operation}}]}"""));

    [Fact]
    public void A1Value_TheSchemaCannotStateIsInvalidWithTheParsersReason()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"op":"merge_cells","range":"B2:A"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(0, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("merge_cells", error.Details["op"]!.GetValue<string>());
        Assert.StartsWith("range must be an A1 cell or range on the operation's sheet, such as B2 or B2:D10: ", error.Details["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportRange_RefusesADestinationRange()
    {
        CliException error = Assert.Throws<CliException>(() =>
            Parse("""{"ops":[{"op":"import_range","path":"eu.xlsx","from":"A1:B2","to":"C1:D2"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("single anchor cell", error.Details!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportRange_ImportsValuesByDefault() =>
        Assert.Equal(
            ImportContents.Values,
            Assert.IsType<ImportRangeOp>(Assert.Single(
                Parse("""{"ops":[{"op":"import_range","path":"eu.xlsx","from":"A1:B2","to":"C1"}]}""").Ops)).Content);

    [Theory]
    [InlineData("1", "$1:$1")]
    [InlineData("1:2", "$1:$2")]
    [InlineData("$3:$1", "$1:$3")]
    public void PrintTitleRows_AreNormalizedToTheAbsoluteBand(string input, string expected)
    {
        CellsOpsBatch batch = Parse($$"""{"ops":[{"op":"set_print_area","titleRows":"{{input}}"}]}""");

        Assert.Equal(expected, Assert.IsType<SetPrintAreaOp>(Assert.Single(batch.Ops)).TitleRows);
    }

    [Fact]
    public void PrintTitlesAlone_KeepThePrintAreaAndAnEmptyOpClearsIt()
    {
        string source = _fixture.CreateSalesWorkbook("print-area.xlsx");
        string framed = Apply(source, """{"ops":[{"op":"set_print_area","sheet":"Data","range":"A1:C3"}]}""", "print-area.framed.xlsx");
        string titled = Apply(framed, """{"ops":[{"op":"set_print_area","sheet":"Data","titleRows":"1","titleColumns":"A:B"}]}""", "print-area.titled.xlsx");

        using (var workbook = new Workbook(titled))
        {
            PageSetup setup = workbook.Worksheets["Data"].PageSetup;
            Assert.Equal("A1:C3", setup.PrintArea);
            Assert.Equal("$1:$1", setup.PrintTitleRows);
            Assert.Equal("$A:$B", setup.PrintTitleColumns);
        }

        string cleared = Apply(titled, """{"ops":[{"op":"set_print_area","sheet":"Data"}]}""", "print-area.cleared.xlsx");
        using var final = new Workbook(cleared);
        Assert.True(string.IsNullOrEmpty(final.Worksheets["Data"].PageSetup.PrintArea));
        Assert.Equal("$1:$1", final.Worksheets["Data"].PageSetup.PrintTitleRows);
    }

    [Theory]
    [InlineData("justify", null)]
    [InlineData(null, "baseline")]
    public void StyleWriter_RejectsAnAlignmentItDoesNotMap(string? horizontal, string? vertical)
    {
        using var workbook = new Workbook();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StyleWriter.Apply(workbook.CreateStyle(), new StyleData { HAlign = horizontal, VAlign = vertical }));
    }

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    private static CellsOpsBatch Parse(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);

    private string Apply(string path, string operations, string output) =>
        _fixture.Engine.ApplyOps(
            path,
            Parse(operations),
            new EditRequest { OutputPath = _fixture.Temp.File(output), Overwrite = true }).Output!.Path;
}
