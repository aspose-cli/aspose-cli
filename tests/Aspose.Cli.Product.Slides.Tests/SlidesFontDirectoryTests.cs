using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

[Collection(ProcessFontSourcesCollection.Name)]
public sealed class SlidesFontDirectoryTests
{
    [Fact]
    public void UseFonts_AddsTheDirectoryOnlyForTheScope()
    {
        using var fixture = new SlidesEngineFixture();
        string fonts = fixture.File("fonts");
        FontFixtures.WriteUniqueFont(fonts);
        string input = CreatePresentation(fixture, fixture.File("fixture.pptx"));
        var environment = new SlidesFontEnvironment(fixture.Gate, ProductTestBudgets.Create<SlidesModule>());

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
        using var fixture = new SlidesEngineFixture();
        using var workspace = new TempWorkspace();
        CreatePresentation(fixture, workspace.File("fixture.pptx"));

        FontDirectoryContract.Verify(workspace, "fixture.pptx");
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Cli_ConvertLaysOutWithTheFontDirectory()
    {
        using var fixture = new SlidesEngineFixture();
        using var workspace = new TempWorkspace();
        CreatePresentation(fixture, workspace.File("fixture.pptx"));

        FontDirectoryContract.VerifyPdfOutput(
            workspace, "convert", output => ["slides", "convert", "fixture.pptx", "--to", "pdf", "--out", output]);
    }

    private static bool FixtureAvailable(IFontEnvironment environment, string input) =>
        environment.CheckFonts(input, new FontCheckRequest()).Fonts
            .Single(static font => font.Name == FontFixtures.UniqueFamily)
            .Available;

    private static string CreatePresentation(SlidesEngineFixture fixture, string path)
    {
        using var presentation = new Presentation();
        IAutoShape shape = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80);
        shape.TextFrame.Text = "Fixture text drawn with a font from an explicit directory.";
        shape.TextFrame.Paragraphs[0].Portions[0].PortionFormat.LatinFont = new FontData(FontFixtures.UniqueFamily);
        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }
}
