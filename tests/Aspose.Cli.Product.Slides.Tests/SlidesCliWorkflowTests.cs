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
        CreateDeck(_workspace.File("deck.pptx"));

        // The three commands only read the deck, so they run at once.
        CliResult info = null!, read = null!, convert = null!;
        Parallel.Invoke(
            () => info = _workspace.Run(
                "slides", "inspect", "deck.pptx", "--output", "json"),
            () => read = _workspace.Run(
                "slides", "query", "slides", "deck.pptx", "--slides", "1", "--output", "json"),
            () => convert = _workspace.Run(
                "slides", "convert", "deck.pptx", "--to", "png",
                "--slides", "1", "--out", "slide.png", "--output", "json"));

        Assert.True(info.ExitCode == 0, info.StdErr);
        Assert.Equal(3, JsonNode.Parse(info.StdOut)!["presentation"]!["slideCount"]!.GetValue<int>());
        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonArray slides = JsonNode.Parse(read.StdOut)!["slides"]!.AsArray();
        Assert.Single(slides);
        Assert.Equal(1, slides[0]!["slide"]!.GetValue<int>());
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
    public void Review_FindingOnOneSlide_HasThatSlidesImageAsEvidence()
    {
        CreateDeck(_workspace.File("deck.pptx"));
        using (var presentation = new Presentation(_workspace.File("deck.pptx")))
        {
            presentation.Slides[1].Shapes.AddAutoShape(ShapeType.Rectangle, 600, 200, 300, 80).TextFrame.Text = "Off the edge";
            presentation.Save(_workspace.File("deck.pptx"), SaveFormat.Pptx);
        }

        CliResult review = _workspace.Run(
            "review", "deck.pptx", "--out", "evidence", "--code", "SLIDES_SHAPE_OUTSIDE_SLIDE", "--output", "json");

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode finding = Assert.Single(JsonNode.Parse(review.StdOut)!["findings"]!.AsArray())!;
        Assert.Equal("slide 2", finding["location"]!.GetValue<string>());
        Assert.Equal(
            ["artifacts/slide-0002.png"],
            finding["evidence"]!.AsArray().Select(static path => path!.GetValue<string>()));
    }

    [Fact]
    public void Review_OfAPasswordEncryptedPresentation_RendersWithItsPassword()
    {
        // An encrypted Open XML presentation is an OLE compound file, not a ZIP package.
        CreateDeck(_workspace.File("locked.pptx"));
        using (var presentation = new Presentation(_workspace.File("locked.pptx")))
        {
            presentation.ProtectionManager.Encrypt("secret");
            presentation.Save(_workspace.File("locked.pptx"), SaveFormat.Pptx);
        }

        CliResult unlocked = _workspace.RunWithEnv(
            new Dictionary<string, string?> { ["DECK_PASSWORD"] = "secret" },
            "review", "locked.pptx", "--password-env", "DECK_PASSWORD", "--out", "unlocked", "--output", "json");
        CliResult locked = _workspace.Run(
            "review", "locked.pptx", "--out", "locked", "--output", "json");

        Assert.True(unlocked.ExitCode == 0, unlocked.StdOut + unlocked.StdErr);
        Assert.True(File.Exists(_workspace.File(Path.Combine("unlocked", "artifacts", "slide-0001.png"))));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked.StdErr)!["error"]!["code"]!.GetValue<string>());
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
        int resume = Array.IndexOf(selection, last["slide"]!.GetValue<int>()) + (cut ? 0 : 1);
        int budget = cut && returned.Count == 1 ? 40 : 20;
        Assert.EndsWith(
            $" --slides {Aspose.Cli.Sdk.Addressing.PageRange.Describe(selection[resume..])} --scope shapes --max-chars {budget} --output json",
            read["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.True(single.ExitCode == 0, single.StdErr);
        Assert.EndsWith(
            " --slides 1-4 --scope shapes --notes --max-chars 8 --output json",
            JsonNode.Parse(single.StdOut)!["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuerySearch_WindowNextReturnsTheFollowingHits()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        // The unpaged search is the reference, so evaluation watermark text cannot skew the pages.
        CliResult all = null!, first = null!;
        Parallel.Invoke(
            () => all = _workspace.Run(
                "slides", "query", "search", "deck.pptx", "--pattern", "Slide", "--scope", "shapes", "--output", "json"),
            () => first = _workspace.Run(
                "slides", "query", "search", "deck.pptx", "--pattern", "Slide", "--scope", "shapes",
                "--max-hits", "2", "--output", "json"));

        Assert.True(all.ExitCode == 0, all.StdErr);
        Assert.True(first.ExitCode == 0, first.StdErr);
        string[] expected = Hits(JsonNode.Parse(all.StdOut)!);
        Assert.True(expected.Length > 2, all.StdOut);
        JsonNode page = JsonNode.Parse(first.StdOut)!;
        Assert.Equal(expected[..2], Hits(page));
        Assert.Equal(2, page["window"]!["returned"]!.GetValue<int>());
        Assert.True(page["window"]!["truncated"]!.GetValue<bool>());
        string next = page["window"]!["next"]!.GetValue<string>();
        Assert.EndsWith(
            " --pattern Slide --scope shapes --max-hits 2 --skip 2 --output json", next, StringComparison.Ordinal);

        CliResult following = _workspace.RunCommandLine(next);

        Assert.True(following.ExitCode == 0, following.StdErr);
        JsonNode rest = JsonNode.Parse(following.StdOut)!;
        Assert.Equal(expected[2..Math.Min(4, expected.Length)], Hits(rest));
        Assert.Equal(expected.Length > 4, rest["window"]!["truncated"]!.GetValue<bool>());

        static string[] Hits(JsonNode result) =>
        [
            .. result["hits"]!.AsArray().Select(static hit =>
                $"{hit!["slide"]}:{hit["shapeId"]}:{hit["start"]}"),
        ];
    }

    /// <summary>
    /// Evaluation mode cuts read text short, so replace_text refuses only when the text in its
    /// scope was cut: short shape text is replaced while long speaker notes stay out of scope.
    /// </summary>
    [Fact]
    public void Evaluation_ReplaceTextRefusesOnlyWhenTheTextInItsScopeWasCutShort()
    {
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 50, 50, 200, 60).TextFrame.Text = "Hi";
            slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Speaker notes long enough to be cut short.";
            presentation.Save(_workspace.File("deck.pptx"), SaveFormat.Pptx);
        }

        File.WriteAllText(_workspace.File("shapes.json"), """{"ops":[{"op":"replace_text","find":"Hi","replace":"Yo","scope":"shapes"}]}""");
        File.WriteAllText(_workspace.File("all.json"), """{"ops":[{"op":"replace_text","find":"Hi","replace":"Yo","scope":"all"}]}""");

        CliResult shapes = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "shapes.json", "--out", "shapes.pptx",
            "--license-mode", "evaluation", "--output", "json");
        CliResult all = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "all.json", "--out", "all.pptx",
            "--license-mode", "evaluation", "--output", "json");

        Assert.True(shapes.ExitCode == 0, shapes.StdErr);
        Assert.Equal(1, JsonNode.Parse(shapes.StdOut)!["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.Equal(7, all.ExitCode);
        Assert.Equal("EVALUATION_LIMIT", JsonNode.Parse(all.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("all.pptx")));
    }

    public void Dispose() => _workspace.Dispose();
}
