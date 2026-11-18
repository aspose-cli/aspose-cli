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
    public void InspectQueryAndRender_RoundTripsThroughTheBuiltCli()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        // The three commands only read the deck, so they run at once.
        CliResult info = null!, read = null!, render = null!;
        Parallel.Invoke(
            () => info = _workspace.Run(
                "slides", "inspect", "deck.pptx", "--output-format", "json"),
            () => read = _workspace.Run(
                "slides", "read", "deck.pptx", "--slides", "1", "--output-format", "json"),
            () => render = _workspace.Run(
                "slides", "render", "deck.pptx",
                "--slides", "1", "--out", "slide.png", "--output-format", "json"));

        Assert.True(info.ExitCode == 0, info.StdErr);
        Assert.Equal(3, JsonNode.Parse(info.StdOut)!["presentation"]!["slideCount"]!.GetValue<int>());
        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonArray slides = JsonNode.Parse(read.StdOut)!["slides"]!.AsArray();
        Assert.Single(slides);
        Assert.Equal(1, slides[0]!["slide"]!.GetValue<int>());
        Assert.True(render.ExitCode == 0, render.StdErr);
        Assert.Single(JsonNode.Parse(render.StdOut)!["outputs"]!.AsArray());
        Assert.True(File.Exists(_workspace.File("slide.png")));
    }

    /// <summary>
    /// A title with a line break reads as two lines in JSON, as its shape's text does.
    /// </summary>
    [Fact]
    public void TitleWithALineBreak_ReadsAsLinesInJson()
    {
        Assert.Equal(0, _workspace.Run("slides", "create", "deck.pptx", "--output-format", "json").ExitCode);
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_title","slide":1,"text":"A\u000bB"}]}""");
        CliResult edit = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", "titled.pptx", "--output-format", "json");
        Assert.True(edit.ExitCode == 0, edit.StdErr);

        CliResult json = _workspace.Run("slides", "read", "titled.pptx", "--output-format", "json");

        Assert.Equal("A\nB", JsonNode.Parse(json.StdOut)!["slides"]![0]!["title"]!.GetValue<string>());
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

    [Theory]
    [InlineData("ppt")]
    [InlineData("pot")]
    public void Create_FromABinaryPptTemplate_FillsItsLayouts(string format)
    {
        string template = CreateBinaryDeck(format);

        CliResult created = _workspace.Run(
            "slides", "create", "deck.pptx", "--from-markdown", "min.md", "--template", template, "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        using var deck = new Presentation(_workspace.File("deck.pptx"));
        Assert.Equal(["Title Slide", "Title and Content"], deck.Slides.Select(static slide => slide.LayoutSlide.Name));
    }

    [Theory]
    [InlineData("ppt")]
    [InlineData("pps")]
    public void Edit_OfABinaryPptInput_InsertsSlidesOnItsLayouts(string format)
    {
        string input = CreateBinaryDeck(format);
        File.WriteAllText(
            _workspace.File("insert.json"),
            """{"ops":[{"op":"insert_slide","layout":"Title Slide"},{"op":"insert_slide","layout":"Section Header"},{"op":"insert_slide"}]}""");

        CliResult edited = _workspace.Run(
            "slides", "edit", input, "--ops", "insert.json", "--out", "edited.pptx", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        using var deck = new Presentation(_workspace.File("edited.pptx"));
        Assert.Equal(
            ["Title Slide", "Title and Content", "Title Slide", "Section Header", "Title Slide"],
            deck.Slides.Select(static slide => slide.LayoutSlide.Name));
    }

    [Fact]
    public void EvaluationEdit_OfAPptInput_HasTheWatermarksOfTheSamePptxInput()
    {
        string ppt = CreateBinaryDeck("ppt");
        File.WriteAllText(_workspace.File("insert.json"), """{"ops":[{"op":"insert_slide","layout":"Title and Content"}]}""");

        int[] Watermarks(string input, string output)
        {
            CliResult edited = _workspace.Run(
                "slides", "edit", input, "--ops", "insert.json", "--out", output, "--license-mode", "evaluation", "--output-format", "json");
            Assert.True(edited.ExitCode == 0, edited.StdErr);
            using var deck = new Presentation(_workspace.File(output));
            return [.. deck.Slides.Select(static slide => slide.Shapes.Count(SlidesEvaluationMarks.IsEvaluationWatermark))];
        }

        int[] fromPptx = Watermarks("blank.pptx", "from-pptx.pptx");
        int[] fromPpt = Watermarks(ppt, "from-ppt.pptx");

        Assert.All(fromPptx, static count => Assert.True(count > 0));
        Assert.Equal(fromPptx, fromPpt);
    }

    /// <summary>
    /// The min.md outline, blank.pptx authored from it on the CLI's own design, and that deck as a
    /// PowerPoint 97-2003 binary file of the format: the CLI converts to PPT, the engine saves the rest.
    /// </summary>
    private string CreateBinaryDeck(string format)
    {
        File.WriteAllText(_workspace.File("min.md"), "# Title\n\n## Slide two\n\n- point\n");
        Assert.Equal(0, _workspace.Run("slides", "create", "blank.pptx", "--from-markdown", "min.md").ExitCode);
        string binary = $"blank.{format}";
        if (format == "ppt")
        {
            Assert.Equal(0, _workspace.Run("slides", "convert", "blank.pptx", "--out", binary).ExitCode);
        }
        else
        {
            using var source = new Presentation(_workspace.File("blank.pptx"));
            source.Save(_workspace.File(binary), format == "pot" ? SaveFormat.Pot : SaveFormat.Pps);
        }

        return binary;
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
            "slides", "read", "four.pptx", "--slides", "1,2,4", "--max-chars", "20", "--output-format", "json");
        CliResult single = _workspace.Run(
            "slides", "read", "four.pptx", "--max-chars", "4", "--notes", "--output-format", "json");

        // Evaluation mode may add watermark text, so the cut point comes from the response.
        Assert.True(window.ExitCode == 0, window.StdErr);
        JsonNode read = JsonNode.Parse(window.StdOut)!;
        JsonArray returned = read["slides"]!.AsArray();
        JsonNode last = returned[^1]!;
        bool cut = last["contentTruncated"]!.GetValue<bool>();
        int[] selection = [1, 2, 4];
        int resume = Array.IndexOf(selection, last["slide"]!.GetValue<int>()) + (cut ? 0 : 1);
        int budget = cut && returned.Count == 1 ? 40 : 20;
        Assert.Equal(
            (Aspose.Cli.Sdk.Addressing.PageRange.Describe(selection[resume..]), $"{budget}"),
            (read["window"]!["next"]!["slides"]!.GetValue<string>(), read["window"]!["next"]!["max-chars"]!.GetValue<string>()));
        Assert.True(single.ExitCode == 0, single.StdErr);
        JsonNode singleNext = JsonNode.Parse(single.StdOut)!["window"]!["next"]!;
        Assert.Equal(("1-4", "8"), (singleNext["slides"]!.GetValue<string>(), singleNext["max-chars"]!.GetValue<string>()));
    }

    [Fact]
    public void QuerySearch_WindowNextReturnsTheFollowingHits()
    {
        CreateDeck(_workspace.File("deck.pptx"));

        // The unpaged search is the reference, so evaluation watermark text cannot skew the pages.
        CliResult all = null!, first = null!;
        Parallel.Invoke(
            () => all = _workspace.Run(
                "slides", "search", "deck.pptx", "--pattern", "Slide", "--scope", "shapes", "--output-format", "json"),
            () => first = _workspace.Run(
                "slides", "search", "deck.pptx", "--pattern", "Slide", "--scope", "shapes",
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

        CliResult following = _workspace.RunNext(next, "slides", "search", "deck.pptx", "--pattern", "Slide", "--scope", "shapes", "--max-hits", "2", "--output-format", "json");

        Assert.True(following.ExitCode == 0, following.StdErr);
        JsonNode rest = JsonNode.Parse(following.StdOut)!;
        Assert.Equal(expected[2..Math.Min(4, expected.Length)], Hits(rest));
        Assert.Equal(expected.Length > 4, rest["window"]!["truncated"]!.GetValue<bool>());

        static string[] Hits(JsonNode result) =>
        [
            .. result["hits"]!.AsArray().Select(static hit =>
                $"{hit!["slide"]}:{hit["shapeId"]}:{hit["context"]}"),
        ];
    }

    /// <summary>
    /// --verify reads the staged output back: the operations with a read-back are checked, the
    /// others are not, and an operation that changed nothing reports 0 with OP_NO_EFFECT.
    /// </summary>
    [Fact]
    public void Verify_ChecksTheOperationsItCanReadBack()
    {
        CreateDeck(_workspace.File("deck.pptx"));
        File.WriteAllText(_workspace.File("ops.json"), """
            {"ops":[
              {"id":"hide","op":"set_slide_hidden","slides":"1","hidden":true},
              {"id":"props","op":"set_properties","title":"Q3"},
              {"id":"style","op":"set_background","slides":"1","color":"#336699"},
              {"id":"again","op":"set_slide_hidden","slides":"1","hidden":true}
            ]}
            """);

        CliResult edit = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", "verified.pptx", "--verify", "--output-format", "json");

        Assert.True(edit.ExitCode == 0, edit.StdErr);
        JsonNode result = JsonNode.Parse(edit.StdOut)!;
        JsonNode verification = result["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.Empty(verification["issues"]!.AsArray());
        Assert.Equal(["props", "again"], verification["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        Assert.Equal([1L, 1L, 1L, 0L], result["applied"]!.AsArray().Select(static outcome => outcome!["itemsAffected"]!.GetValue<long>()));
        Assert.Equal(["slide/1"], result["applied"]![0]!["targets"]!.AsArray().Select(static target => target!.GetValue<string>()));
        Assert.Equal(["property/title"], result["applied"]![1]!["targets"]!.AsArray().Select(static target => target!.GetValue<string>()));
        Assert.Contains("OP_NO_EFFECT", WarningCodes(result));
        Assert.Equal([1], result["slidesTouched"]!.AsArray().Select(static slide => slide!.GetValue<int>()));
    }

    /// <summary>
    /// Evaluation mode cuts read text short, so replace_text still replaces the text it sees and
    /// discloses EVALUATION_INPUT_TRUNCATED only when the text in its scope was cut: long speaker
    /// notes are cut, and a scope without them reports nothing.
    /// </summary>
    [Fact]
    public void Evaluation_ReplaceTextDisclosesOnlyWhenTheTextInItsScopeWasCutShort()
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
            "--license-mode", "evaluation", "--output-format", "json");
        CliResult all = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "all.json", "--out", "all.pptx",
            "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(shapes.ExitCode == 0, shapes.StdErr);
        JsonNode shapesResult = JsonNode.Parse(shapes.StdOut)!;
        Assert.Equal(1, shapesResult["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.DoesNotContain("EVALUATION_INPUT_TRUNCATED", WarningCodes(shapesResult));
        Assert.True(all.ExitCode == 0, all.StdErr);
        JsonNode allResult = JsonNode.Parse(all.StdOut)!;
        Assert.Equal(1, allResult["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.Contains("EVALUATION_INPUT_TRUNCATED", WarningCodes(allResult));
        Assert.True(File.Exists(_workspace.File("all.pptx")));
    }

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// Evaluation mode reads longer text as its first characters and a notice, so new text that
    /// starts like the old reads the same before and after: the operation counts the object it
    /// wrote, as changed, and discloses that the read was cut.
    /// </summary>
    [Theory]
    [InlineData("""{"op":"set_title","slide":1,"text":"业绩概览 Performance (draft)"}""", "slide/1/shape/")]
    [InlineData("""{"op":"set_body","slide":1,"paragraphs":[{"text":"Revenue grew in every region (draft)"}]}""", "slide/1/shape/")]
    [InlineData("""{"op":"set_text","slide":1,"shapeName":"Box","text":"Speaker text long enough (draft)"}""", "slide/1/shape/")]
    [InlineData("""{"op":"set_table_cell","slide":1,"shapeName":"Grid","row":1,"column":1,"text":"Quarterly total (draft)"}""", "slide/1/shape/")]
    [InlineData("""{"op":"set_notes","slide":1,"text":"Speaker notes long enough to be cut (draft)"}""", "slide/1/notes")]
    public void Evaluation_TextThatReadsTheSameCutShort_CountsAsWritten(string operation, string target)
    {
        CreateCutDeck(_workspace.File("cut.pptx"), "业绩概览 Performance");
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");

        CliResult edit = _workspace.Run(
            "slides", "edit", "cut.pptx", "--ops", "ops.json", "--out", "out.pptx", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
        JsonNode result = JsonNode.Parse(edit.StdOut)!;
        Assert.Equal(1, result["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        string written = Assert.Single(result["applied"]![0]!["targets"]!.AsArray())!.GetValue<string>();
        Assert.StartsWith(target, written, StringComparison.Ordinal);
        Assert.DoesNotContain("OP_NO_EFFECT", WarningCodes(result));
        Assert.Contains("EVALUATION_INPUT_TRUNCATED", WarningCodes(result));
    }

    [Fact]
    public void Evaluation_ShortTextWrittenAgain_ChangesNothing()
    {
        CreateCutDeck(_workspace.File("short.pptx"), "Q3");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_title","slide":1,"text":"Q3"}]}""");

        CliResult edit = _workspace.Run(
            "slides", "edit", "short.pptx", "--ops", "ops.json", "--out", "out.pptx", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
        JsonNode result = JsonNode.Parse(edit.StdOut)!;
        Assert.Equal(0, result["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.Contains("OP_NO_EFFECT", WarningCodes(result));
    }

    /// <summary>A title-and-content slide whose body, text box, table cell and notes are long enough for evaluation mode to cut.</summary>
    private static void CreateCutDeck(string path, string title)
    {
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides.AddEmptySlide(presentation.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject));
        presentation.Slides.RemoveAt(0);
        IAutoShape[] placeholders = [.. slide.Shapes.OfType<IAutoShape>().Where(static shape => shape.Placeholder is not null)];
        placeholders.First(static shape => shape.Placeholder!.Type is PlaceholderType.Title or PlaceholderType.CenteredTitle).TextFrame.Text = title;
        placeholders.First(static shape => shape.Placeholder!.Type is PlaceholderType.Body or PlaceholderType.Object).TextFrame.Text =
            "Revenue grew in every region";
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 50, 300, 300, 60);
        box.Name = "Box";
        box.TextFrame.Text = "Speaker text long enough";
        ITable grid = slide.Shapes.AddTable(400, 300, [150, 150], [40, 40]);
        grid.Name = "Grid";
        grid[0, 0].TextFrame.Text = "Quarterly total";
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Speaker notes long enough to be cut";
        presentation.Save(path, SaveFormat.Pptx);
    }

    private static string[] WarningCodes(JsonNode result) =>
        [.. result["warnings"]?.AsArray().Select(static warning => warning!["code"]!.GetValue<string>()) ?? []];

    [Fact]
    public void InspectLayouts_OmitsTheNameOfAnUnnamedLayout()
    {
        using (var presentation = new Presentation())
        {
            presentation.LayoutSlides[0].Name = string.Empty;
            presentation.Save(_workspace.File("unnamed.pptx"), SaveFormat.Pptx);
        }

        // The run checks the result against its schema, where a present name is a string.
        CliResult inspect = _workspace.Run("slides", "inspect", "unnamed.pptx", "--detail", "layouts", "--output-format", "json");

        Assert.True(inspect.ExitCode == 0, inspect.StdOut + inspect.StdErr);
        Assert.Contains(JsonNode.Parse(inspect.StdOut)!["layouts"]!.AsArray(), static layout => layout!["name"] is null);
    }

    [Fact]
    public void TruncatedPresentation_IsReportedAsIncompleteRatherThanAnUnknownFormat()
    {
        using (var presentation = new Presentation())
        {
            presentation.Save(_workspace.File("whole.pptx"), SaveFormat.Pptx);
        }

        byte[] whole = File.ReadAllBytes(_workspace.File("whole.pptx"));
        File.WriteAllBytes(_workspace.File("cut.pptx"), whole[..(whole.Length / 3)]);

        CliResult inspect = _workspace.Run("slides", "inspect", "cut.pptx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(inspect.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.DoesNotContain("Unknown", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("incomplete", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
