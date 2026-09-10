using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCliWorkflowTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void InspectQueryAndConvert_RoundTripsThroughTheBuiltCli()
    {
        CliResult capabilities = _workspace.Run(
            "capabilities", "slides", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        JsonNode product = JsonNode.Parse(capabilities.StdOut)!["products"]![0]!;
        Assert.Equal(
            ["slides", "slides convert", "slides create", "slides edit", "slides extract", "slides inspect", "slides query", "slides query search", "slides query slides", "slides render"],
            product["commands"]!.AsArray()
                .Select(static command => command!["path"]!.GetValue<string>()));
        Assert.Equal(
            [
                "add_section", "add_slide", "append_presentation", "apply_layout",
                "delete_shape", "delete_slides", "duplicate_slide", "insert_chart",
                "insert_image", "insert_shape", "insert_table", "move_slide",
                "replace_text", "set_background", "set_body", "set_footer",
                "set_notes", "set_properties", "set_shape_style", "set_slide_hidden",
                "set_slide_size", "set_table_cell", "set_text", "set_title",
                "set_transition", "update_chart_data",
            ],
            product["operations"]!.AsArray()
                .Select(static operation => operation!["id"]!.GetValue<string>()));

        CreateDeck(_workspace.File("deck.pptx"));

        CliResult info = _workspace.Run(
            "slides", "inspect", "deck.pptx", "--output", "json");
        CliResult read = _workspace.Run(
            "slides", "query", "slides", "deck.pptx", "--slides", "1", "--output", "json");
        CliResult convert = _workspace.Run(
            "slides", "convert", "deck.pptx", "--to", "png",
            "--slides", "1", "--out", "slide.png", "--output", "json");

        Assert.True(info.ExitCode == 0, info.StdErr);
        Assert.Equal(3, JsonNode.Parse(info.StdOut)!["presentation"]!["slides"]!.GetValue<int>());
        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonArray slides = JsonNode.Parse(read.StdOut)!["slides"]!.AsArray();
        Assert.Single(slides);
        Assert.Equal(1, slides[0]!["number"]!.GetValue<int>());
        Assert.True(convert.ExitCode == 0, convert.StdErr);
        Assert.Single(JsonNode.Parse(convert.StdOut)!["outputs"]!.AsArray());
        Assert.True(File.Exists(_workspace.File("slide.png")));
    }

    private static void CreateDeck(string path)
    {
        SlidesFontCatalog.EnsureInitialized();
        using var presentation = new Presentation();
        for (int index = 0; index < 3; index++)
        {
            ISlide slide = index == 0
                ? presentation.Slides[0]
                : presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
            slide.Shapes.AddAutoShape(
                ShapeType.Rectangle,
                40,
                30,
                600,
                70).TextFrame.Text = $"Slide {index + 1}";
        }

        presentation.Save(path, SaveFormat.Pptx);
    }

    public void Dispose() => _workspace.Dispose();
}
