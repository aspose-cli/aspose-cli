using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfFontDirectoryTests
{
    [Fact]
    public void UseFonts_AddsTheDirectoryOnlyForTheScope()
    {
        using var fixture = new PdfEngineFixture();
        string fonts = fixture.File("fonts");
        string font = FontFixtures.WriteUniqueFont(fonts);
        string input = CreateDocument(fixture, fixture.File("fixture.pdf"), font);
        var environment = new PdfFontEnvironment(fixture.Gate, ProductTestBudgets.Create<PdfModule>());

        Assert.False(FixtureAvailable(environment, input));
        using (environment.UseFonts(FontSearchProfile.Explicit([fonts])))
        {
            Assert.True(FixtureAvailable(environment, input));
        }
        Assert.False(FixtureAvailable(environment, input));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_FontsCheckAndReviewUseTheFontDirectory()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using var source = new TempDirectory();
        CreateDocument(fixture, workspace.File("fixture.pdf"), FontFixtures.WriteUniqueFont(source.Path));

        FontDirectoryContract.Verify(workspace, "fixture.pdf");
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_ConvertAndEditResolveTheFontDirectoryFont()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using var source = new TempDirectory();
        CreateDocument(fixture, workspace.File("fixture.pdf"), FontFixtures.WriteUniqueFont(source.Path));
        FontFixtures.WriteUniqueFont(workspace.File("fonts"));
        string watermark = $$"""{"ops":[{"op":"add_watermark_text","text":"Draft","font":"{{FontFixtures.UniqueFamily}}"}]}""";

        CliResult ambientConvert = workspace.Run("pdf", "convert", "fixture.pdf", "--to", "pdfa-2b", "--out", "ambient-a.pdf", "--output", "json");
        CliResult fontsConvert = workspace.Run("pdf", "convert", "fixture.pdf", "--to", "pdfa-2b", "--out", "fonts-a.pdf", "--font-dir", "fonts", "--output", "json");
        CliResult ambientEdit = workspace.Run("pdf", "edit", "fixture.pdf", "--ops", watermark, "--out", "ambient-edit.pdf", "--output", "json");
        CliResult fontsEdit = workspace.Run("pdf", "edit", "fixture.pdf", "--ops", watermark, "--out", "fonts-edit.pdf", "--font-dir", "fonts", "--output", "json");

        Assert.NotEqual(0, ambientConvert.ExitCode);
        Assert.Contains("AsposeCLIFixtureSans", ambientConvert.StdErr, StringComparison.Ordinal);
        Assert.True(fontsConvert.ExitCode == 0, fontsConvert.StdErr);
        Assert.True(FontDirectoryContract.EmbedsFixture(workspace, "fonts-a.pdf"));
        Assert.NotEqual(0, ambientEdit.ExitCode);
        Assert.Contains(FontFixtures.UniqueFamily, ambientEdit.StdErr, StringComparison.Ordinal);
        Assert.True(fontsEdit.ExitCode == 0, fontsEdit.StdErr);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_CreateFromHtmlLaysOutWithTheFontDirectory()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        File.WriteAllText(
            workspace.File("brand.html"),
            $"<html><body><p style=\"font-family:'{FontFixtures.UniqueFamily}'\">Brand text.</p></body></html>");

        FontDirectoryContract.VerifyPdfOutput(
            workspace, "create", output => ["pdf", "create", output, "--from-html", "brand.html"]);
    }

    private static bool FixtureAvailable(IFontEnvironment environment, string input) =>
        environment.CheckFonts(input, new FontCheckRequest()).Fonts
            .Single(static font => font.Name.StartsWith("AsposeCLIFixtureSans", StringComparison.Ordinal))
            .Available;

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
