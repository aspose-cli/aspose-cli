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
    [InlineData("""{"op":"redact_text","pattern":""}""")]
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
    [InlineData("""{"op":"add_link","page":1,"rect":{"x":0,"y":0,"width":10,"height":10},"url":"mailto:team@example.com"}""")]
    public void ParserAndSchema_AcceptTheSameValidOperation(string operation)
    {
        string batch = $$"""{"ops":[{{operation}}]}""";

        _ = Parse(batch);

        Assert.True(IsSchemaValid(batch), "The schema rejected an operation the parser accepts.");
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
        fixture.Gate.EnsureApplied();
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

    private static bool IsSchemaValid(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Schema.Value.Evaluate(document.RootElement).IsValid;
    }

    private static PdfOpsBatch Parse(string json) =>
        PdfOps.Catalog.Parse<PdfOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
