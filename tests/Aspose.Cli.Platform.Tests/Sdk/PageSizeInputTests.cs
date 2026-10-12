using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The page size input every product's operations share: the SDK describes it once, and a
/// vocabulary of another assembly, here the test vocabulary, publishes and checks it as its own.
/// </summary>
public sealed class PageSizeInputTests
{
    private static readonly Lazy<JsonSchema> Published = new(static () => JsonSchema.FromText(
        TestOp.Catalog.Describe("edit").Schema.Document, new BuildOptions { SchemaRegistry = new SchemaRegistry() }));

    [Theory]
    [InlineData("""{"preset":"a4"}""", true)]
    [InlineData("""{"preset":"letter"}""", true)]
    [InlineData("""{"width":300,"height":400.5}""", true)]
    [InlineData("""{"preset":"a4","width":300}""", false)]
    [InlineData("""{"preset":"a4","height":300}""", false)]
    [InlineData("""{"preset":"a4","width":300,"height":400}""", true)]
    [InlineData("""{"preset":"a4","width":841.89,"height":595.28}""", true)]
    [InlineData("""{"width":300}""", false)]
    [InlineData("""{"height":300}""", false)]
    [InlineData("""{}""", false)]
    [InlineData("""{"preset":"A4"}""", false)]
    [InlineData("""{"preset":"b5"}""", false)]
    [InlineData("""{"width":0,"height":400}""", false)]
    [InlineData("""{"width":300,"height":-1}""", false)]
    public void ParserAndSchemaAgree(string pageSize, bool valid)
    {
        string operation = $$"""{"op":"size_pages","pageSize":{{pageSize}}}""";
        using JsonDocument document = JsonDocument.Parse($$"""{"ops":[{{operation}}]}""");

        Assert.Equal(valid, !Parse(operation).StartsWith("error: ", StringComparison.Ordinal));
        Assert.Equal(valid, Published.Value.Evaluate(document.RootElement).IsValid);
    }

    [Theory]
    [InlineData("""{"preset":"a4","width":300}""", "pageSize must set height when it sets width")]
    [InlineData("""{}""", "pageSize must set at least one of: preset, width")]
    [InlineData("""{"width":300}""", "pageSize must set height when it sets width")]
    [InlineData("""{"preset":"b5"}""", "pageSize.preset does not accept 'b5'")]
    [InlineData("""{"width":0,"height":400}""", "pageSize.width must be greater than 0")]
    public void Parse_RejectsByTheWirePath(string pageSize, string reason) =>
        Assert.Equal("error: " + reason, Parse($$"""{"op":"size_pages","pageSize":{{pageSize}}}"""));

    [Fact]
    public void Schema_PublishesTheSdkRecordAsADefinitionOfTheVocabulary()
    {
        JsonObject definitions = JsonNode.Parse(TestOp.Catalog.Describe("edit").Schema.Document)!["$defs"]!.AsObject();
        JsonObject input = definitions["pageSizeInput"]!.AsObject();
        JsonObject properties = input["properties"]!.AsObject();

        Assert.Equal("#/$defs/pageSizeInput", definitions["size_pages"]!["properties"]!["pageSize"]!["$ref"]!.GetValue<string>());
        Assert.Equal("""["a3","a4","a5","letter","legal","tabloid"]""", properties["preset"]!["enum"]!.ToJsonString());
        Assert.Equal(0, properties["width"]!["exclusiveMinimum"]!.GetValue<int>());
        Assert.Equal(0, properties["height"]!["exclusiveMinimum"]!.GetValue<int>());
        Assert.Equal(
            """[{"anyOf":[{"required":["preset"]},{"required":["width"]}]},{"dependentRequired":{"width":["height"]}},{"dependentRequired":{"height":["width"]}}]""",
            input["allOf"]!.ToJsonString());
        Assert.StartsWith("The size of a page in points", input["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(properties["width"]!["description"]?.GetValue<string>()));
    }

    [Fact]
    public void Dimensions_AreThePresetsInPortraitOrTheGivenPoints()
    {
        Assert.Equal(PageSizePresets.Dimensions(PageSizePresets.Letter), new PageSizeInput { Preset = PageSizePresets.Letter }.Dimensions());
        Assert.Equal((300d, 400.5d), new PageSizeInput { Width = 300, Height = 400.5 }.Dimensions());
    }

    [Theory]
    [InlineData(841.89, 595.28)]
    [InlineData(595.28, 841.89)]
    [InlineData(595, 842)]
    public void APresetWithItsOwnSize_ReadsAsItWasReportedInEitherOrientation(double width, double height) =>
        Assert.Equal((width, height), new PageSizeInput { Preset = PageSizePresets.A4, Width = width, Height = height }.Dimensions());

    [Fact]
    public void APresetWithAnotherPapersSize_IsRefused()
    {
        OperationInvalidException error = Assert.Throws<OperationInvalidException>(
            static () => new PageSizeInput { Preset = PageSizePresets.A4, Width = 612, Height = 792 }.Dimensions());

        Assert.Equal("pageSize names a4 but measures 612 by 792 points, which is not a4 in either orientation", error.Message);
    }

    private static string Parse(string operation)
    {
        try
        {
            TestBatch batch = TestOp.Catalog.Parse<TestBatch>($$"""{"ops":[{{operation}}]}""", TestContracts.Json);
            return JsonSerializer.Serialize(batch.Ops[0], TestContracts.Json.LocalOptions);
        }
        catch (CliException error)
        {
            return "error: " + error.Details!["reason"]!.GetValue<string>();
        }
    }
}
