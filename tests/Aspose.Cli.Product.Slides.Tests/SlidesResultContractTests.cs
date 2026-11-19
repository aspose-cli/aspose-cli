using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Real CLI runs whose JSON the workspace checks against the generated result schemas, over a
/// deck that holds media, notes, comments, sections and document properties.
/// </summary>
public sealed class SlidesResultContractTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Read_ReportsTheObjectsOfEveryKindTheDeckHolds()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode read = _workspace.Run("slides", "read", "deck.pptx", "--output-format", "json").Json();

        string[] kinds = [.. read["objects"]!.AsArray().Select(static item => item!["kind"]!.GetValue<string>()).Distinct()];
        Assert.Contains("section", kinds);
        Assert.Contains("image", kinds);
        Assert.Contains("notes", kinds);
        Assert.Contains("paragraph", kinds);
    }

    [Fact]
    public void Render_ReportsTheResolutionOrTheWidth()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode byDpi = _workspace.Run(
            "slides", "render", "deck.pptx", "--slides", "1-2", "--dpi", "36", "--out", "dpi.png", "--output-format", "json").Json();
        JsonNode byWidth = _workspace.Run(
            "slides", "render", "deck.pptx", "--slides", "1", "--width", "64", "--out", "width.png", "--output-format", "json").Json();

        Assert.Equal(36, byDpi["dpi"]!.GetValue<int>());
        Assert.Equal(2, byDpi["outputs"]!.AsArray().Count);
        Assert.Null(byWidth["dpi"]);
        Assert.Equal(64, byWidth["width"]!.GetValue<int>());
    }

    [Fact]
    public void Extract_ReportsEachWrittenFile()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode extracted = _workspace.Run(
            "slides", "extract", "deck.pptx", "--what", "media", "--out-dir", "media", "--output-format", "json").Json();

        Assert.Equal("media", extracted["what"]!.GetValue<string>());
        Assert.Null(extracted["items"]);
        JsonNode item = extracted["outputs"]![0]!;
        // Each file names the media item it holds beside its file fields.
        Assert.Equal(
            new[] { "path", "format", "sizeBytes", "index" }.Order(StringComparer.Ordinal),
            item.AsObject().Select(static pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal(1, item["index"]!.GetValue<int>());
        string path = item["path"]!.GetValue<string>();
        Assert.Equal(Path.GetExtension(path).TrimStart('.'), item["format"]!.GetValue<string>());
        Assert.Equal(new FileInfo(path).Length, item["sizeBytes"]!.GetValue<long>());
    }

    private static void CreateDeck(string path)
    {
        using var presentation = new Presentation();
        ISlide first = presentation.Slides[0];
        first.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 30, 600, 70).TextFrame.Text = "Quarterly review";
        IPPImage image = presentation.Images.AddImage(SlidesEngineFixture.Png(16));
        first.Shapes.AddPictureFrame(ShapeType.Rectangle, 40, 120, 64, 64, image);
        first.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Speaker notes.";
        ICommentAuthor author = presentation.CommentAuthors.AddAuthor("Reviewer", "R");
        author.Comments.AddComment("Check the numbers.", first, new System.Drawing.PointF(10, 10), DateTime.UtcNow);
        ISlide second = presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        second.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 30, 600, 70).TextFrame.Text = "Results";
        presentation.Sections.AddSection("Results", second);
        presentation.DocumentProperties.Author = "Finance";
        presentation.Save(path, SaveFormat.Pptx);
    }
}
