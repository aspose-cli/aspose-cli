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

    [Fact]
    public void Cli_FontsCheckAndReviewUseTheFontDirectory()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        using var source = new TempDirectory();
        CreateDocument(fixture, workspace.File("fixture.pdf"), FontFixtures.WriteUniqueFont(source.Path));

        FontDirectoryContract.Verify(workspace, "fixture.pdf");
    }

    private static bool FixtureAvailable(IFontEnvironment environment, string input) =>
        environment.CheckFonts(input, new FontCheckRequest()).Fonts
            .Single(static font => font.Name.StartsWith("AsposeCLIFixtureSans", StringComparison.Ordinal))
            .Available;

    /// <summary>A page whose text uses the fixture font without embedding it.</summary>
    private static string CreateDocument(PdfEngineFixture fixture, string path, string font)
    {
        fixture.Gate.EnsureApplied();
        using var document = new Document();
        var text = new TextFragment("Fixture text drawn with a font from an explicit directory.");
        text.TextState.Font = FontRepository.OpenFont(font);
        text.TextState.Font.IsEmbedded = false;
        document.Pages.Add().Paragraphs.Add(text);
        document.Save(path);
        return path;
    }
}
