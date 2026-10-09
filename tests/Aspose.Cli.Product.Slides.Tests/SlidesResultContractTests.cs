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

        JsonNode info = Succeeds(_workspace.Run(
            "slides", "inspect", "deck.pptx", "--preview",
            "--detail", "comments", "--detail", "fonts", "--detail", "layouts", "--detail", "masters",
            "--detail", "media", "--detail", "notes", "--detail", "properties", "--detail", "sections",
            "--output", "json"));

        Assert.Contains(info["sections"]!.AsArray(), static section => section!["name"]!.GetValue<string>() == "Results");
        Assert.Equal("image", Assert.Single(info["media"]!.AsArray())!["type"]!.GetValue<string>());
        Assert.Equal("Check the numbers.", Assert.Single(info["comments"]!.AsArray())!["text"]!.GetValue<string>());
        Assert.True(info["notes"]![0]!["present"]!.GetValue<bool>());
        Assert.Equal("Finance", info["properties"]!["author"]!.GetValue<string>());
    }

    [Fact]
    public void QuerySlidesFull_ReportsRunsNotesAndComments()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode read = Succeeds(_workspace.Run(
            "slides", "query", "slides", "deck.pptx", "--scope", "full", "--output", "json"));

        JsonNode first = read["slides"]![0]!;
        // Evaluation mode cuts read text short, so only the presence of the notes is checked.
        Assert.NotNull(first["notes"]);
        Assert.Single(first["comments"]!.AsArray());
        Assert.Contains(first["shapes"]!.AsArray(), static shape => shape!["type"]!.GetValue<string>() == "image");
        Assert.Contains(first["shapes"]!.AsArray(), static shape => shape!["runs"] is JsonArray);
    }

    [Fact]
    public void Render_ReportsTheResolutionOrTheWidth()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode byDpi = Succeeds(_workspace.Run(
            "slides", "render", "deck.pptx", "--slides", "1-2", "--dpi", "36", "--out", "dpi.png", "--output", "json"));
        JsonNode byWidth = Succeeds(_workspace.Run(
            "slides", "render", "deck.pptx", "--slides", "1", "--width", "64", "--out", "width.png", "--output", "json"));

        Assert.Equal(36, byDpi["dpi"]!.GetValue<int>());
        Assert.Equal(2, byDpi["outputs"]!.AsArray().Count);
        Assert.Null(byWidth["dpi"]);
        Assert.Equal(64, byWidth["width"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("media", "image")]
    [InlineData("notes", "notes")]
    [InlineData("text", "text")]
    public void Extract_ReportsEachWrittenFile(string what, string kind)
    {
        CreateDeck(_workspace.File("deck.pptx"));

        JsonNode extracted = Succeeds(_workspace.Run(
            "slides", "extract", "deck.pptx", "--what", what, "--out-dir", what, "--output", "json"));

        Assert.Equal(what, extracted["what"]!.GetValue<string>());
        JsonNode item = extracted["items"]![0]!;
        Assert.Equal(kind, item["kind"]!.GetValue<string>());
        Assert.True(File.Exists(item["path"]!.GetValue<string>()));
    }

    private static JsonNode Succeeds(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
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
