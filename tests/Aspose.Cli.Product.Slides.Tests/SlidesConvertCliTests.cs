using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What <c>slides convert</c> writes and says through the CLI: hidden slides, which an export of
/// every slide leaves out with a warning and a selection converts; the slide numbers of selected
/// slides; the evaluation marks of the slides an output shows; and what an ODP output loses.
/// </summary>
public sealed class SlidesConvertCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public SlidesConvertCliTests()
    {
        ScenarioLicense.Project(_workspace.Path);
        File.WriteAllText(_workspace.File("deck.md"), "# One\n\nFirst\n\n# Two\n\nSecond\n\n# Three\n\nThird\n\n# Four\n\nFourth\n");
    }

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// An export of every slide leaves the hidden slide out, as PowerPoint's export does, and
    /// names it in a warning whose hint selects it; a presentation format keeps it, hidden.
    /// </summary>
    [Theory]
    [InlineData("pdf", 3)]
    [InlineData("md", 3)]
    [InlineData("html", 3)]
    [InlineData("pptx", 4)]
    public void EveryHiddenSlide_IsLeftOutOfAnExportWithAWarning(string format, int shown)
    {
        HiddenSecondSlide();

        JsonNode result = Run("slides", "convert", "deck.pptx", "--to", format, "--out", $"all.{format}");

        Assert.Equal(shown, ShownSlides(result, format));
        JsonNode? warning = (result["warnings"] as JsonArray ?? []).SingleOrDefault(static warning => warning!["code"]!.GetValue<string>() == "HIDDEN_SLIDES_SKIPPED");
        if (shown == 4)
        {
            Assert.Null(warning);
            return;
        }

        Assert.NotNull(warning);
        Assert.Contains("hidden slide(s) 2,", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("--slides 1-4", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>A hidden slide that --slides selects is converted, whatever the export format.</summary>
    [Theory]
    [InlineData("pdf")]
    [InlineData("md")]
    [InlineData("html")]
    [InlineData("html5")]
    public void SelectedHiddenSlide_IsConverted(string format)
    {
        HiddenSecondSlide();
        string output = format == "html5" ? "two.html" : $"two.{format}";

        JsonNode result = Run("slides", "convert", "deck.pptx", "--to", format, "--slides", "2", "--out", output);

        Assert.DoesNotContain(result["warnings"] as JsonArray ?? [], static warning => warning!["code"]!.GetValue<string>() == "HIDDEN_SLIDES_SKIPPED");
        if (format == "pdf")
        {
            Assert.Equal(1, Run("pdf", "inspect", output)["pdf"]!["pageCount"]!.GetValue<int>());
        }
        else
        {
            Assert.Contains("Two", File.ReadAllText(_workspace.File(output)), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A render draws a hidden slide, which a slide show and convert leave out, and names it in a
    /// warning, whether it renders every slide or --slides selects the hidden one.
    /// </summary>
    [Theory]
    [InlineData(null, 4)]
    [InlineData("2", 1)]
    public void Render_DrawsAHiddenSlideAndWarns(string? slides, int drawn)
    {
        HiddenSecondSlide();
        string[] selection = slides is null ? [] : ["--slides", slides];

        JsonNode result = Run(["slides", "render", "deck.pptx", .. selection, "--out", "slide.png"]);

        Assert.Equal(drawn, result["outputs"]!.AsArray().Count);
        JsonNode warning = Assert.Single(result["warnings"] as JsonArray ?? [], static warning => warning!["code"]!.GetValue<string>() == "HIDDEN_SLIDES_RENDERED")!;
        Assert.Contains("slide(s) 2 are hidden", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("""{"op":"set_slide_hidden","slides":"2","hidden":false}""", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>A render of slides none of which is hidden does not warn.</summary>
    [Fact]
    public void Render_OfShownSlidesDoesNotWarn()
    {
        HiddenSecondSlide();

        JsonNode result = Run("slides", "render", "deck.pptx", "--slides", "1,3", "--out", "slide.png");

        Assert.DoesNotContain("HIDDEN_SLIDES_RENDERED", Codes(result));
    }

    /// <summary>A review draws a hidden slide and names it in a warning, so its evidence is not read as the show.</summary>
    [Fact]
    public void Review_DrawsAHiddenSlideAndWarns()
    {
        HiddenSecondSlide();

        CliResult review = _workspace.Run("review", "deck.pptx", "--out", "evidence", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        JsonNode warning = Assert.Single(result["warnings"] as JsonArray ?? [], static warning => warning!["code"]!.GetValue<string>() == "HIDDEN_SLIDES_RENDERED")!;
        Assert.Contains("slide(s) 2 are hidden", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("""{"op":"set_slide_hidden","slides":"2","hidden":false}""", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>A PDF export holds the slides alone: the speaker notes are not in it.</summary>
    [Fact]
    public void PdfExport_LeavesSpeakerNotesOut()
    {
        Run("slides", "create", "deck.pptx", "--from-markdown", "deck.md");
        Run("slides", "edit", "deck.pptx", "--in-place", "--ops", """[{"op":"set_notes","slide":1,"text":"Zq9x"}]""");

        Run("slides", "convert", "deck.pptx", "--out", "deck.pdf");
        CliResult read = _workspace.Run("pdf", "read", "deck.pdf", "--output-format", "json");

        Assert.True(read.ExitCode is 0 or 8, read.StdErr);
        Assert.Equal(4, Run("pdf", "inspect", "deck.pdf")["pdf"]!["pageCount"]!.GetValue<int>());
        Assert.Contains("First", read.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Zq9x", read.StdOut, StringComparison.Ordinal);
    }

    /// <summary>The slide numbers of selected slides converted to Markdown are their numbers in the deck.</summary>
    [Fact]
    public void SelectedSlidesInMarkdown_KeepTheirSlideNumbers()
    {
        using (var fixture = new SlidesEngineFixture())
        using (var deck = new Presentation())
        {
            for (int slide = 1; slide < 4; slide++)
            {
                deck.Slides.AddEmptySlide(deck.Slides[0].LayoutSlide);
            }

            deck.HeaderFooterManager.SetAllSlideNumbersVisibility(true);
            deck.Save(_workspace.File("numbered.pptx"), SaveFormat.Pptx);
        }

        Run("slides", "convert", "numbered.pptx", "--to", "md", "--slides", "3-4", "--out", "numbered.md");

        string[] numbers = [.. File.ReadAllLines(_workspace.File("numbered.md")).Select(static line => line.Trim()).Where(static line => line is "1" or "2" or "3" or "4")];
        Assert.Equal(["3", "4"], numbers);
    }

    /// <summary>
    /// A licensed export of selected slides discloses only the evaluation marks those slides
    /// carry: a slide an evaluation save marked is disclosed, an unmarked one is not.
    /// </summary>
    [LicensedFact]
    public void SelectedSlides_DiscloseOnlyTheirOwnEvaluationMarks()
    {
        File.WriteAllText(_workspace.File("one.md"), "# One\n\nFirst\n");
        Run("slides", "create", "marked.pptx", "--from-markdown", "one.md", "--license-mode", "evaluation");
        Run("slides", "edit", "marked.pptx", "--in-place", "--ops", """[{"op":"insert_slide"}]""");

        Assert.Contains("EVALUATION_INPUT_MARKED", Codes(Run("slides", "convert", "marked.pptx", "--slides", "1", "--out", "first.pdf")));
        Assert.DoesNotContain("EVALUATION_INPUT_MARKED", Codes(Run("slides", "convert", "marked.pptx", "--slides", "2", "--out", "second.pdf")));
    }

    /// <summary>
    /// Evaluation mode writes no watermark into a Markdown conversion but cuts its longer text
    /// short: the <c>EVALUATION_MODE</c> message states the cut text and claims no watermark. A
    /// deck an unlicensed create saved carries watermark boxes of its own, which the message
    /// names as the output's content.
    /// </summary>
    [Fact]
    public void EvaluationMode_MarkdownConversion_StatesTheCutTextAndNoWatermark()
    {
        File.WriteAllText(_workspace.File("long.md"), "# Quarterly results\n\nRevenue grew strongly this quarter\n");
        Run("slides", "create", "long.pptx", "--from-markdown", "long.md");

        JsonNode result = Run("slides", "convert", "long.pptx", "--to", "md", "--out", "long.out.md", "--license-mode", "evaluation");

        string message = Assert.Single(result["warnings"] as JsonArray ?? [], static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_MODE")!["message"]!.GetValue<string>();
        Assert.StartsWith("Evaluation mode, requested with --license-mode evaluation: some operations are limited.", message, StringComparison.Ordinal);
        Assert.Contains("cut the content short", message, StringComparison.Ordinal);
        if (TestLicense.Path is not null)
        {
            Assert.DoesNotContain("watermark", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A .markdown output is the md format: an evaluation conversion of selected slides to it
    /// states the same cut text, and the same absence of a watermark, as one to .md.
    /// </summary>
    [Fact]
    public void EvaluationMode_SelectedSlidesToDotMarkdown_AreDisclosedAsMarkdown()
    {
        File.WriteAllText(_workspace.File("long.md"), "# Quarterly results\n\nRevenue grew strongly this quarter\n");
        Run("slides", "create", "long.pptx", "--from-markdown", "long.md");

        string[] messages = [.. new[] { "long.out.md", "long.out.markdown" }.Select(output => Assert.Single(
            Run("slides", "convert", "long.pptx", "--to", "md", "--slides", "1", "--out", output, "--license-mode", "evaluation")["warnings"] as JsonArray ?? [],
            static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_MODE")!["message"]!.GetValue<string>())];

        Assert.Contains("cut the content short", messages[0], StringComparison.Ordinal);
        Assert.Equal(messages[0], messages[1]);
    }

    /// <summary>
    /// A Markdown conversion writes only its output: nothing appears in the working directory,
    /// for every slide or for a selection.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public void Markdown_WritesNothingIntoTheWorkingDirectory(string? slides)
    {
        Run("slides", "create", "deck.pptx", "--from-markdown", "deck.md");
        Directory.CreateDirectory(_workspace.File("out"));
        string[] before = [.. Directory.EnumerateFileSystemEntries(_workspace.Path).Order(StringComparer.Ordinal)];

        Run(["slides", "convert", "deck.pptx", "--to", "md", "--out", Path.Combine("out", "deck.md"), .. slides is null ? Array.Empty<string>() : ["--slides", slides]]);

        Assert.Equal(before, Directory.EnumerateFileSystemEntries(_workspace.Path).Order(StringComparer.Ordinal));
        Assert.Equal([_workspace.File(Path.Combine("out", "deck.md"))], Directory.EnumerateFileSystemEntries(_workspace.File("out")));
    }

    /// <summary>
    /// Markdown keeps the bullets and levels a body placeholder inherits from its layout and
    /// master: a first-level item is a list item, and a second-level item one indented under it.
    /// </summary>
    [Fact]
    public void BodyPlaceholderBullets_BecomeANestedMarkdownList()
    {
        File.WriteAllText(_workspace.File("list.md"), "# Deck\n\n## Plan\n\n- Up\n  - Sub\n- Go\n");
        Run("slides", "create", "list.pptx", "--from-markdown", "list.md");

        Run("slides", "convert", "list.pptx", "--to", "md", "--slides", "2", "--out", "list.out.md");

        string[] lines = File.ReadAllLines(_workspace.File("list.out.md"));
        string first = Assert.Single(lines, static line => line.TrimStart().StartsWith("- Up", StringComparison.Ordinal));
        string second = Assert.Single(lines, static line => line.TrimStart().StartsWith("- Sub", StringComparison.Ordinal));
        Assert.Single(lines, static line => line.TrimStart().StartsWith("- Go", StringComparison.Ordinal));
        Assert.True(Indent(second) > Indent(first), string.Join(Environment.NewLine, lines));

        static int Indent(string line) => line.Length - line.TrimStart().Length;
    }

    /// <summary>An ODP output names the native charts, transitions and sections it does not keep.</summary>
    [Fact]
    public void OdpOutput_NamesTheChartsTransitionsAndSectionsItLoses()
    {
        Run("slides", "create", "deck.pptx", "--from-markdown", "deck.md");
        Run("slides", "edit", "deck.pptx", "--in-place", "--ops", """
            [{"op":"set_transition","slides":"1","kind":"fade"},
             {"op":"insert_section","name":"Part","at":2},
             {"op":"insert_chart","slide":2,"kind":"column","rect":{"x":50,"y":100,"width":300,"height":200},"categories":["a","b"],"series":[{"name":"S","values":[1,2]}]}]
            """);

        JsonNode result = Run("slides", "convert", "deck.pptx", "--out", "deck.odp");

        string message = Assert.Single(result["warnings"] as JsonArray ?? [], static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!["message"]!.GetValue<string>();
        Assert.Contains("native charts of slide(s) 2", message, StringComparison.Ordinal);
        Assert.Contains("transitions of slide(s) 1", message, StringComparison.Ordinal);
        Assert.Contains("section(s)", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An html, html5 or Markdown output warns that it may not keep every presentation feature;
    /// a presentation or a PDF does not.
    /// </summary>
    [Theory]
    [InlineData("html", "html", true)]
    [InlineData("html5", "html", true)]
    [InlineData("md", "md", true)]
    [InlineData("pptx", "pptx", false)]
    [InlineData("pdf", "pdf", false)]
    public void Convert_ToHtmlHtml5OrMd_WarnsLossyConversion(string format, string extension, bool lossy)
    {
        Run("slides", "create", "deck.pptx", "--from-markdown", "deck.md");

        JsonNode result = Run("slides", "convert", "deck.pptx", "--to", format, "--out", $"converted.{extension}");

        JsonNode? warning = (result["warnings"] as JsonArray ?? [])
            .SingleOrDefault(static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION");
        if (!lossy)
        {
            Assert.Null(warning);
            return;
        }

        Assert.NotNull(warning);
        Assert.Equal($"Slides conversion to {format} may not preserve every presentation feature.", warning["message"]!.GetValue<string>());
    }

    /// <summary>Each render format writes its own kind of image: a PNG, a JPEG or an SVG document.</summary>
    [Theory]
    [InlineData("png", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData("jpeg", new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData("svg", null)]
    public void Render_WritesTheImageOfItsFormat(string format, byte[]? signature)
    {
        Run("slides", "create", "deck.pptx", "--from-markdown", "deck.md");

        JsonNode result = Run("slides", "render", "deck.pptx", "--slides", "1", "--to", format, "--out", $"slide.{format}");

        byte[] content = File.ReadAllBytes(result["outputs"]![0]!["path"]!.GetValue<string>());
        if (signature is null)
        {
            Assert.Contains("<svg", System.Text.Encoding.UTF8.GetString(content), StringComparison.Ordinal);
            return;
        }

        Assert.Equal(signature, content[..signature.Length]);
    }

    // deck.pptx: four slides, the second hidden.
    private void HiddenSecondSlide()
    {
        Run("slides", "create", "deck.pptx", "--from-markdown", "deck.md");
        Run("slides", "edit", "deck.pptx", "--in-place", "--ops", """[{"op":"set_slide_hidden","slides":"2","hidden":true}]""");
    }

    // The slides an output shows: its pages or its slide headings.
    private int ShownSlides(JsonNode result, string format)
    {
        string path = result["outputs"]![0]!["path"]!.GetValue<string>();
        return format switch
        {
            "pdf" => Run("pdf", "inspect", path)["pdf"]!["pageCount"]!.GetValue<int>(),
            "pptx" => Run("slides", "inspect", path)["presentation"]!["slideCount"]!.GetValue<int>(),
            _ => new[] { "One", "Two", "Three", "Four" }.Count(title => File.ReadAllText(path).Contains(title, StringComparison.Ordinal)),
        };
    }

    private JsonNode Run(params string[] args)
    {
        CliResult result = _workspace.Run([.. args, "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    private static string[] Codes(JsonNode result) =>
        [.. (result["warnings"] as JsonArray ?? []).Select(static warning => warning!["code"]!.GetValue<string>())];
}
