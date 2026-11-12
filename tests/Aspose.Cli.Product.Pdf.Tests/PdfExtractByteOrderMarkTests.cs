using System.Text;
using System.Xml.Linq;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What <c>pdf extract</c> derives from a PDF is UTF-8 without a byte order mark, as every text
/// output is, while an attachment keeps the bytes the PDF stores, a mark it starts with included.
/// </summary>
public sealed class PdfExtractByteOrderMarkTests
{
    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];

    [Fact]
    public void ExtractFormsAsXfdf_WritesTheEnginesExportWithoutItsByteOrderMark()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Customer"));
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = "Customer", Value = "季度 Initial" });
            document.Save(input);
        }

        byte[] engine;
        using (var document = new Document(input))
        using (var data = new MemoryStream())
        {
            new Aspose.Pdf.Facades.Form(document).ExportXfdf(data);
            engine = data.ToArray();
        }

        PdfExtractResult exported = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            What = PdfExtractKinds.Forms,
            Output = new ResolvedDirectory(fixture.File("form-data")),
            FormFormat = PdfFormats.FormData.Single(static candidate => candidate.Id == "xfdf"),
        });

        PdfExtractOutput xfdf = Assert.Single(exported.Outputs);
        byte[] bytes = File.ReadAllBytes(xfdf.Path);
        Assert.Equal(Mark, engine[..Mark.Length]);
        Assert.Equal(engine[Mark.Length..], bytes);
        Assert.Equal(bytes.LongLength, xfdf.SizeBytes);
        XDocument fields = XDocument.Parse(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes));
        Assert.Equal("utf-8", fields.Declaration?.Encoding, ignoreCase: true);
        Assert.Contains("季度 Initial", fields.Root!.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractAttachments_KeepsAByteOrderMarkTheAttachmentStartsWith()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("report.pdf");
        byte[] content = [.. Mark, .. "region,10\n"u8];
        using (var document = new Document())
        {
            document.Pages.Add();
            document.EmbeddedFiles.Add("data.csv", new FileSpecification(new MemoryStream(content), "data.csv", "data.csv") { UnicodeName = "data.csv" });
            document.Save(input);
        }

        PdfExtractResult extracted = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            What = PdfExtractKinds.Attachments,
            Output = new ResolvedDirectory(fixture.File("files")),
        });

        PdfExtractOutput attachment = Assert.Single(extracted.Outputs);
        Assert.Equal(content, File.ReadAllBytes(attachment.Path));
        Assert.Equal(content.LongLength, attachment.SizeBytes);
    }
}
