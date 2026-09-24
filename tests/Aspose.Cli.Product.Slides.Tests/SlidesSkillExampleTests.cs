using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

[Category(TestCategory.Slow)]
public sealed class SlidesSkillExampleTests
{
    [Fact]
    public void OutlineExampleCreatesExpectedSlidesWithTemplateGeometry()
    {
        using var workspace = new TempWorkspace();
        InstalledSkillExampleResult run = InstalledSkillExample.Run(workspace, "aspose-cli-slides", "deck-from-outline");
        string directory = run.Directory;
        bool evaluation = AssertEvaluationDisclosed(run);
        using var deck = new Presentation(Path.Combine(directory, "qbr.pptx"));
        using LoadedPresentation template = SlidesPresentationLoader.OpenDefaultTemplate();

        Assert.Equal(3, deck.Slides.Count);
        AssertText("Quarterly update", Text(deck.Slides[0]), evaluation);
        AssertText("Highlights", Text(deck.Slides[1]), evaluation);
        AssertText("Next steps", Text(deck.Slides[2]), evaluation);
        Assert.Equal(template.Presentation.SlideSize.Size, deck.SlideSize.Size);
        Assert.True(File.Exists(Path.Combine(directory, "qbr.review/review.json")));
    }

    [Fact]
    public void EditExampleRetainsSourceAndChangesTitleAndNotes()
    {
        using var workspace = new TempWorkspace();
        InstalledSkillExampleResult run = InstalledSkillExample.Run(workspace, "aspose-cli-slides", "edit-deck-safely");
        string directory = run.Directory;
        bool evaluation = AssertEvaluationDisclosed(run);
        using var original = new Presentation(Path.Combine(directory, "deck.pptx"));
        using var changed = new Presentation(Path.Combine(directory, "deck.revised.pptx"));

        AssertText("Delivery plan", Text(original.Slides[1]), evaluation);
        Assert.DoesNotContain("Confirmed delivery plan", Text(original.Slides[1]), StringComparison.Ordinal);
        AssertText("Confirmed delivery plan", Text(changed.Slides[1]), evaluation);
        INotesSlide notes = changed.Slides[1].NotesSlideManager.NotesSlide;
        Assert.NotNull(notes);
        AssertText("Launch approved.", string.Join("\n", notes.Shapes.OfType<IAutoShape>()
            .Select(shape => shape.TextFrame?.Text)), evaluation);
        Assert.True(File.Exists(Path.Combine(directory, "deck.review/review.json")));
    }

    [Fact]
    public void DataExampleCreatesNativeChartWithDocumentedCoordinates()
    {
        using var workspace = new TempWorkspace();
        InstalledSkillExampleResult run = InstalledSkillExample.Run(workspace, "aspose-cli-slides", "data-slides");
        string directory = run.Directory;
        _ = AssertEvaluationDisclosed(run);
        using var deck = new Presentation(Path.Combine(directory, "metrics.review.pptx"));

        Assert.Equal(2, deck.Slides.Count);
        IChart chart = Assert.Single(deck.Slides[1].Shapes.OfType<IChart>());
        Assert.Equal(ChartType.ClusteredColumn, chart.Type);
        Assert.Equal(["North", "South", "East", "West"],
            chart.ChartData.Categories.Select(category => category.AsCell.Value.ToString()));
        Assert.Equal([120d, 90d, 75d, 60d],
            chart.ChartData.Series[0].DataPoints.Select(point => Convert.ToDouble(point.Value.Data)));
        Assert.True(File.Exists(Path.Combine(directory, "metrics.review/review.json")));
    }

    private static bool AssertEvaluationDisclosed(InstalledSkillExampleResult run)
    {
        bool evaluation = run.Outputs.Any(result => result["license"]?["mode"]?.GetValue<string>() == "evaluation");
        if (evaluation)
        {
            JsonNode[] warnings = run.Outputs.SelectMany(result => result["warnings"]?.AsArray() ?? []).OfType<JsonNode>().ToArray();
            Assert.Contains(warnings, warning => warning["code"]!.GetValue<string>() == EnvelopeParts.EvaluationWatermark.Code);
            Assert.Contains(warnings, warning => warning["code"]!.GetValue<string>() == SlidesEngineSupport.EvaluationInputWarning.Code);
        }
        return evaluation;
    }

    private static void AssertText(string expected, string actual, bool evaluation)
    {
        if (actual.Contains(expected, StringComparison.Ordinal)) { return; }
        Assert.True(evaluation, "Licensed example output must preserve the complete text.");
        Assert.Contains(expected[..Math.Min(5, expected.Length)], actual, StringComparison.Ordinal);
        Assert.Contains(SlidesEngineSupport.EvaluationTruncationMarker, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(ISlide slide) => string.Join("\n",
        slide.Shapes.OfType<IAutoShape>().Select(shape => shape.TextFrame?.Text));
}
