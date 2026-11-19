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
    public void ReadAndRender_RoundTripThroughTheBuiltCli()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        // The two commands only read the deck, so they run at once.
        CliResult read = null!, render = null!;
        Parallel.Invoke(
            () => read = _workspace.Run(
                "slides", "read", "deck.pptx", "--kind", "slide", "--output-format", "json"),
            () => render = _workspace.Run(
                "slides", "render", "deck.pptx",
                "--slides", "1", "--out", "slide.png", "--output-format", "json"));

        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonNode result = JsonNode.Parse(read.StdOut)!;
        Assert.Equal(3, result["objects"]!.AsArray().Count);
        Assert.All(result["objects"]!.AsArray(), static item => Assert.Equal("slide", item!["kind"]!.GetValue<string>()));
        Assert.Equal(3, result["inventory"]!["read"]!["slides"]!.GetValue<long>());
        Assert.True(render.ExitCode == 0, render.StdErr);
        Assert.Single(JsonNode.Parse(render.StdOut)!["outputs"]!.AsArray());
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
    public void Create_FromAnEncryptedTemplate_IsPasswordRequired()
    {
        using (var template = new Presentation())
        {
            template.ProtectionManager.Encrypt("template-secret");
            template.Save(_workspace.File("locked.potx"), SaveFormat.Potx);
        }

        CliResult refused = _workspace.Run(
            "slides", "create", "deck.pptx", "--template", "locked.potx", "--output-format", "json");

        Assert.Equal(3, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("PASSWORD_REQUIRED", error["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("deck.pptx")));
    }

    /// <summary>
    /// A PowerPoint 97-2003 binary template is saved as the new PPTX: its slides, on the layouts
    /// the engine reads them on.
    /// </summary>
    [Theory]
    [InlineData("ppt")]
    [InlineData("pot")]
    public void Create_FromABinaryPptTemplate_SavesItsSlidesAsPptx(string format)
    {
        string template = $"blank.{format}";
        using (var source = new Presentation())
        {
            source.Slides.AddEmptySlide(source.LayoutSlides[1]);
            source.Save(_workspace.File(template), format == "pot" ? SaveFormat.Pot : SaveFormat.Ppt);
        }

        string[] layouts;
        using (var loaded = new Presentation(_workspace.File(template)))
        {
            layouts = [.. loaded.Slides.Select(static slide => slide.LayoutSlide.Name)];
        }

        CliResult created = _workspace.Run(
            "slides", "create", "deck.pptx", "--template", template, "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        Assert.Equal(2, JsonNode.Parse(created.StdOut)!["slideCount"]!.GetValue<int>());
        Assert.Equal(LoadFormat.Pptx, PresentationFactory.Instance.GetPresentationInfo(_workspace.File("deck.pptx")).LoadFormat);
        using var deck = new Presentation(_workspace.File("deck.pptx"));
        Assert.Equal(layouts, deck.Slides.Select(static slide => slide.LayoutSlide.Name));
    }

    [Fact]
    public void Search_WindowNextReturnsTheFollowingHits()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        // The unpaged search is the reference, so evaluation watermark text cannot skew the pages.
        CliResult all = null!, first = null!;
        Parallel.Invoke(
            () => all = _workspace.Run(
                "slides", "search", "deck.pptx", "--pattern", "Slide", "--output-format", "json"),
            () => first = _workspace.Run(
                "slides", "search", "deck.pptx", "--pattern", "Slide",
                "--max-hits", "2", "--output-format", "json"));

        Assert.True(all.ExitCode == 0, all.StdErr);
        Assert.True(first.ExitCode == 0, first.StdErr);
        string[] expected = Hits(JsonNode.Parse(all.StdOut)!);
        Assert.True(expected.Length > 2, all.StdOut);
        JsonNode page = JsonNode.Parse(first.StdOut)!;
        Assert.Equal(expected[..2], Hits(page));
        Assert.Equal(2, page["window"]!["returned"]!.GetValue<int>());
        Assert.True(page["window"]!["truncated"]!.GetValue<bool>());
        JsonNode next = page["window"]!["next"]!;
        Assert.Equal("2", next["skip"]!.GetValue<string>());

        CliResult following = _workspace.RunNext(next, "slides", "search", "deck.pptx", "--pattern", "Slide", "--max-hits", "2", "--output-format", "json");

        Assert.True(following.ExitCode == 0, following.StdErr);
        JsonNode rest = JsonNode.Parse(following.StdOut)!;
        Assert.Equal(expected[2..Math.Min(4, expected.Length)], Hits(rest));
        Assert.Equal(expected.Length > 4, rest["window"]!["truncated"]!.GetValue<bool>());

        static string[] Hits(JsonNode result) =>
        [
            .. result["hits"]!.AsArray().Select(static hit =>
                $"{hit!["at"]}:{hit["row"]}:{hit["offset"]}"),
        ];
    }

    /// <summary>
    /// The object table applies the license before it opens the presentation: with a license, a
    /// read and an edit's read-back give text longer than five characters whole, where an engine
    /// that opened the file first would read it in evaluation mode, cut short.
    /// </summary>
    [Fact]
    public void LicensedReadAndEditReadBack_GiveTheWholeText()
    {
        TestLicense.Require("Evaluation mode cuts text longer than five characters short.");
        const string Text = "A sentence longer than five characters";
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 50).TextFrame.Text = Text;
            presentation.Save(_workspace.File("long.pptx"), SaveFormat.Pptx);
        }

        string license = TestLicense.Path!;
        CliResult read = _workspace.Run("slides", "read", "long.pptx", "--kind", "shape", "--license", license, "--output-format", "json");
        CliResult edit = _workspace.Run("slides", "edit", "long.pptx", "--out", "edited.pptx", "--license", license, "--output-format", "json",
            "--ops", """{"ops":[{"set":"slide/256/shape/2","props":{"x":20}}]}""");

        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.Equal(Text, JsonNode.Parse(read.StdOut)!["objects"]![0]!["props"]!["text"]!.GetValue<string>());
        Assert.True(edit.ExitCode == 0, edit.StdErr);
        Assert.Equal(Text, JsonNode.Parse(edit.StdOut)!["ops"]![0]!["now"]!["text"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse(edit.StdOut)!["license"]);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void TruncatedPresentation_IsReportedAsIncompleteRatherThanAnUnknownFormat()
    {
        using (var presentation = new Presentation())
        {
            presentation.Save(_workspace.File("whole.pptx"), SaveFormat.Pptx);
        }

        byte[] whole = File.ReadAllBytes(_workspace.File("whole.pptx"));
        File.WriteAllBytes(_workspace.File("cut.pptx"), whole[..(whole.Length / 3)]);

        CliResult read = _workspace.Run("slides", "read", "cut.pptx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(read.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.DoesNotContain("Unknown", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("incomplete", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
