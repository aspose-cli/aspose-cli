using Aspose.Cli.Sdk.Rendering;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfFontDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FontlessPages_HaveEmptyFontResults(bool imageOnly)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("fontless.pdf", pages: 1,
            textPages: new HashSet<int>(),
            imagePages: imageOnly ? new HashSet<int> { 1 } : null);
        byte[] original = File.ReadAllBytes(input);
        using (var document = new Document(input))
        {
            Assert.Null(document.Pages[1].Resources.Fonts);
            Assert.Equal(imageOnly ? 1 : 0, document.Pages[1].Resources.Images?.Count ?? 0);
        }

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["fonts"] });

        Assert.Empty(info.Fonts!);
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Fact]
    public void MixedPages_RetainFontsFromTextPageAfterFontlessPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("mixed.pdf", pages: 3,
            textPages: new HashSet<int> { 3 }, imagePages: new HashSet<int> { 2 });
        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["fonts"] });

        Assert.Equal("Helvetica", Assert.Single(info.Fonts!).Name);
    }
}
