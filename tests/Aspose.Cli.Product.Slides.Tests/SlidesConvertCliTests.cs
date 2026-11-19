using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What <c>slides convert</c> and <c>slides render</c> write through the CLI: hidden slides, which
/// a render draws; the speaker notes, which a PDF export leaves out; the slide numbers of selected
/// slides; and the files each output format writes.
/// </summary>
public sealed class SlidesConvertCliTests : IDisposable
{
    private static readonly string[] Titles = ["One", "Two", "Three", "Four"];

    private readonly TempWorkspace _workspace = new();

    public SlidesConvertCliTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A render draws a hidden slide, which a slide show and convert leave out, whether it renders
    /// every slide or --slides selects the hidden one.
    /// </summary>
    [Theory]
    [InlineData(null, 4)]
    [InlineData("2", 1)]
    public void Render_DrawsAHiddenSlide(string? slides, int drawn)
    {
        Deck(hidden: 2);
        string[] selection = slides is null ? [] : ["--slides", slides];

        JsonNode result = Run(["slides", "render", "deck.pptx", .. selection, "--out", "slide.png"]);

        Assert.Equal(drawn, result["outputs"]!.AsArray().Count);
    }

    /// <summary>A PDF export holds the slides alone: the speaker notes are not in it.</summary>
    [Fact]
    public void PdfExport_LeavesSpeakerNotesOut()
    {
        Deck(notes: "Zq9x");

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
    /// A Markdown conversion writes only its output: nothing appears in the working directory,
    /// for every slide or for a selection.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public void Markdown_WritesNothingIntoTheWorkingDirectory(string? slides)
    {
        Deck();
        Directory.CreateDirectory(_workspace.File("out"));
        string[] before = [.. Directory.EnumerateFileSystemEntries(_workspace.Path).Order(StringComparer.Ordinal)];

        Run(["slides", "convert", "deck.pptx", "--to", "md", "--out", Path.Combine("out", "deck.md"), .. slides is null ? Array.Empty<string>() : ["--slides", slides]]);

        Assert.Equal(before, Directory.EnumerateFileSystemEntries(_workspace.Path).Order(StringComparer.Ordinal));
        Assert.Equal([_workspace.File(Path.Combine("out", "deck.md"))], Directory.EnumerateFileSystemEntries(_workspace.File("out")));
    }

    /// <summary>Each render format writes its own kind of image: a PNG, a JPEG, a TIFF or an SVG document.</summary>
    [Theory]
    [InlineData("png", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData("jpeg", new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData("tiff", new byte[] { 0x49, 0x49, 0x2A, 0x00 })]
    [InlineData("svg", null)]
    public void Render_WritesTheImageOfItsFormat(string format, byte[]? signature)
    {
        Deck();

        JsonNode result = Run("slides", "render", "deck.pptx", "--slides", "1", "--to", format, "--out", $"slide.{format}");

        byte[] content = File.ReadAllBytes(result["outputs"]![0]!["path"]!.GetValue<string>());
        if (signature is null)
        {
            Assert.Contains("<svg", System.Text.Encoding.UTF8.GetString(content), StringComparison.Ordinal);
            return;
        }

        Assert.Equal(signature, content[..signature.Length]);
    }

    /// <summary>
    /// A convert to TIFF, a format only render writes, is refused before anything is written, on a
    /// deck at a path with a space, for a selection, and on an encrypted deck.
    /// </summary>
    [Theory]
    [InlineData("with space", null, false)]
    [InlineData("plain", "2-3", false)]
    [InlineData("locked", "2", true)]
    public void TiffConvert_IsRefusedBeforeAnythingIsWritten(string folder, string? slides, bool encrypted)
    {
        const string Variable = "SLIDES_TIFF_PASSWORD";
        var variables = new Dictionary<string, string?> { [Variable] = "Tiff-Secret-7c2e" };
        string deck = Path.Combine(folder, "deck.pptx");
        Directory.CreateDirectory(_workspace.File(folder));
        Deck(path: deck, password: encrypted ? variables[Variable] : null);
        string[] open = encrypted ? ["--password-env", Variable] : [];
        string[] select = slides is null ? [] : ["--slides", slides];

        CliResult refused = _workspace.RunWithEnv(variables, ["slides", "convert", deck, .. open, "--to", "tiff", .. select, "--output-format", "json"]);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(["deck.pptx"], Directory.EnumerateFiles(_workspace.File(folder)).Select(Path.GetFileName));
    }

    /// <summary>
    /// Aspose.Slides writes to standard output while it renders with font fallback rules (known
    /// issue SLIDES-FALLBACK-STDOUT); a conversion of East Asian text keeps the JSON result the
    /// only standard output.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void Convert_KeepsTheJsonResultTheOnlyStandardOutput()
    {
        using (var fixture = new SlidesEngineFixture())
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "问题与对策";
            presentation.Save(_workspace.File("deck.pptx"), SaveFormat.Pptx);
        }

        CliResult result = _workspace.Run("slides", "convert", "deck.pptx", "--to", "pdf", "--output-format", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.DoesNotContain("Updating of Inner rules", result.StdOut, StringComparison.Ordinal);
        Assert.NotNull(JsonNode.Parse(result.StdOut));
    }

    /// <summary>
    /// Saves a deck of four slides, each with its title (One to Four) and text (First to Fourth),
    /// one of them hidden, notes on the first, or a password, when they are given.
    /// </summary>
    private void Deck(int? hidden = null, string? notes = null, string path = "deck.pptx", string? password = null)
    {
        using var fixture = new SlidesEngineFixture();
        using var deck = new Presentation();
        string[] texts = ["First", "Second", "Third", "Fourth"];
        for (int index = 0; index < Titles.Length; index++)
        {
            ISlide slide = index == 0 ? deck.Slides[0] : deck.Slides.AddEmptySlide(deck.LayoutSlides[0]);
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 30, 600, 70).TextFrame.Text = Titles[index];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 120, 600, 70).TextFrame.Text = texts[index];
            slide.Hidden = hidden == index + 1;
        }

        if (notes is not null)
        {
            deck.Slides[0].NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = notes;
        }

        if (password is not null)
        {
            deck.ProtectionManager.Encrypt(password);
        }

        deck.Save(_workspace.File(path), SaveFormat.Pptx);
    }

    private JsonNode Run(params string[] args)
    {
        CliResult result = _workspace.Run([.. args, "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }
}
