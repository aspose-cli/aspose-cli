using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

[Collection(ProcessFontSourcesCollection.Name)]
public sealed class PdfFontDirectoryTests
{
    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_ConvertAndEditResolveTheFontDirectoryFont()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using var source = new TempDirectory();
        CreateDocument(fixture, workspace.File("fixture.pdf"), FontFixtures.WriteUniqueFont(source.Path));
        FontFixtures.WriteUniqueFont(workspace.File("fonts"));
        string watermark = $$$"""{"ops":[{"do":"stamp_text","args":{"page":"page/1","value":"Draft","textState.font":"{{{FontFixtures.UniqueFamily}}}"}}]}""";

        CliResult ambientConvert = workspace.Run("pdf", "convert", "fixture.pdf", "--to", "pdfa-2b", "--convert-error-action", "Delete", "--out", "ambient-a.pdf", "--output-format", "json");
        CliResult fontsConvert = workspace.Run("pdf", "convert", "fixture.pdf", "--to", "pdfa-2b", "--convert-error-action", "Delete", "--out", "fonts-a.pdf", "--font-dir", "fonts", "--output-format", "json");
        CliResult ambientEdit = workspace.Run("pdf", "edit", "fixture.pdf", "--ops", watermark, "--out", "ambient-edit.pdf", "--output-format", "json");
        CliResult fontsEdit = workspace.Run("pdf", "edit", "fixture.pdf", "--ops", watermark, "--out", "fonts-edit.pdf", "--font-dir", "fonts", "--output-format", "json");

        Assert.True(ambientConvert.ExitCode == 0, ambientConvert.StdErr);
        Assert.False(FontDirectoryContract.EmbedsFixture(workspace, "ambient-a.pdf"));
        Assert.True(fontsConvert.ExitCode == 0, fontsConvert.StdErr);
        Assert.True(FontDirectoryContract.EmbedsFixture(workspace, "fonts-a.pdf"));
        Assert.NotEqual(0, ambientEdit.ExitCode);
        Assert.Contains(FontFixtures.UniqueFamily, ambientEdit.StdErr, StringComparison.Ordinal);
        Assert.True(fontsEdit.ExitCode == 0, fontsEdit.StdErr);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_ConvertFromHtmlLaysOutWithTheFontDirectory()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        File.WriteAllText(
            workspace.File("brand.html"),
            $"<html><body><p style=\"font-family:'{FontFixtures.UniqueFamily}'\">Brand text.</p></body></html>");

        FontDirectoryContract.VerifyPdfOutput(
            workspace, "convert", output => ["pdf", "convert", "brand.html", "--out", output]);
    }

    /// <summary>A page whose text uses the fixture font without embedding it.</summary>
    private static string CreateDocument(PdfEngineFixture fixture, string path, string font)
    {
        using var document = new Document();
        var text = new TextFragment("Fixture text drawn with a font from an explicit directory.");
        text.TextState.Font = FontRepository.OpenFont(font);
        text.TextState.Font.IsEmbedded = false;
        document.Pages.Add().Paragraphs.Add(text);
        document.Save(path);
        return path;
    }
}
