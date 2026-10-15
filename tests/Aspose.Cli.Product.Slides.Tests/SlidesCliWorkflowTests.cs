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
                "slides", "inspect", "deck.pptx", "--output-format", "json"),
            () => read = _workspace.Run(
                "slides", "read", "deck.pptx", "--slides", "1", "--output-format", "json"),
            () => convert = _workspace.Run(
                "slides", "convert", "deck.pptx", "--to", "png",
                "--slides", "1", "--out", "slide.png", "--output-format", "json"));

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
            "review", "deck.pptx", "--out", "evidence", "--code", "SLIDES_SHAPE_OUTSIDE_SLIDE", "--output-format", "json");

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
            "review", "locked.pptx", "--password-env", "DECK_PASSWORD", "--out", "unlocked", "--output-format", "json");
        CliResult locked = _workspace.Run(
            "review", "locked.pptx", "--out", "locked", "--output-format", "json");

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
        Assert.EndsWith(
            $" --slides {Aspose.Cli.Sdk.Addressing.PageRange.Describe(selection[resume..])} --content shapes --max-chars {budget} --output-format json",
            read["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.True(single.ExitCode == 0, single.StdErr);
        Assert.EndsWith(
            " --slides 1-4 --content shapes --notes --max-chars 8 --output-format json",
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
        string next = page["window"]!["next"]!.GetValue<string>();
        Assert.EndsWith(
            " --pattern Slide --scope shapes --max-hits 2 --skip 2 --output-format json", next, StringComparison.Ordinal);

        CliResult following = _workspace.RunCommandLine(next);

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

    /// <summary>
    /// A review in evaluation mode reads text evaluation mode cuts short through its view and
    /// publishes a result: the input's truncation is disclosed once, not by both.
    /// </summary>
    [Fact]
    public void EvaluationReview_DisclosesTheCutShortTextOnce()
    {
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 50, 50, 400, 80).TextFrame.Text = "A sentence much longer than five characters.";
            slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Speaker notes long enough to be cut short.";
            presentation.Save(_workspace.File("long.pptx"), SaveFormat.Pptx);
        }

        CliResult review = _workspace.Run("review", "long.pptx", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdErr);
        Assert.Single(
            JsonNode.Parse(review.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_INPUT_TRUNCATED");
    }

    [Fact]
    public void EvaluationReview_LeavesTheWatermarkOutOfTheOtherChecksAndCountsIt()
    {
        WatermarkedDeck(_workspace.File("marked.pptx"));

        CliResult review = _workspace.Run("review", "marked.pptx", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Equal(["SLIDES_EVALUATION_WATERMARK"], FindingCodes(result));
        Assert.True(Metric(result, "excludedEvaluationMarks") >= 1);
        Assert.Null(Metric(result, "excludedEvaluationWatermarks"));
    }

    [Fact]
    public void LicensedReview_ChecksTheWatermarkLikeAnyShapeAndReportsNoExcludedMarks()
    {
        TestLicense.Require("Evaluation mode leaves the watermark out of the checks.");
        Aspose.Cli.TestKit.Scenarios.ScenarioLicense.Project(_workspace.Path);
        WatermarkedDeck(_workspace.File("marked.pptx"));

        CliResult review = _workspace.Run("review", "marked.pptx", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Null(result["license"]);
        Assert.Contains("SLIDES_EVALUATION_WATERMARK", FindingCodes(result));
        Assert.Contains(FindingCodes(result), static code => code != "SLIDES_EVALUATION_WATERMARK");
        Assert.Null(Metric(result, "excludedEvaluationMarks"));
    }

    /// <summary>A deck with a box shaped like the evaluation watermark over a table.</summary>
    private static void WatermarkedDeck(string path)
    {
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides[0];
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 100, 400, 80);
        box.FillFormat.FillType = FillType.NoFill;
        box.TextFrame.Text = "Evaluation only.";
        box.TextFrame.Paragraphs.Add(new Paragraph { Text = "Created with Aspose.Slides." });
        box.ShapeLock.SelectLocked = true;
        box.ShapeLock.PositionLocked = true;
        slide.Shapes.AddTable(100, 100, [200, 200], [100, 100]);
        presentation.Save(path, SaveFormat.Pptx);
    }

    private static string[] FindingCodes(JsonNode result) =>
        [.. result["findings"]!.AsArray().Select(static finding => finding!["code"]!.GetValue<string>()).Distinct()];

    private static long? Metric(JsonNode result, string name) =>
        result["coverage"]!["metrics"]!.AsArray()
            .FirstOrDefault(metric => metric!["name"]!.GetValue<string>() == name)?["value"]!.GetValue<long>();

    public void Dispose() => _workspace.Dispose();

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

    [Theory]
    [InlineData("notes", "set_notes")]
    [InlineData("shapes", "set_text")]
    public void EvaluationReplaceOverCutText_HintsTheOperationThatWritesWithoutReading(string scope, string operation)
    {
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 50, 50, 400, 80).TextFrame.Text = "Speaker text long enough to be cut short.";
            slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Speaker notes long enough to be cut short.";
            presentation.Save(_workspace.File("cut.pptx"), SaveFormat.Pptx);
        }

        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{"op":"replace_text","find":"Speaker","replace":"Talk","scope":"{{scope}}"}]}""");

        CliResult edit = _workspace.Run(
            "slides", "edit", "cut.pptx", "--ops", "ops.json", "--out", "out.pptx", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
        JsonNode warning = JsonNode.Parse(edit.StdOut)!["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "OP_NO_EFFECT")!;
        Assert.Contains(operation, warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
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
        Assert.DoesNotContain("Use one of", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("truncated", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void SlidesMerge_SuggestsEdit()
    {
        CliResult merge = _workspace.Run("slides", "merge", "a.pptx", "b.pptx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(merge.StdErr)!["error"]!;
        Assert.Equal("slides edit", error["details"]!["suggestions"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public void ConvertedOdp_IsRecognizedAsAPresentationByReview()
    {
        CreateDeck(_workspace.File("deck.pptx"));
        CliResult convert = _workspace.Run("slides", "convert", "deck.pptx", "--to", "odp", "--out", "deck.odp", "--output-format", "json");
        Assert.True(convert.ExitCode == 0, convert.StdOut + convert.StdErr);

        using (var archive = System.IO.Compression.ZipFile.OpenRead(_workspace.File("deck.odp")))
        {
            // OpenDocument requires the mimetype first and stored, where content sniffing reads it.
            Assert.Equal("mimetype", archive.Entries[0].FullName);
            Assert.Equal(archive.Entries[0].Length, archive.Entries[0].CompressedLength);
        }

        CliResult review = _workspace.Run("review", "deck.odp", "--out", "odp-review", "--output-format", "json");
        Assert.True(review.ExitCode is 0 or 8, review.StdOut + review.StdErr);
    }

    [Fact]
    public void EvaluationReview_LaysOutTheWholeTextRatherThanTheCutText()
    {
        // Evaluation mode reads each line as its first characters, which would fit the box.
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 300, 300, 40).TextFrame.Text =
                string.Join("\n", Enumerable.Range(1, 6).Select(static line => $"Line {line} of a long text"));
            presentation.Save(_workspace.File("tall.pptx"), SaveFormat.Pptx);
        }

        CliResult review = _workspace.Run("review", "tall.pptx", "--out", "tall-review", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdOut + review.StdErr);
        Assert.Contains("SLIDES_TEXT_OVERFLOWS_SHAPE", FindingCodes(JsonNode.Parse(review.StdOut)!));
    }
}
