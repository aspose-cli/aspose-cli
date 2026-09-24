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
            Assert.Single(product["operations"]!.AsArray())!["ops"]!.AsArray()
                .Select(static operation => operation!.GetValue<string>()));

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

    [Fact]
    public void QuerySlides_NextSpellsRemainingSlidesAsRangesAndRereadsACutSlide()
    {
        using (var presentation = new Presentation())
        {
            for (int index = 0; index < 4; index++)
            {
                ISlide slide = index == 0
                    ? presentation.Slides[0]
                    : presentation.Slides.AddEmptySlide(presentation.Slides[0].LayoutSlide);
                slide.Shapes.Clear();
                slide.Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 40).TextFrame.Text = "Quarterly";
            }

            presentation.Save(_workspace.File("four.pptx"), SaveFormat.Pptx);
        }

        CliResult window = _workspace.Run(
            "slides", "query", "slides", "four.pptx", "--slides", "1,2,4", "--max-chars", "20", "--output", "json");
        CliResult single = _workspace.Run(
            "slides", "query", "slides", "four.pptx", "--max-chars", "4", "--notes", "--output", "json");

        // Evaluation mode may add watermark text, so the cut point comes from the response.
        Assert.True(window.ExitCode == 0, window.StdErr);
        JsonNode read = JsonNode.Parse(window.StdOut)!;
        JsonArray returned = read["slides"]!.AsArray();
        JsonNode last = returned[^1]!;
        bool cut = last["contentTruncated"]!.GetValue<bool>();
        int[] selection = [1, 2, 4];
        int resume = Array.IndexOf(selection, last["number"]!.GetValue<int>()) + (cut ? 0 : 1);
        int budget = cut && returned.Count == 1 ? 40 : 20;
        Assert.EndsWith(
            $" --slides {Aspose.Cli.Sdk.Addressing.PageRange.Describe(selection[resume..])} --scope shapes --max-chars {budget} --output json",
            read["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.True(single.ExitCode == 0, single.StdErr);
        Assert.EndsWith(
            " --slides 1-4 --scope shapes --notes --max-chars 8 --output json",
            JsonNode.Parse(single.StdOut)!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    public void Dispose() => _workspace.Dispose();
}
