using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>The Words ops schema and the parser give the same answer for block addresses and operation rules.</summary>
public sealed class WordsOpContractTests
{
    private static readonly Lazy<JsonSchema> Schema = new(static () => JsonSchema.FromText(
        ProductCatalog.Build([new WordsModule()]).Resources.Read("v2/words/ops"),
        SchemaTestRegistry.CreateOptions()));

    [Theory]
    [InlineData("""{"op":"delete_blocks","target":{"blocks":"0"}}""")]
    [InlineData("""{"op":"set_text","at":{"block":1,"find":"Intro"},"text":"a"}""")]
    [InlineData("""{"op":"set_text","at":{"find":""},"text":"a"}""")]
    [InlineData("""{"op":"insert_markdown","at":{"block":1},"position":"after","markdown":""}""")]
    [InlineData("""{"op":"set_header","paragraphs":["a"],"markdown":"b"}""")]
    [InlineData("""{"op":"accept_revisions","author":""}""")]
    [InlineData("""{"op":"format_text","target":{"block":1},"size":null}""")]
    [InlineData("""{"op":"add_watermark","imagePath":"logo.png","color":"#FF0000"}""")]
    [InlineData("""{"op":"add_section","position":"end","after":1}""")]
    [InlineData("""{"op":"add_section","after":1}""")]
    [InlineData("""{"op":"add_section","position":"after"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"docs/a"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"file:///c:/a.txt"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"javascript:alert(1)"}""")]
    [InlineData("""{"op":"append_document","path":" "}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"delete_blocks","target":{"blocks":"1-3,7"}}""")]
    [InlineData("""{"op":"set_footer","markdown":"_Confidential_"}""")]
    [InlineData("""{"op":"set_properties","custom":{"Reviewer":null}}""")]
    [InlineData("""{"op":"mail_merge","inline":[{"Name":null}]}""")]
    [InlineData("""{"op":"add_section","position":"after","after":1}""")]
    [InlineData("""{"op":"add_section"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"https://example.com/a"}""")]
    [InlineData("""{"op":"insert_hyperlink","at":{"block":1},"position":"after","text":"a","url":"mailto:team@example.com"}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
    }

    [Theory]
    [InlineData("""{"op":"insert_table","at":{"block":1},"position":"after","rows":1,"cols":2,"data":[["a"],["b"]]}""", "more rows")]
    public void Parser_RejectsRulesTheRecordsStateInTheirSummaries(string operation, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    private static WordsOpsBatch Parse(string json) =>
        WordsOp.Catalog.Parse<WordsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
