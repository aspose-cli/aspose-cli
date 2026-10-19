using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Real CLI runs whose JSON the workspace checks against the generated result schemas, over a
/// deck that fills the optional parts of each result: media, notes, comments and sections.
/// </summary>
public sealed class SlidesResultContractTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void InspectWithEveryDetail_ReportsEveryInventory()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode info = _workspace.Run(
            "slides", "inspect", "deck.pptx", "--sample",
            "--detail", "comments", "--detail", "fonts", "--detail", "layouts", "--detail", "masters",
            "--detail", "media", "--detail", "notes", "--detail", "properties", "--detail", "sections",
            "--output-format", "json").Json();

        Assert.Contains(info["sections"]!.AsArray(), static section => section!["name"]!.GetValue<string>() == "Results");
        Assert.Equal("image", Assert.Single(info["media"]!.AsArray())!["kind"]!.GetValue<string>());
        Assert.Equal("Check the numbers.", Assert.Single(info["comments"]!.AsArray())!["text"]!.GetValue<string>());
        Assert.True(info["notes"]![0]!["present"]!.GetValue<bool>());
        Assert.Equal("Finance", info["properties"]!["author"]!.GetValue<string>());
    }

    [Fact]
    public void QuerySlidesFull_ReportsRunsNotesAndComments()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode read = _workspace.Run(
            "slides", "read", "deck.pptx", "--content", "full", "--output-format", "json").Json();

        JsonNode first = read["slides"]![0]!;
        // Evaluation mode cuts read text short, so only the presence of the notes is checked.
        Assert.NotNull(first["notes"]);
        Assert.Single(first["comments"]!.AsArray());
        Assert.Contains(first["shapes"]!.AsArray(), static shape => shape!["kind"]!.GetValue<string>() == "image");
        Assert.Contains(first["shapes"]!.AsArray(), static shape => shape!["runs"] is JsonArray);
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

    [Theory]
    [InlineData("media", "index")]
    [InlineData("notes", "slide")]
    [InlineData("text", "slide")]
    public void Extract_ReportsEachWrittenFile(string what, string unit)
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode extracted = _workspace.Run(
            "slides", "extract", "deck.pptx", "--what", what, "--out-dir", what, "--output-format", "json").Json();

        Assert.Equal(what, extracted["what"]!.GetValue<string>());
        Assert.Null(extracted["items"]);
        JsonNode item = extracted["outputs"]![0]!;
        // Each file names the one unit it holds beside its file fields: a media item or a slide.
        Assert.Equal(
            new[] { "path", "format", "sizeBytes", "fingerprint", unit }.Order(StringComparer.Ordinal),
            item.AsObject().Select(static pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal(1, item[unit]!.GetValue<int>());
        string path = item["path"]!.GetValue<string>();
        Assert.Equal(Path.GetExtension(path).TrimStart('.'), item["format"]!.GetValue<string>());
        Assert.Equal(new FileInfo(path).Length, item["sizeBytes"]!.GetValue<long>());
        Assert.Equal(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),
            item["fingerprint"]!["sha256"]!.GetValue<string>());
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
