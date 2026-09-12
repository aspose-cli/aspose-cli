using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Words composition, routing, and schema invariants.</summary>
public sealed class WordsModuleTests
    : ProductContractTests<WordsModule>
{
    protected override IReadOnlyList<ResultEnvelope> CanonicalResults =>
        WordsContractSamples.Results;

    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs =>
        WordsContractSamples.Inputs;

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            WordsOpsParser.Parse("""{"ops":null}"""));
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
            WordsOps.Names.Order(StringComparer.Ordinal),
            WordsContractSamples.Ops.Ops
                .Select(static operation => operation.OpName)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PageSizes_SchemaAndRuntimeStayAligned()
    {
        ProductResourceCatalog resources =
            ProductCatalog.Build([new WordsModule()]).Resources;
        JsonNode schema = JsonNode.Parse(resources.Read("v2/words/ops"))!;
        string[] declaredSizes = schema["$defs"]!["pageSetup"]!["properties"]!
            ["size"]!["enum"]!.AsArray()
            .Select(static value => value!.GetValue<string>())
            .ToArray();
        string[] expectedSizes = ["a3", "a4", "a5", "letter", "legal"];

        Assert.Equal(expectedSizes, declaredSizes);
        foreach (string size in declaredSizes)
        {
            WordsOpsBatch batch = WordsOpsParser.Parse(
                "{\"ops\":[{\"op\":\"set_page_setup\",\"setup\":{\"size\":\""
                + size
                + "\"}}]}");
            Assert.Equal(size, Assert.IsType<SetPageSetupOp>(batch.Ops[0]).Setup.Size);
        }

        Assert.Throws<CliException>(() => WordsOpsParser.Parse(
            """{"ops":[{"op":"set_page_setup","setup":{"size":"A4"}}]}"""));
        Assert.Throws<CliException>(() => WordsOpsParser.Parse(
            """{"ops":[{"op":"set_page_setup","setup":{"size":"tabloid"}}]}"""));
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
    [InlineData("""{"op":"insert_image","at":{"block":1},"position":"after","path":"image.png","inline":false}""", """{"inline":false}""")]
    [InlineData("""{"op":"insert_toc","at":{"block":1},"position":"after","maxLevel":0}""", """{"maxLevel":0}""")]
    [InlineData("""{"op":"replace_text","find":"a","replace":"b","scope":null}""", """{"scope":null}""")]
    public void OperationFields_PreserveDefaultsAndExplicitValues(string input, string expected) =>
        AssertOperationDefaults<WordsOp>(input, expected);
}
