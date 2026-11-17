using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>The Slides ops schema and the parser give the same answer, including the rules inherited from target bases.</summary>
public sealed class SlidesOpContractTests
{
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
        Assert.False(OpsSchema<SlidesModule>.Accepts(batch), "The schema accepted an operation the parser rejects.");
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

        Assert.True(OpsSchema<SlidesModule>.Accepts(batch), "The schema rejected an operation the parser accepts.");
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

    private static SlidesOpsBatch Parse(string json) =>
        SlidesOp.Catalog.Parse<SlidesOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
