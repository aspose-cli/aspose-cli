using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

[Collection(ProcessFontSourcesCollection.Name)]
public sealed class SlidesFontDirectoryTests
{
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
