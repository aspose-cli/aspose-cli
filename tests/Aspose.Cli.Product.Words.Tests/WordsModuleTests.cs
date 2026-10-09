using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Words composition, routing, and schema invariants.</summary>
public sealed class WordsModuleTests
    : ProductContractTests<WordsModule>
{
    // Result schemas are generated from the result records, and real CLI runs validate every
    // result type against them (WordsResultSchemaCoverageTests), so no hand-written result
    // samples remain.
    protected override IReadOnlyList<ResultEnvelope> CanonicalResults => [];

    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } =
        [new(ResultEnvelope.SchemaUri("words", "ops"), WordsOperationSample.Batch)];

    protected override IReadOnlyDictionary<string, string> Homonyms { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kind"] = "An extracted item's kind names what was extracted, not a list, break or header kind.",
        };

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            Parse("""{"ops":null}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void OperationObjects_RejectUnknownNestedFields() =>
        AssertOperationObjectIsStrict<WordsOp>(
            """{"op":"set_text","at":{"block":1},"text":"hello"}""", "at");

    [Fact]
    public Task EveryDefaultInputFormatHasPositiveRoutingEvidence() =>
        ProductRoutingContract.AssertPositiveRoutesAsync<WordsModule>(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["doc"] = ProductRoutingContract.CompoundFile(),
                ["dot"] = ProductRoutingContract.CompoundFile(),
                ["docx"] = OpenXml(),
                ["docm"] = OpenXml(),
                ["dotx"] = OpenXml(),
                ["dotm"] = OpenXml(),
                ["rtf"] = ProductRoutingContract.Utf8(@"{\rtf1\ansi sample}"),
                ["odt"] = OpenDocument(),
                ["ott"] = OpenDocument(),
                ["txt"] = ProductRoutingContract.Utf8("plain text\n"),
                ["md"] = ProductRoutingContract.Utf8("# Markdown\n"),
                ["epub"] = ProductRoutingContract.ZipMarker(
                    "META-INF/container.xml"),
                ["mobi"] = ProductRoutingContract.At(
                    60,
                    ProductRoutingContract.Utf8("BOOKMOBI")),
                ["azw3"] = ProductRoutingContract.At(
                    60,
                    ProductRoutingContract.Utf8("BOOKMOBI")),
                ["chm"] = ProductRoutingContract.Utf8("ITSF"),
            });

    [Fact]
    public void CanonicalOps_CoverEveryRegisteredOperation()
    {
        Assert.Equal(
            WordsOp.Catalog.Names.Order(StringComparer.Ordinal),
            WordsOperationSample.Batch.Ops
                .Select(WordsOp.Catalog.NameOf)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PageSizes_SchemaAndRuntimeStayAligned()
    {
        ProductResourceCatalog resources =
            ProductCatalog.Build([new WordsModule()]).Resources;
        JsonNode schema = JsonNode.Parse(resources.Read("v2/words/ops"))!;
        string[] declaredSizes = schema["$defs"]!["pageSetupInput"]!["properties"]!
            ["size"]!["enum"]!.AsArray()
            .Select(static value => value!.GetValue<string>())
            .ToArray();
        string[] expectedSizes = ["a3", "a4", "a5", "letter", "legal"];

        Assert.Equal(expectedSizes, declaredSizes);
        foreach (string size in declaredSizes)
        {
            WordsOpsBatch batch = Parse(
                "{\"ops\":[{\"op\":\"set_page_setup\",\"setup\":{\"size\":\""
                + size
                + "\"}}]}");
            Assert.Equal(size, Assert.IsType<SetPageSetupOp>(batch.Ops[0]).Setup.Size);
        }

        Assert.Throws<CliException>(() => Parse(
            """{"ops":[{"op":"set_page_setup","setup":{"size":"A4"}}]}"""));
        Assert.Throws<CliException>(() => Parse(
            """{"ops":[{"op":"set_page_setup","setup":{"size":"tabloid"}}]}"""));
    }

    [Fact]
    public void ProtectionTypes_ReadAsNoneOrTheProtectMode()
    {
        JsonNode schema = JsonNode.Parse(ProductCatalog.Build([new WordsModule()]).Resources.Read("v2/words/ops"))!;
        string[] modes = schema["$defs"]!["protect"]!["properties"]!["mode"]!["enum"]!.AsArray()
            .Select(static value => value!.GetValue<string>())
            .ToArray();

        foreach (Aspose.Words.ProtectionType type in Enum.GetValues<Aspose.Words.ProtectionType>())
        {
            string mode = Engine.Mapping.WordsProtection.ToMode(type);
            if (type == Aspose.Words.ProtectionType.NoProtection)
            {
                Assert.Equal("none", mode);
                continue;
            }

            Assert.Contains(mode, modes);
            Assert.Equal(type, Engine.Mapping.WordsProtection.FromMode(mode));
        }
    }

    private static byte[] OpenXml() => ProductRoutingContract.ZipMarker(
        "word/document.xml");

    private static byte[] OpenDocument() => ProductRoutingContract.ZipMarker(
        "application/vnd.oasis.opendocument.text");

    [Theory]
    [InlineData("""{"op":"replace_text","find":"a","replace":"b"}""", """{"scope":"body"}""")]
    [InlineData("""{"op":"insert_image","at":{"block":1},"position":"after","path":"image.png"}""", """{"inline":true}""")]
    [InlineData("""{"op":"insert_toc","at":{"block":1},"position":"after"}""", """{"maxLevel":3}""")]
    [InlineData("""{"op":"add_section"}""", """{"position":"end"}""")]
    [InlineData("""{"op":"set_header","paragraphs":["Header"]}""", """{"kind":"primary"}""")]
    [InlineData("""{"op":"set_footer","paragraphs":["Footer"]}""", """{"kind":"primary"}""")]
    [InlineData("""{"op":"set_page_numbers"}""", """{"location":"footer","alignment":"center"}""")]
    [InlineData("""{"op":"append_document","path":"other.docx"}""", """{"importFormatMode":"keepSource"}""")]
    [InlineData("""{"op":"update_fields"}""", """{"what":"all"}""")]
    [InlineData("""{"op":"add_watermark","text":"DRAFT"}""", """{"faded":true}""")]
    [InlineData("""{"op":"add_watermark","text":"DRAFT","faded":false}""", """{"faded":false}""")]
    [InlineData("""{"op":"insert_image","at":{"block":1},"position":"after","path":"image.png","inline":false}""", """{"inline":false}""")]
    [InlineData("""{"op":"insert_toc","at":{"block":1},"position":"after","maxLevel":0}""", """{"maxLevel":0}""")]
    public void OperationFields_PreserveDefaultsAndExplicitValues(string input, string expected) =>
        AssertOperationDefaults<WordsOp>(input, expected);

    [Fact]
    public void ReplaceTextScope_RejectsExplicitNullAsAnInvalidOperation()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            Parse("""{"ops":[{"op":"replace_text","find":"a","replace":"b","scope":null}]}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Theory]
    [InlineData("text", 0)]
    [InlineData("text", 201)]
    [InlineData("imagePath", 0)]
    public void Watermark_OutsideTheSchemaLimitsIsAnInvalidOperation(string field, int length)
    {
        var error = Assert.Throws<CliException>(() => Parse(Watermark(field, length)));
        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void Watermark_AcceptsTheLongestTextTheSchemaAllows() =>
        Assert.Equal(200, Assert.IsType<AddWatermarkOp>(Parse(Watermark("text", 200)).Ops[0]).Text!.Length);

    private static string Watermark(string field, int length) =>
        $$"""{"ops":[{"op":"add_watermark","{{field}}":"{{new string('x', length)}}"}]}""";

    private static WordsOpsBatch Parse(string json) =>
        WordsOp.Catalog.Parse<WordsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
