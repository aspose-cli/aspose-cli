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
    [InlineData("""{"op":"format_range","range":"A1","style":{}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"hAlign":"justify"}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"size":500}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"font":""}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"numberFormat":""}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"size":14}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"font":"Arial","bold":true}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"hAlign":"center"}}""")]
    [InlineData("""{"op":"set_print_area","titleRows":"A1:B2"}""")]
    [InlineData("""{"op":"set_print_area","titleColumns":"B2"}""")]
    [InlineData("""{"op":"set_print_area","titleRows":"1:2:3"}""")]
    [InlineData("""{"op":"resize_columns","from":"B2"}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.True(error.Code == ErrorCodes.OpsInvalid || error.Code == CellsDiagnostics.RangeInvalid, error.Code.Name);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"rename_sheet","sheet":"Data","to":"Archive"}""")]
    [InlineData("""{"op":"group_rows","from":2,"to":4,"collapse":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"size":10.5,"hAlign":"center","indent":2}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"style":{"bold":true,"italic":true,"underline":true,"strikethrough":true,"color":"#C00000","bg":"#FFC7CE","numberFormat":"0.0"}}""")]
    [InlineData("""{"op":"set_print_area","titleRows":"$1:$2","titleColumns":"A"}""")]
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

    [Theory]
    [InlineData("1", "$1:$1")]
    [InlineData("1:2", "$1:$2")]
    [InlineData("$3:$1", "$1:$3")]
    public void PrintTitleRows_AreNormalizedToTheAbsoluteBand(string input, string expected)
    {
        OpsBatch batch = Parse($$"""{"ops":[{"op":"set_print_area","titleRows":"{{input}}"}]}""");

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

    private static OpsBatch Parse(string json) =>
        CellsOps.Catalog.Parse<OpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);

    private string Apply(string path, string operations, string output) =>
        _fixture.Engine.ApplyOps(
            path,
            Parse(operations),
            new EditRequest { OutputPath = _fixture.Temp.File(output), Overwrite = true }).Output!.Path;
}
