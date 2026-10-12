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
    [InlineData("""{"op":"rename_sheet","name":"Archive"}""")]
    [InlineData("""{"op":"ungroup_rows","first":2,"collapse":true}""")]
    [InlineData("""{"op":"ungroup_columns","first":"B","collapse":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"alignment":"justify"}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"fontSize":500}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"font":""}}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"numberFormat":""}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"matchStyle":{"size":14}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"matchStyle":{"font":"Arial","bold":true}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"matchStyle":{"hAlign":"center"}}""")]
    [InlineData("""{"op":"set_print_area","range":"A1:C3","titleRows":"A1:B2"}""")]
    [InlineData("""{"op":"set_print_area","range":"A1:C3","titleColumns":"B2"}""")]
    [InlineData("""{"op":"set_print_area","range":"A1:C3","titleRows":"1:2:3"}""")]
    [InlineData("""{"op":"resize_columns","first":"B2"}""")]
    [InlineData("""{"op":"set_autofilter"}""")]
    [InlineData("""{"op":"set_page_setup"}""")]
    [InlineData("""{"op":"set_chart","index":1,"name":"Sales","title":"Revised"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"docs/a"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"file:///c:/a.txt"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"javascript:alert(1)"}""")]
    [InlineData("""{"op":"add_comment","cell":"A1","text":""}""")]
    [InlineData("""{"op":"insert_image","path":"logo.png","at":"A1:B2"}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"matchStyle":{}}""")]
    [InlineData("""{"op":"set_page_setup","margins":{}}""")]
    [InlineData("""{"op":"insert_chart","kind":"line","dataRange":"A1:B3","at":"D2:H9","legend":{}}""")]
    [InlineData("""{"op":"set_sheet_view"}""")]
    [InlineData("""{"op":"set_values","range":"A1","values":[[]]}""")]
    [InlineData("""{"op":"set_chart","index":1,"seriesInRows":false}""")]
    [InlineData("""{"op":"import_range","path":"eu.xlsx","from":"A1:B2","to":"C1","content":"formulas"}""")]
    [InlineData("""{"op":"import_range","path":"eu.xlsx","to":"C1"}""")]
    [InlineData("""{"op":"import_sheet","path":" "}""")]
    [InlineData("""{"op":"import_sheet","path":"eu.xlsx","at":0}""")]
    [InlineData("""{"op":"insert_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"X"}],"captions":"fr"}""")]
    [InlineData("""{"op":"insert_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"X","label":" "}]}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"format_range","range":"B2:C6,E2:F6","style":{"bold":true}}""", "range")]
    [InlineData("""{"op":"set_values","range":"A1,C1","values":[[1]]}""", "range")]
    [InlineData("""{"op":"clear_range","range":"A1:B2, D1:E2"}""", "range")]
    [InlineData("""{"op":"add_comment","cell":"A1,B2","text":"x"}""", "cell")]
    [InlineData("""{"op":"insert_chart","kind":"line","dataRange":"A1:B3,D1:E3","at":"G2:K9"}""", "dataRange")]
    [InlineData("""{"op":"insert_chart","kind":"line","dataRange":"A1:B3","at":"G2:K9,G12:K19"}""", "at")]
    public void ASingleRangeFieldGivenAList_SaysItTakesOneRange(string operation, string field)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains($"{field} must be", error.Message, StringComparison.Ordinal);
        Assert.Contains("lists 2 ranges separated by commas, but takes one range", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("separator", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"op":"rename_sheet","sheet":"Data","name":"Archive"}""")]
    [InlineData("""{"op":"group_rows","first":2,"last":4,"collapsed":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"fontSize":10.5,"alignment":"center","indent":2}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates"},"matchStyle":{"bold":true,"italic":true,"underline":true,"strikethrough":true,"color":"#C00000","fillColor":"#FFC7CE","numberFormat":"0.0"}}""")]
    [InlineData("""{"op":"set_print_area","range":"A1:C3","titleRows":"$1:$2","titleColumns":"A"}""")]
    [InlineData("""{"op":"clear_autofilter"}""")]
    [InlineData("""{"op":"set_chart","name":"Sales","title":"Revised"}""")]
    [InlineData("""{"op":"set_sheet_view","gridlines":false}""")]
    [InlineData("""{"op":"insert_chart","kind":"line","dataRange":"A1:B3","at":"D2:H9","legend":{"visible":false}}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"https://example.com/a"}""")]
    [InlineData("""{"op":"set_hyperlink","cell":"A1","url":"mailto:team@example.com"}""")]
    [InlineData("""{"op":"import_range","path":"eu.xlsx","from":"Totals!A2:D8","to":"Report!F2","content":"all","sourcePasswordEnv":"EU_PASSWORD"}""")]
    [InlineData("""{"op":"import_sheet","path":"eu.csv","sourceSheet":"eu","name":"EU","at":1}""")]
    [InlineData("""{"op":"insert_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"净额","label":"净额合计"}],"captions":"zh"}""")]
    [InlineData("""{"op":"insert_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"Amount"}],"captions":"en"}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
    }

    [Theory]
    [InlineData("""{"op":"merge_cells","range":"A1:B2","unexpected":true}""")]
    [InlineData("""{"op":"format_range","range":"A1","style":{"bold":true,"unexpected":true}}""")]
    [InlineData("""{"op":"set_page_setup","margins":{"top":72,"unexpected":1}}""")]
    [InlineData("""{"op":"insert_chart","kind":"pie","dataRange":"A1:B3","at":"D2:H9","legend":{"visible":true,"unexpected":1}}""")]
    [InlineData("""{"op":"add_conditional_format","range":"A1:A9","rule":{"kind":"duplicates","unexpected":1},"matchStyle":{"bold":true}}""")]
    [InlineData("""{"op":"sort_range","range":"A1:B9","by":[{"column":"A","unexpected":1}]}""")]
    [InlineData("""{"op":"insert_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"X","unexpected":1}]}""")]
    public void Schema_RejectsFieldsTheContractDoesNotKnow(string operation) =>
        Assert.False(IsSchemaValid($$"""{"ops":[{{operation}}]}"""));

    [Fact]
    public void AHeaderMarginInsideMargins_PointsToHeaderMargin()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"op":"set_page_setup","margins":{"top":72,"header":20}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.StartsWith("The header and footer distances are headerMargin and footerMargin, beside margins.", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void A1Value_TheSchemaCannotStateIsInvalidWithTheParsersReason()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"op":"merge_cells","range":"B2:A"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
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
            CellsImportContents.Values,
            Assert.IsType<ImportRangeOp>(Assert.Single(
                Parse("""{"ops":[{"op":"import_range","path":"eu.xlsx","from":"A1:B2","to":"C1"}]}""").Ops)).Content);

    [Fact]
    public void CreatePivot_ChoosesTheCaptionLanguageByDefaultAndLeavesLabelsUnset()
    {
        InsertPivotOp pivot = Assert.IsType<InsertPivotOp>(Assert.Single(
            Parse("""{"ops":[{"op":"insert_pivot","sourceRange":"A1:B9","at":"D1","values":[{"field":"X"}]}]}""").Ops));

        Assert.Equal(CellsPivotCaptionLanguages.Auto, pivot.Captions);
        Assert.Null(Assert.Single(pivot.Values).Label);
    }

    [Theory]
    [InlineData("1", "$1:$1")]
    [InlineData("1:2", "$1:$2")]
    [InlineData("$3:$1", "$1:$3")]
    public void PrintTitleRows_AreNormalizedToTheAbsoluteBand(string input, string expected)
    {
        CellsOpsBatch batch = Parse($$"""{"ops":[{"op":"set_print_area","range":"A1:C3","titleRows":"{{input}}"}]}""");

        Assert.Equal(expected, Assert.IsType<SetPrintAreaOp>(Assert.Single(batch.Ops)).TitleRows);
    }

    [Fact]
    public void SetPrintArea_ReplacesTheAreaAndTitlesAndClearPrintAreaRemovesThem()
    {
        string source = _fixture.CreateSalesWorkbook("print-area.xlsx");
        string titled = Apply(source, """{"ops":[{"op":"set_print_area","sheet":"Data","range":"A1:C3","titleRows":"1","titleColumns":"A:B"}]}""", "print-area.titled.xlsx");
        string framed = Apply(titled, """{"ops":[{"op":"set_print_area","sheet":"Data","range":"A1:B2"}]}""", "print-area.framed.xlsx");

        using (var workbook = new Workbook(titled))
        {
            PageSetup setup = workbook.Worksheets["Data"].PageSetup;
            Assert.Equal(("A1:C3", "$1:$1", "$A:$B"), (setup.PrintArea, setup.PrintTitleRows, setup.PrintTitleColumns));
        }

        using (var workbook = new Workbook(framed))
        {
            PageSetup setup = workbook.Worksheets["Data"].PageSetup;
            Assert.Equal("A1:B2", setup.PrintArea);
            Assert.True(string.IsNullOrEmpty(setup.PrintTitleRows) && string.IsNullOrEmpty(setup.PrintTitleColumns));
        }

        string cleared = Apply(titled, """{"ops":[{"op":"clear_print_area","sheet":"Data"}]}""", "print-area.cleared.xlsx");
        using var final = new Workbook(cleared);
        PageSetup clearedSetup = final.Worksheets["Data"].PageSetup;
        Assert.True(string.IsNullOrEmpty(clearedSetup.PrintArea) && string.IsNullOrEmpty(clearedSetup.PrintTitleRows)
            && string.IsNullOrEmpty(clearedSetup.PrintTitleColumns));
    }

    [Theory]
    [InlineData("justify", null)]
    [InlineData(null, "baseline")]
    public void StyleWriter_RejectsAnAlignmentItDoesNotMap(string? horizontal, string? vertical)
    {
        using var workbook = new Workbook();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StyleWriter.Apply(workbook.CreateStyle(), new CellsStyleData { Alignment = horizontal, VerticalAlignment = vertical }));
    }

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    [Theory]
    [InlineData("""{"op":"insert_sheet","name":"Summary","position":0}""", "at")]
    [InlineData("""{"op":"import_sheet","path":"eu.xlsx","position":0}""", "at")]
    [InlineData("""{"op":"move_sheet","sheet":"Data","position":0}""", "to")]
    public void ARenamedSheetPositionField_IsRefusedAndSuggestsItsNewName(string operation, string field)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains(field, error.Details!.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains($"Did you mean '{field}'", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("create_chart", "insert_chart")]
    [InlineData("create_pivot", "insert_pivot")]
    [InlineData("create_table", "define_table")]
    [InlineData("edit_comment", "set_comment")]
    [InlineData("remove_duplicates", "delete_duplicates")]
    [InlineData("remove_hyperlink", "delete_hyperlink")]
    [InlineData("update_chart", "set_chart")]
    [InlineData("add_sheet", "insert_sheet")]
    [InlineData("add_chart", "insert_chart")]
    [InlineData("add_pivot", "insert_pivot")]
    [InlineData("add_table", "define_table")]
    [InlineData("add_image", "insert_image")]
    [InlineData("add_name", "define_name")]
    public void ARenamedOperation_IsRefusedAndSuggestsItsNewName(string old, string renamed)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{"op":"{{old}}"}]}"""));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Equal(renamed, error.Details!["suggestions"]!.AsArray()[0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"op":"rename_sheet","sheet":"S3","name":"华东/销售"}""", "holds '/'")]
    [InlineData("""{"op":"insert_sheet","name":"Q1:Q2"}""", "holds ':'")]
    [InlineData("""{"op":"insert_sheet","name":"'Quoted"}""", "apostrophe")]
    [InlineData("""{"op":"rename_sheet","sheet":"S3","name":"history"}""", "History")]
    [InlineData("""{"op":"import_sheet","path":"a.xlsx","name":"[draft]"}""", "holds '['")]
    [InlineData("""{"op":"insert_sheet","name":"A name that is longer than thirty-one"}""", "31")]
    public void ASheetNameExcelRefuses_IsAnOperationError(string operation, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains(reason, error.Details!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownOperation_ListsTheCommonOperationsFirst()
    {
        CliException error = Assert.Throws<CliException>(() => Parse("""{"ops":[{"op":"write_cells"}]}"""));

        string[] available = [.. error.Details!["available"]!.AsArray().Select(static name => name!.GetValue<string>())];
        Assert.True(error.Details["availableCount"]!.GetValue<int>() > available.Length);
        Assert.True(Array.IndexOf(available, "set_values") is >= 0 and < 5, string.Join(", ", available));
    }

    [Theory]
    [InlineData("""{"op":"set_autofilter","sheet":"Data","off":true}""", "clear_autofilter")]
    [InlineData("""{"op":"set_tab_color","sheet":"Data"}""", "clear_tab_color")]
    [InlineData("""{"op":"set_print_area","sheet":"Data","titleRows":"1"}""", "clear_print_area")]
    [InlineData("""{"op":"freeze_panes","sheet":"Data","cell":"A1"}""", "unfreeze_panes")]
    public void AnOperationThatWouldRemoveState_PointsAtTheOperationThatRemovesIt(string operation, string remover)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal("OPS_INVALID", error.Code.Name);
        Assert.Contains(remover, error.Hint, StringComparison.Ordinal);
    }

    private static CellsOpsBatch Parse(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);

    private string Apply(string path, string operations, string output) =>
        CellsEdit.Run(_fixture.Session,
            new CellsEditRequest { Input = path, Batch = Parse(operations), Output = TestOutput.At(_fixture.Temp.File(output), overwrite: true) }).Output!.Path;
}
