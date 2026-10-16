using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>The Slides ops schema and the parser give the same answer, including the rules inherited from target bases.</summary>
public sealed class SlidesOpContractTests
{
    private static readonly Lazy<JsonSchema> Schema = new(static () => JsonSchema.FromText(
        ProductCatalog.Build([new SlidesModule()]).Resources.Read("v2/slides/ops"),
        SchemaTestRegistry.CreateOptions()));

    [Theory]
    [InlineData("""{"op":"set_title","slide":1,"slideId":256,"text":"x"}""")]
    [InlineData("""{"op":"set_title","slideId":0,"text":"x"}""")]
    [InlineData("""{"op":"set_text","slide":1,"text":"x"}""")]
    [InlineData("""{"op":"set_text","slide":1,"shapeName":"","text":"x"}""")]
    [InlineData("""{"op":"delete_slides","slides":"0"}""")]
    [InlineData("""{"op":"set_background","color":"#FFFFFF","path":"a.png"}""")]
    [InlineData("""{"op":"set_body","slide":1,"paragraphs":[{"text":"x","list":{"level":10}}]}""")]
    [InlineData("""{"op":"insert_table","slide":1,"rect":{"x":0,"y":0,"width":10,"height":10},"rowCount":101,"columnCount":1}""")]
    [InlineData("""{"op":"set_transition","slides":"1"}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{}}""")]
    [InlineData("""{"op":"set_footer"}""")]
    [InlineData("""{"op":"set_slide_size","slideSize":{"preset":"16x9","width":800,"height":450}}""")]
    [InlineData("""{"op":"insert_section","name":" ","at":1}""")]
    [InlineData("""{"op":"set_text","slide":1,"shapeName":" ","text":"x"}""")]
    [InlineData("""{"op":"set_layout","slides":"1","layout":" "}""")]
    [InlineData("""{"op":"insert_slide","layout":""}""")]
    [InlineData("""{"op":"import_slides","path":" "}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"font":"+mn-lt"}}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"latinFont":"+mn-ea"}}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"eastAsianFont":"+mj-lt"}}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"latinFont":""}}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"set_text","slideId":256,"placeholder":"body","text":"x"}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"bold":false}}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"latinFont":"+mj-lt","eastAsianFont":"+mn-ea"}}""")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"font":"Microsoft YaHei","latinFont":"Calibri"}}""")]
    [InlineData("""{"op":"set_footer","showNumber":false}""")]
    [InlineData("""{"op":"insert_chart","slide":1,"kind":"scatter","rect":{"x":0,"y":0,"width":10,"height":10},"categories":["a"],"series":[{"name":"s","values":[1],"xValues":[2]}]}""")]
    [InlineData("""{"op":"replace_text","find":" ","replace":"_"}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
    }

    [Theory]
    [InlineData("""{"op":"replace_text","find":"(","replace":"x","regex":true}""")]
    [InlineData("""{"op":"insert_chart","slide":1,"kind":"bar","rect":{"x":0,"y":0,"width":10,"height":10},"categories":["a","b"],"series":[{"name":"s","values":[1]}]}""")]
    [InlineData("""{"op":"insert_table","slide":1,"rect":{"x":0,"y":0,"width":10,"height":10},"rowCount":1,"columnCount":1,"values":[["a","b"]]}""")]
    public void Parser_RejectsWhatTheOperationSummaryStates(string operation)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Theory]
    [InlineData("add_slide", "insert_slide")]
    [InlineData("append_presentation", "import_slides")]
    [InlineData("apply_layout", "set_layout")]
    [InlineData("duplicate_slide", "copy_slide")]
    [InlineData("set_shape_style", "format_shape")]
    [InlineData("update_chart_data", "set_chart_data")]
    [InlineData("add_section", "insert_section")]
    public void RenamedOperation_IsRefusedWithItsNewName(string old, string renamed)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{"op":"{{old}}"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(renamed, (string?)error.Details!["suggestions"]![0]);
    }

    [Theory]
    [InlineData("""{"op":"insert_section","name":"A","startSlide":1}""", "at")]
    [InlineData("""{"op":"replace_text","find":"a","replace":"b","matchCase":true}""", "caseSensitive")]
    [InlineData("""{"op":"set_background","imagePath":"a.png"}""", "path")]
    [InlineData("""{"op":"import_slides","path":"a.pptx","masterPolicy":"keep-source"}""", "formatting")]
    [InlineData("""{"op":"insert_table","slide":1,"rect":{"x":0,"y":0,"width":10,"height":10},"rowCount":1,"columnCount":1,"data":[["a"]]}""", "values")]
    [InlineData("""{"op":"set_table_cell","slide":1,"shapeId":2,"row":1,"col":1,"text":"a"}""", "column")]
    [InlineData("""{"op":"set_slide_size","size":"16x9"}""", "slideSize")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"fill":"#FFFFFF"}}""", "fillColor")]
    [InlineData("""{"op":"format_shape","slide":1,"shapeId":2,"style":{"size":12}}""", "fontSize")]
    [InlineData("""{"op":"insert_chart","slide":1,"type":"bar","rect":{"x":0,"y":0,"width":10,"height":10},"categories":["a"],"series":[{"name":"s","values":[1]}]}""", "kind")]
    [InlineData("""{"op":"insert_chart","slide":1,"kind":"bar","box":{"x":0,"y":0,"width":10,"height":10},"categories":["a"],"series":[{"name":"s","values":[1]}]}""", "rect")]
    public void RenamedField_IsRefusedWithItsNewName(string operation, string renamed)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(renamed, (string?)error.Details!["suggestions"]![0]);
    }

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    /// <summary>
    /// An import that names its file with source, as other tools do, is refused with path as the
    /// first suggestion and a hint that names it.
    /// </summary>
    [Theory]
    [InlineData("""{"op":"import_slides","source":"a.pptx"}""")]
    public void AnImportThatNamesItsFileSource_SuggestsPath(string operation)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal("path", error.Details!["suggestions"]!.AsArray()[0]!.GetValue<string>());
        Assert.StartsWith("Did you mean 'path'", error.Hint, StringComparison.Ordinal);
    }

    private static SlidesOpsBatch Parse(string json) =>
        SlidesOp.Catalog.Parse<SlidesOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
