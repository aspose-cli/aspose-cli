using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Words composition, routing, and schema invariants.</summary>
public sealed class WordsModuleTests
    : ProductContractTests<WordsModule>
{
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } =
        [new(WordsOp.Catalog.SchemaId, WordsOperationSample.Batch)];

    // rule-exemptions: begin
    protected override IReadOnlyDictionary<(string Schema, string Pointer), string> Homonyms { get; } =
        new Dictionary<(string Schema, string Pointer), string>
        {
            [("v2/words/compare-result", "#/$defs/wordsRevisionSample/properties/kind")] = "A difference's kind (insertion, deletion, move or format change), not a list, break or header kind.",
            [("v2/words/inspect-result", "#/$defs/wordsStyleData/properties/kind")] = "A style's kind (paragraph, character, table or list), not a list, break or header kind.",
            [("v2/words/inspect-result", "#/$defs/wordsRevisionData/properties/kind")] = "A revision's kind (insertion, deletion, move or format change), not a list, break or header kind.",
            [("v2/words/read-result", "#/$defs/wordsBlockData/properties/kind")] = "A read block's kind says whether it is a paragraph or a table, not a list, break or header kind.",
        };
    // rule-exemptions: end

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
            """{"op":"set_text","target":{"block":1},"text":"hello"}""", "target");

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
                ["flatopc"] = ProductRoutingContract.Utf8(
                    "<?xml version=\"1.0\"?><?mso-application progid=\"Word.Document\"?>"
                    + "<pkg:package xmlns:pkg=\"http://schemas.microsoft.com/office/2006/xmlPackage\">"
                    + "<pkg:part pkg:name=\"/word/document.xml\"/></pkg:package>"),
                ["wordml"] = ProductRoutingContract.Utf8(
                    "<?xml version=\"1.0\"?><w:wordDocument xmlns:w=\"http://schemas.microsoft.com/office/word/2003/wordml\"/>"),
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
    public void PageSize_TakesTheSharedPresetsOrWidthAndHeight()
    {
        foreach (string preset in new[] { "a3", "a4", "a5", "letter", "legal", "tabloid" })
        {
            WordsOpsBatch batch = Parse($$$"""{"ops":[{"op":"set_page_setup","pageSize":{"preset":"{{{preset}}}"}}]}""");
            Assert.Equal(preset, Assert.IsType<SetPageSetupOp>(batch.Ops[0]).PageSize!.Preset);
        }

        SetPageSetupOp custom = Assert.IsType<SetPageSetupOp>(Parse(
            """{"ops":[{"op":"set_page_setup","pageSize":{"width":500,"height":700}}]}""").Ops[0]);
        Assert.Equal((500d, 700d), custom.PageSize!.Dimensions());
        Assert.Throws<CliException>(() => Parse(
            """{"ops":[{"op":"set_page_setup","pageSize":{"preset":"A4"}}]}"""));
        Assert.Throws<CliException>(() => Parse(
            """{"ops":[{"op":"set_page_setup","pageSize":{"preset":"a4","width":500}}]}"""));
        SetPageSetupOp mismatched = Assert.IsType<SetPageSetupOp>(Parse(
            """{"ops":[{"op":"set_page_setup","pageSize":{"preset":"a4","width":500,"height":700}}]}""").Ops[0]);
        Assert.Throws<OperationInvalidException>(() => mismatched.PageSize!.Dimensions());
        Assert.Throws<CliException>(() => Parse(
            """{"ops":[{"op":"set_page_setup","setup":{"size":"a4"}}]}"""));
    }

    [Fact]
    public void ProtectionTypes_ReadAsNoneOrTheProtectMode()
    {
        JsonNode schema = JsonNode.Parse(ProductCatalog.Build([new WordsModule()]).Resources.Read("v2/words/ops"))!;
        string[] modes = schema["$defs"]!["protect_document"]!["properties"]!["protection"]!["enum"]!.AsArray()
            .Select(static value => value!.GetValue<string>())
            .ToArray();

        foreach (Aspose.Words.ProtectionType type in Enum.GetValues<Aspose.Words.ProtectionType>())
        {
            string? mode = Engine.Mapping.WordsProtection.ToMode(type);
            if (type == Aspose.Words.ProtectionType.NoProtection)
            {
                Assert.Null(mode);
                continue;
            }

            Assert.NotNull(mode);

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
    [InlineData("""{"op":"set_header","paragraphs":[{"text":"Header"}]}""", """{"kind":"primary"}""")]
    [InlineData("""{"op":"set_footer","paragraphs":[{"text":"Footer"}]}""", """{"kind":"primary"}""")]
    [InlineData("""{"op":"set_page_numbers"}""", """{"location":"footer","alignment":null}""")]
    [InlineData("""{"op":"import_sections","path":"other.docx"}""", """{"formatting":"keep-source"}""")]
    [InlineData("""{"op":"refresh_fields"}""", """{"what":"all"}""")]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"bullet"}""", """{"level":1}""")]
    [InlineData("""{"op":"set_watermark","text":"DRAFT"}""", """{"faded":true}""")]
    [InlineData("""{"op":"set_watermark","text":"DRAFT","faded":false}""", """{"faded":false}""")]
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
    [InlineData("path", 0)]
    public void Watermark_OutsideTheSchemaLimitsIsAnInvalidOperation(string field, int length)
    {
        var error = Assert.Throws<CliException>(() => Parse(Watermark(field, length)));
        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void Watermark_AcceptsTheLongestTextTheSchemaAllows() =>
        Assert.Equal(200, Assert.IsType<SetWatermarkOp>(Parse(Watermark("text", 200)).Ops[0]).Text!.Length);

    private static string Watermark(string field, int length) =>
        $$"""{"ops":[{"op":"set_watermark","{{field}}":"{{new string('x', length)}}"}]}""";

    [Theory]
    [InlineData("""{"op":"format_text","target":{"block":1},"color":"red"}""")]
    [InlineData("""{"op":"format_text","target":{"block":1},"highlight":"#FFFF00"}""")]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"bullet","level":0}""")]
    [InlineData("""{"op":"insert_paragraphs","at":{"block":1},"position":"after","paragraphs":[{"text":"a","list":{"level":0}}]}""")]
    [InlineData("""{"op":"set_header","paragraphs":[{"text":"a","list":{"level":1}}]}""")]
    [InlineData("""{"op":"set_properties","custom":{"Title":"x"}}""")]
    [InlineData("""{"op":"protect_document","protection":"readOnly"}""")]
    [InlineData("""{"op":"set_page_numbers","numberStyle":"upperRoman"}""")]
    [InlineData("""{"op":"import_sections","path":"a.docx","formatting":"keepSource"}""")]
    [InlineData("""{"op":"insert_section","at":0,"position":"after"}""")]
    public void OldValueForms_AreInvalidOperations(string operation)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    [Theory]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"bullet","numberStyle":"letters-lower"}""", "number a list")]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"bullet","start":5}""", "number a list")]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"bullet","numberFormat":"(%1)"}""", "number a list")]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"number","numberFormat":"%1.%2"}""", "%2")]
    [InlineData("""{"op":"set_list","target":{"block":1},"kind":"number","numberFormat":"%1 of 50%"}""", "placeholders %1 to %9")]
    [InlineData("""{"op":"format_text","target":{"block":1},"regex":true,"bold":true}""", "true need match")]
    [InlineData("""{"op":"format_text","target":{"block":1},"caseSensitive":true,"bold":true}""", "true need match")]
    public void SettingWithoutWhatItNeeds_IsAnInvalidOperation(string operation, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NumberFormat_TakesFixedTextAndALiteralPercentSign() =>
        Parse("""{"ops":[{"op":"set_list","target":{"block":1},"kind":"number","numberFormat":"Done 100%% %%2"}]}""");

    private static WordsOpsBatch Parse(string json) =>
        WordsOp.Catalog.Parse<WordsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
