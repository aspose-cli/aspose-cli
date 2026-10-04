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
    [InlineData("""{"op":"insert_blank_page","at":1,"size":"LETTER"}""")]
    [InlineData("""{"op":"set_page_size","pages":"1","size":"a4"}""")]
    [InlineData("""{"op":"crop_pages","pages":"1","rect":{"x":-1,"y":0,"width":10,"height":10}}""")]
    [InlineData("""{"op":"redact_area","page":1,"rect":{"x":0,"y":-5,"width":10,"height":10}}""")]
    [InlineData("""{"op":"add_link","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"ftp://example.com/a"}""")]
    [InlineData("""{"op":"add_link","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"file:///etc/passwd"}""")]
    [InlineData("""{"op":"add_link","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"javascript:alert(1)"}""")]
    [InlineData("""{"op":"redact_text","pattern":""}""")]
    [InlineData("""{"op":"rotate_pages","pages":"0","angle":90}""")]
    [InlineData("""{"op":"flatten_forms","fields":[""]}""")]
    [InlineData("""{"op":"flatten_forms","fields":[]}""")]
    [InlineData("""{"op":"flatten_forms","all":true}""")]
    [InlineData("""{"op":"add_attachment","path":" "}""")]
    [InlineData("""{"op":"add_attachment","path":"scan.png","mimeType":"png"}""")]
    [InlineData("""{"op":"add_attachment","path":"scan.png","mimeType":"image/png; q=1"}""")]
    [InlineData("""{"op":"set_metadata","custom":{"author":null}}""")]
    [InlineData("""{"op":"add_bookmark","title":"Intro","page":1,"parent":""}""")]
    [InlineData("""{"op":"add_bookmark","title":"Intro","page":1,"parent":"1/"}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":[""]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["0"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["01"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1/"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["/1"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["a"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1//2"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1/0"]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1"],"all":true}""")]
    [InlineData("""{"op":"delete_bookmarks"}""")]
    [InlineData("""{"op":"delete_bookmarks","path":"Intro"}""")]
    [InlineData("""{"op":"delete_bookmarks","index":"1"}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":[]}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":"1"}""")]
    public void ParserAndSchema_RejectTheSameInvalidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        CliException error = Assert.Throws<CliException>(() => Parse(batch));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.False(IsSchemaValid(batch), "The schema accepted an operation the parser rejects.");
    }

    [Theory]
    [InlineData("""{"op":"insert_blank_page","at":1,"size":"Letter"}""")]
    [InlineData("""{"op":"crop_pages","pages":"1","rect":{"x":0,"y":0,"width":10,"height":10}}""")]
    [InlineData("""{"op":"add_link","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"https://example.com/a"}""")]
    [InlineData("""{"op":"add_link","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"mailto:team@example.com"}""")]
    [InlineData("""{"op":"flatten_forms"}""")]
    [InlineData("""{"op":"delete_bookmarks","indexes":["2/1"],"all":false}""")]
    [InlineData("""{"op":"delete_bookmarks","all":true}""")]
    [InlineData("""{"op":"add_bookmark","title":"I/O","page":1,"parent":"10/2"}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
    }

    [Theory]
    [InlineData("""["2","2"]""", "indexes must not repeat \"2\"")]
    [InlineData("""["2/1","2"]""", "indexes must not list \"2/1\" with \"2\": deleting bookmark 2 already deletes its children")]
    [InlineData("""["1","3/2/1","3"]""", "indexes must not list \"3/2/1\" with \"3\": deleting bookmark 3 already deletes its children")]
    public void DeleteBookmarks_RefusesAnIndexAnotherListedIndexAlreadyDeletes(string indexes, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            $$"""{"ops":[{"op":"delete_bookmarks","indexes":{{indexes}}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteBookmarks_AcceptsSiblingsAndLookalikePrefixes() =>
        Assert.Equal(
            ["1", "10", "2/1", "2/10"],
            Assert.IsType<DeleteBookmarksOp>(Assert.Single(Parse(
                """{"ops":[{"op":"delete_bookmarks","indexes":["1","10","2/1","2/10"]}]}""").Ops)).Indexes!);

    [Theory]
    [InlineData("""{"op":"delete_bookmarks","indexes":["1","Results"]}""", "indexes[1]")]
    [InlineData("""{"op":"add_bookmark","title":"Intro","page":1,"parent":"Results"}""", "parent")]
    public void BookmarkIndexes_ExplainWhereAnIndexComesFrom(string operation, string field)
    {
        CliException error = Assert.Throws<CliException>(() => Parse($$"""{"ops":[{{operation}}]}"""));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains(
            $"{field} must be a bookmark index as pdf inspect --detail outline reports it",
            error.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a*")]
    [InlineData("x?")]
    public void RedactText_RejectsAnExpressionThatMatchesTheEmptyString(string pattern)
    {
        CliException error = Assert.Throws<CliException>(() => Parse(
            $$"""{"ops":[{"op":"redact_text","regex":true,"pattern":{{JsonSerializer.Serialize(pattern)}}}]}"""));

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

        PdfFormResult form = fixture.Engine.ReadForm(path, new PdfFormReadRequest());

        Assert.Equal(
            [("agree", "checkbox"), ("choice", "combobox"), ("date", "text"), ("name", "text")],
            form.Fields.Select(static field => (field.Name, field.Type)));
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

        PdfFormResult form = fixture.Engine.ReadForm(path, new PdfFormReadRequest());

        Assert.Equal(
            [("first", 72d, height - 640, 20d, 20d), ("second", 300d, height - 516, 16d, 16d)],
            form.Fields.Select(static field => (field.Name, field.Rect!.X, field.Rect.Y, field.Rect.Width, field.Rect.Height)));
    }

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    private static PdfOpsBatch Parse(string json) =>
        PdfOp.Catalog.Parse<PdfOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
