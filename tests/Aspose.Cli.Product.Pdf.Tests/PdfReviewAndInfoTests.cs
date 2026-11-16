using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Page geometry and form fields reported by inspect.</summary>
public sealed class PdfReviewAndInfoTests
{
    [Theory]
    [InlineData(Rotation.None, 0)]
    [InlineData(Rotation.on90, 90)]
    [InlineData(Rotation.on180, 180)]
    [InlineData(Rotation.on270, 270)]
    public void InspectPreview_ReportsRotationInDegrees(Rotation rotation, int degrees)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument("rotated.pdf", pages: 1);
        using (var document = new Document(path))
        {
            document.Pages[1].Rotate = rotation;
            document.Save(path);
        }

        PdfPageInfo page = Assert.Single(PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, IncludePreview = true }).Pages!);

        Assert.Equal(degrees, page.Rotation);
    }

    /// <summary>Inspect counts form fields by name, so a radio group is one field.</summary>
    [Fact]
    public void Inspect_CountsARadioGroupAsOneField()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("form.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new Aspose.Pdf.Forms.TextBoxField(page, new Rectangle(72, 700, 300, 720)) { PartialName = "company" });
            var color = new Aspose.Pdf.Forms.RadioButtonField(page) { PartialName = "color" };
            foreach ((string option, double x) in new[] { ("Red", 72.0), ("Green", 112.0), ("Blue", 152.0) })
            {
                color.Add(new Aspose.Pdf.Forms.RadioButtonOptionField(page, new Rectangle(x, 620, x + 20, 640)) { OptionName = option });
            }
            document.Form.Add(color);
            document.Save(input);
        }

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["forms"] });

        Assert.Equal(2, info.Forms!.FieldCount);
        Assert.Equal(2, fixture.Forms(input).Fields.Count);
    }
}
