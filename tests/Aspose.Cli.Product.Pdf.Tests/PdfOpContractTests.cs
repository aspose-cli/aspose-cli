using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>The PDF ops schema and the op rules give the same answer, and form fields use product names.</summary>
public sealed class PdfOpContractTests
{
    private static readonly Lazy<JsonSchema> Schema = new(static () => JsonSchema.FromText(
        ProductCatalog.Build([new PdfModule()]).Resources.Read("v2/pdf/ops"),
        SchemaTestRegistry.CreateOptions()));

    [Theory]
    [InlineData("""{"op":"insert_page","at":1,"pageSize":{"preset":"LETTER"}}""")]
    [InlineData("""{"op":"set_page_size","pages":"1","pageSize":{"preset":"A4"}}""")]
    [InlineData("""{"op":"set_page_size","pages":"1","pageSize":{"width":300}}""")]
    [InlineData("""{"op":"set_page_size","pages":"1","pageSize":{"preset":"a4","height":400}}""")]
    [InlineData("""{"op":"set_page_size","pages":"1","size":"a4"}""")]
    [InlineData("""{"op":"crop_pages","pages":"1","rect":{"x":-1,"y":0,"width":10,"height":10}}""")]
    [InlineData("""{"op":"redact_area","page":1,"rect":{"x":0,"y":-5,"width":10,"height":10}}""")]
    [InlineData("""{"op":"add_hyperlink","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"ftp://example.com/a"}""")]
    [InlineData("""{"op":"add_hyperlink","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"file:///etc/passwd"}""")]
    [InlineData("""{"op":"add_hyperlink","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"javascript:alert(1)"}""")]
    [InlineData("""{"op":"redact_text","find":""}""")]
    [InlineData("""{"op":"rotate_pages","pages":"0","rotation":90}""")]
    [InlineData("""{"op":"flatten_forms","fields":[""]}""")]
    [InlineData("""{"op":"flatten_forms","fields":[]}""")]
    [InlineData("""{"op":"flatten_forms","all":true}""")]
    [InlineData("""{"op":"add_attachment","path":" "}""")]
    [InlineData("""{"op":"add_attachment","path":"scan.png","mimeType":"png"}""")]
    [InlineData("""{"op":"add_attachment","path":"scan.png","mimeType":"image/png; q=1"}""")]
    [InlineData("""{"op":"set_properties","custom":{"author":null}}""")]
    [InlineData("""{"op":"add_bookmark","title":"Intro","page":1,"parent":""}""")]
    [InlineData("""{"op":"add_bookmark","title":"Intro","page":1,"parent":"1/"}""")]
    [InlineData("""{"op":"add_bookmark","title":"Q1/Q2","page":1}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":[""]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["0"]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["01"]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["1/"]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["/1"]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["a"]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["1//2"]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["1/0"]}""")]
    [InlineData("""{"op":"delete_bookmarks","all":true}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1"]}""")]
    [InlineData("""{"op":"delete_bookmarks","path":"Intro"}""")]
    [InlineData("""{"op":"delete_bookmarks","index":"1"}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":[]}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":"1"}""")]
    [InlineData("""{"op":"add_watermark_image","path":"image.png","scalePercent":12.5}""")]
    [InlineData("""{"op":"add_watermark_image","path":"image.png","scalePercent":0}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"insert_page","at":1,"pageSize":{"preset":"letter"}}""")]
    [InlineData("""{"op":"crop_pages","pages":"1","rect":{"x":0,"y":0,"width":10,"height":10}}""")]
    [InlineData("""{"op":"add_hyperlink","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"https://example.com/a"}""")]
    [InlineData("""{"op":"add_hyperlink","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"mailto:team@example.com"}""")]
    [InlineData("""{"op":"flatten_forms"}""")]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["2/1"]}""")]
    [InlineData("""{"op":"delete_bookmarks"}""")]
    [InlineData("""{"op":"add_bookmark","title":"Inputs and outputs","page":1,"parent":"10/2"}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
    }

    [Fact]
    public void WatermarkImage_RefusesAFractionalScalePercentAsAWholeNumberPercentage()
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            """{"ops":[{"op":"add_watermark_image","path":"image.png","scalePercent":12.5}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("scalePercent must be a whole number", error.Message, StringComparison.Ordinal);
        Assert.Contains("whole-number percentage", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""["2","2"]""", "bookmarks must not repeat \"2\"")]
    [InlineData("""["2/1","2"]""", "bookmarks must not list \"2/1\" with \"2\": deleting bookmark 2 already deletes its children")]
    [InlineData("""["1","3/2/1","3"]""", "bookmarks must not list \"3/2/1\" with \"3\": deleting bookmark 3 already deletes its children")]
    public void DeleteBookmarks_RefusesABookmarkAnotherListedBookmarkAlreadyDeletes(string bookmarks, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            $$"""{"ops":[{"op":"delete_bookmarks","bookmarks":{{bookmarks}}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteBookmarks_AcceptsSiblingsAndLookalikePrefixes() =>
        Assert.Equal(
            ["1", "10", "2/1", "2/10"],
            Assert.IsType<DeleteBookmarksOp>(Assert.Single(Parse(
                """{"ops":[{"op":"delete_bookmarks","bookmarks":["1","10","2/1","2/10"]}]}""").Ops)).Bookmarks!);

    [Theory]
    [InlineData("""{"op":"delete_bookmarks","bookmarks":["1","Results"]}""", "bookmarks[1]")]
    [InlineData("""{"op":"add_bookmark","title":"Intro","page":1,"parent":"Results"}""", "parent")]
    public void BookmarkAddresses_ExplainWhereAnAddressComesFrom(string operation, string field)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(
            $"{field} must be a bookmark address as 'pdf inspect --detail outline' reports it",
            error.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a*")]
    [InlineData("x?")]
    public void RedactText_RejectsAnExpressionThatMatchesTheEmptyString(string pattern)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            $$"""{"ops":[{"op":"redact_text","regex":true,"find":{{JsonSerializer.Serialize(pattern)}}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("empty string", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadForm_ReportsProductFieldTypes()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 700, 200, 720)) { PartialName = "name" });
            document.Form.Add(new DateField(page, new Rectangle(72, 660, 200, 680)) { PartialName = "date" });
            document.Form.Add(new CheckboxField(page, new Rectangle(72, 620, 92, 640)) { PartialName = "agree" });
            document.Form.Add(new ComboBoxField(page, new Rectangle(72, 580, 200, 600)) { PartialName = "choice" });
            document.Save(path);
        }

        PdfFormInfo form = fixture.Forms(path);

        Assert.Equal(
            [("agree", "checkbox"), ("choice", "combobox"), ("date", "text"), ("name", "text")],
            form.Fields.Select(static field => (field.Name, field.Kind)));
    }

    [Fact]
    public void ReadForm_GivesTheRectangleOfEachFieldFromTheTopLeftOfItsPage()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("boxes.pdf");
        double height;
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            height = page.Rect.Height;
            document.Form.Add(new CheckboxField(page, new Rectangle(72, 620, 92, 640)) { PartialName = "first" });
            document.Form.Add(new CheckboxField(page, new Rectangle(300, 500, 316, 516)) { PartialName = "second" });
            document.Save(path);
        }

        PdfFormInfo form = fixture.Forms(path);

        Assert.Equal(
            [("first", 72d, height - 640, 20d, 20d), ("second", 300d, height - 516, 16d, 16d)],
            form.Fields.Select(static field => (field.Name, field.Rect!.X, field.Rect.Y, field.Rect.Width, field.Rect.Height)));
    }

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    [Theory]
    [InlineData("add_link", "add_hyperlink")]
    [InlineData("decrypt", "decrypt_document")]
    [InlineData("encrypt", "encrypt_document")]
    [InlineData("optimize", "optimize_document")]
    [InlineData("insert_blank_page", "insert_page")]
    [InlineData("insert_pages_from", "import_pages")]
    [InlineData("remove_attachment", "delete_attachment")]
    [InlineData("remove_metadata", "clear_properties")]
    [InlineData("set_metadata", "set_properties")]
    public void RenamedOperation_RefusesItsOldNameAndSuggestsTheNewOne(string old, string renamed)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{"op":"{{old}}"}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(renamed, error.Details!["suggestions"]!.AsArray()[0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"op":"rotate_pages","pages":"1","angle":90}""", "rotation")]
    [InlineData("""{"op":"set_page_size","pages":"1","size":"a4"}""", "pageSize")]
    [InlineData("""{"op":"insert_page","at":1,"size":"a4"}""", "pageSize")]
    [InlineData("""{"op":"add_watermark_text","text":"DRAFT","size":12}""", "fontSize")]
    [InlineData("""{"op":"add_page_numbers","format":"{n}"}""", "text")]
    [InlineData("""{"op":"add_header_text","text":"Head","position":"top-left"}""", "placement")]
    [InlineData("""{"op":"add_footer_text","text":"Foot","position":"bottom-left"}""", "placement")]
    [InlineData("""{"op":"add_page_numbers","position":"bottom-left"}""", "placement")]
    [InlineData("""{"op":"import_pages","path":"other.pdf","at":1,"passwordEnv":"SOURCE"}""", "sourcePasswordEnv")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1"]}""", "bookmarks")]
    [InlineData("""{"op":"redact_text","pattern":"secret"}""", "find")]
    [InlineData("""{"op":"set_page_labels","ranges":[{"startPage":1,"style":"decimal"}]}""", "numberStyle")]
    public void RenamedField_RefusesItsOldNameAndSuggestsTheNewOne(string operation, string renamed)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal(renamed, error.Details!["suggestions"]!.AsArray()[0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("Title", "Set it with the title field instead.")]
    [InlineData("AUTHOR", "Set it with the author field instead.")]
    [InlineData("producer", "drop it from custom")]
    [InlineData("modificationDate", "drop it from custom")]
    [InlineData("info:Department", "without the info: or xmp: prefix")]
    [InlineData("XMP:dc:title", "without the info: or xmp: prefix")]
    public void SetProperties_RefusesACustomKeyThatAStandardEntryOrAReportedNameTakes(string key, string hint)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            $$$"""{"ops":[{"op":"set_properties","custom":{"{{{key}}}":"value"}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(hint, error.Hint, StringComparison.Ordinal);
    }

    private static PdfOpsBatch Parse(string json) =>
        PdfOp.Catalog.Parse<PdfOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
