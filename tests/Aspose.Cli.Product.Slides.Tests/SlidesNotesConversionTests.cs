using System.IO.Compression;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// An export holds the slides alone, so a convert to one warns <c>LOSSY_CONVERSION</c> with the
/// numbers of the converted slides whose speaker notes it leaves out; a presentation format keeps
/// the notes and does not warn. Evaluation mode reads text longer than five characters cut short,
/// so every text here is at most five characters long.
/// </summary>
public sealed class SlidesNotesConversionTests
{
    private const string NotesText = "Nq7z";

    /// <summary>
    /// An export warns once, names slide 2 and leaves its notes out; a presentation format keeps
    /// the notes and does not warn. A TIFF holds slide 2 alone (SLIDES-TIFF-ONE-FRAME).
    /// </summary>
    [Theory]
    [InlineData("pdf", "pdf", true, null)]
    [InlineData("xps", "xps", true, null)]
    [InlineData("html", "html", true, null)]
    [InlineData("html5", "html", true, null)]
    [InlineData("md", "md", true, null)]
    [InlineData("tiff", "tiff", true, "2")]
    [InlineData("gif", "gif", true, null)]
    [InlineData("pptx", "pptx", false, null)]
    [InlineData("pptm", "pptm", false, null)]
    [InlineData("ppt", "ppt", false, null)]
    [InlineData("odp", "odp", false, null)]
    public void Convert_WarnsWhenItsFormatLeavesTheSpeakerNotesOut(string format, string extension, bool dropsNotes, string? slides)
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture, noted: 2);
        string output = fixture.File($"deck.{extension}");

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: format),
            Slides = slides is null ? null : PageRange.Parse(slides),
        });

        if (!dropsNotes)
        {
            Assert.Empty(NotesWarnings(result));
            using var converted = new Presentation(output);
            Assert.Equal(NotesText, converted.Slides[1].NotesSlideManager.NotesSlide?.NotesTextFrame?.Text?.Trim());
            return;
        }

        Warning warning = Assert.Single(NotesWarnings(result));
        Assert.Equal($"The {format} output does not include the speaker notes of slide(s) 2: an export holds the slides alone.", warning.Message);
        Assert.Contains("'slides read --slides 2 --notes'", warning.Hint, StringComparison.Ordinal);
        Assert.False(warning.AffectsCompleteness);
        // The text an output holds shows the slides' text but not the notes; an image is the one
        // of the same deck without notes. PdfExport_LeavesSpeakerNotesOut reads a PDF's text.
        if (Text(output, format) is { } text)
        {
            Assert.Contains("T2", text, StringComparison.Ordinal);
            Assert.DoesNotContain(NotesText, text, StringComparison.Ordinal);
        }
        else if (format is "tiff" or "gif")
        {
            string plain = fixture.File($"plain.{extension}");
            SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
            {
                Input = Deck(fixture, noted: null, name: "plain.pptx"),
                Output = TestOutput.At(plain, format: format),
                Slides = slides is null ? null : PageRange.Parse(slides),
            });
            Assert.Equal(File.ReadAllBytes(plain), File.ReadAllBytes(output));
        }
    }

    /// <summary>The notes warning names its kind of content, the speaker notes, in <c>details.content</c>.</summary>
    [Theory]
    [InlineData("pdf")]
    [InlineData("md")]
    public void Warning_NamesTheSpeakerNotesAsItsKindOfContent(string format)
    {
        using var fixture = new SlidesEngineFixture();

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = Deck(fixture, noted: 2),
            Output = TestOutput.At(fixture.File($"deck.{format}"), format: format),
        });

        Warning warning = Assert.Single(NotesWarnings(result));
        Assert.Equal("""{"content":["speaker-notes"]}""", warning.Details?.ToJsonString());
    }

    /// <summary>
    /// The warning names only the converted slides with notes: a deck without notes (an empty
    /// notes page included) and a selection of slides without notes give none.
    /// </summary>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(2, "1,3", null)]
    [InlineData(2, "2-3", "2")]
    [InlineData(3, "1-2", null)]
    [InlineData(3, null, "3")]
    public void Warning_NamesOnlyTheConvertedSlidesThatHaveNotes(int? noted, string? slides, string? expected)
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture, noted);

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("deck.pdf"), format: "pdf"),
            Slides = slides is null ? null : PageRange.Parse(slides),
        });

        Warning[] warnings = [.. NotesWarnings(result)];
        if (expected is null)
        {
            Assert.Empty(warnings);
            return;
        }

        Assert.Contains($"slide(s) {expected}:", Assert.Single(warnings).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A convert of every slide leaves a hidden slide out, so the notes warning names only the
    /// converted slides with notes, not the hidden one, whose notes HIDDEN_SLIDES_SKIPPED covers.
    /// </summary>
    [Fact]
    public void Warning_LeavesOutAHiddenSlideTheExportSkips()
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture, noted: 2);
        using (var deck = new Presentation(input))
        {
            deck.Slides[2].NotesSlideManager.NotesSlide.NotesTextFrame.Text = NotesText;
            deck.Slides[2].Hidden = true;
            deck.Save(input, SaveFormat.Pptx);
        }

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("deck.pdf"), format: "pdf"),
        });

        Warning skipped = Assert.Single(result.Warnings ?? [], static warning => warning.Code == SlidesDiagnostics.HiddenSlidesSkipped);
        Assert.Contains("hidden slide(s) 3,", skipped.Message, StringComparison.Ordinal);
        Assert.Equal(
            "The pdf output does not include the speaker notes of slide(s) 2: an export holds the slides alone.",
            Assert.Single(NotesWarnings(result)).Message);
    }

    private static IEnumerable<Warning> NotesWarnings(SlidesConvertResult result) =>
        (result.Warnings ?? []).Where(static warning =>
            warning.Code == WarningCodes.LossyConversion && warning.Message.Contains("speaker notes", StringComparison.Ordinal));

    /// <summary>
    /// A three-slide deck whose slides show T1, T2 and T3; every slide has a notes page, which on
    /// slide <paramref name="noted"/> holds the notes and on the others is empty.
    /// </summary>
    private static string Deck(SlidesEngineFixture fixture, int? noted, string name = "noted.pptx")
    {
        string path = fixture.File(name);
        using var presentation = new Presentation();
        for (int number = 1; number <= 3; number++)
        {
            ISlide slide = number == 1 ? presentation.Slides[0] : presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = $"T{number}";
            slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = number == noted ? NotesText : string.Empty;
        }

        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }

    /// <summary>The text an output holds where it can be read as text: HTML and Markdown, and the page markup of an XPS package.</summary>
    private static string? Text(string path, string format) => format switch
    {
        "html" or "html5" or "md" => File.ReadAllText(path),
        "xps" => XpsMarkup(path),
        _ => null,
    };

    private static string XpsMarkup(string path)
    {
        using ZipArchive package = ZipFile.OpenRead(path);
        return string.Concat(package.Entries
            .Where(static entry => entry.FullName.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase))
            .Select(static entry =>
            {
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            }));
    }
}
