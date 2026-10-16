using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed partial class SlidesConvertSelectionTests
{
    [Theory]
    [InlineData("pptx")]
    [InlineData("odp")]
    public void ConvertSelectedSlides_KeepsTheSourceSizePropertiesAndMasters(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("source.pptx", slides: 3);
        using (var source = new Presentation(input))
        {
            source.SlideSize.SetSize(800, 450, SlideSizeScaleType.EnsureFit);
            source.DocumentProperties.Title = "Deck";
            source.Masters[0].Name = "Brand";
            source.Save(input, SaveFormat.Pptx);
        }

        string output = fixture.File($"selected.{format}");
        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: format),
            Slides = PageRange.Parse("2-3"),
        });

        Assert.Equal("2-3", result.Slides);
        using var converted = new Presentation(output);
        Assert.Equal(2, converted.Slides.Count);
        Assert.Equal("slide-2", converted.Slides[0].Name);
        Assert.Equal(800f, converted.SlideSize.Size.Width, 0.1f);
        Assert.Equal(450f, converted.SlideSize.Size.Height, 0.1f);
        Assert.Equal("Deck", converted.DocumentProperties.Title);
        // ODP names its master pages itself; PPTX keeps the source master.
        Assert.Equal(format == "pptx" ? "Brand" : converted.Masters[0].Name, converted.Slides[0].LayoutSlide.MasterSlide.Name);
        Assert.Single(converted.Masters);
    }

    /// <summary>
    /// Markdown starts each slide with a slide-number comment that names the slide's number in
    /// the deck: for every slide, for a selection, and around a hidden slide an export leaves out.
    /// </summary>
    [Theory]
    [InlineData(null, null, new[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(null, "2-3", new[] { 2, 3 })]
    [InlineData(4, null, new[] { 1, 2, 3, 5, 6 })]
    [InlineData(4, "4-6", new[] { 4, 5, 6 })]
    public void Markdown_MarksEachSlideWithItsNumberInTheDeck(int? hidden, string? selection, int[] expected)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("numbered.pptx", slides: 6);
        using (var source = new Presentation(input))
        {
            // Titles short enough that evaluation mode does not cut them.
            foreach (ISlide slide in source.Slides)
            {
                ((IAutoShape)slide.Shapes[0]).TextFrame.Text = $"T{slide.SlideNumber}";
            }

            if (hidden is { } number)
            {
                source.Slides[number - 1].Hidden = true;
            }

            source.Save(input, SaveFormat.Pptx);
        }

        string output = fixture.File("numbered.md");
        SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "md"),
            Slides = selection is null ? null : PageRange.Parse(selection),
        });

        // The bytes are UTF-8 without a byte order mark, which would come before the marker.
        byte[] bytes = File.ReadAllBytes(output);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "The Markdown starts with a byte order mark.");
        string markdown = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        int[] marked = [.. SlideMarker().Matches(markdown).Select(static match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))];
        Assert.Equal(expected, marked);
        // The file opens with the first marker, not with a separator a Markdown tool would take
        // for YAML front matter, and each marker is followed by its own slide's text.
        Assert.StartsWith($"<!-- slide {expected[0]} -->", markdown, StringComparison.Ordinal);
        string[] parts = SlideMarker().Split(markdown);
        Assert.All(expected.Select((slide, index) => (slide, text: parts[(2 * index) + 2])), part =>
            Assert.Contains($"T{part.slide}", part.text, StringComparison.Ordinal));
        // Every slide but the first is a separator line, its marker and its content: no separator
        // follows a marker, where it would part the marker from its slide.
        string[] lines = [.. markdown.ReplaceLineEndings("\n").Split('\n').Select(static line => line.Trim())];
        int[] markers = [.. lines.Select((line, index) => (line, index)).Where(static line => SlideMarker().IsMatch(line.line)).Select(static line => line.index)];
        Assert.All(markers, marker => Assert.NotEqual("---", lines[marker + 1]));
        Assert.All(markers.Skip(1), marker => Assert.Equal("---", lines[..marker].Last(static line => line.Length > 0)));
    }

    /// <summary>
    /// Markdown writes a subtitle of two paragraphs as one second-level heading that keeps both
    /// lines, not as one heading for each.
    /// </summary>
    [Fact]
    public void Markdown_WritesASubtitleOfSeveralParagraphsAsOneHeading()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("subtitle.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides.AddEmptySlide(presentation.LayoutSlides.GetByType(SlideLayoutType.Title));
            presentation.Slides.RemoveAt(0);
            slide.Shapes.OfType<IAutoShape>().First(static shape => shape.Placeholder?.Type is PlaceholderType.CenteredTitle or PlaceholderType.Title).TextFrame.Text = "Deck";
            ITextFrame subtitle = slide.Shapes.OfType<IAutoShape>().First(static shape => shape.Placeholder?.Type == PlaceholderType.Subtitle).TextFrame;
            subtitle.Paragraphs[0].Text = "East";
            var date = new Paragraph();
            date.Portions.Add(new Portion("Q3"));
            subtitle.Paragraphs.Add(date);
            presentation.Save(input, SaveFormat.Pptx);
        }

        string output = fixture.File("subtitle.md");
        SlidesExport.Convert(fixture.Session, new SlidesConvertRequest { Input = input, Output = TestOutput.At(output, format: "md") });

        string heading = Assert.Single(File.ReadAllLines(output), static line => line.StartsWith("## ", StringComparison.Ordinal));
        if (fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains("East", heading, StringComparison.Ordinal);
            Assert.Contains("Q3", heading, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex(@"<!-- slide (\d+) -->")]
    private static partial Regex SlideMarker();

    [Fact]
    public void ConvertSelectedSlides_KeepsSourceEncryption()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("locked.pptx", slides: 2, password: "secret");
        string output = fixture.File("locked.selected.pptx");

        SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "pptx"),
            Slides = PageRange.Parse("1"),
            Password = new Secret("secret"),
        });

        IPresentationInfo info = PresentationFactory.Instance.GetPresentationInfo(output);
        Assert.True(info.IsPasswordProtected);
        Assert.True(info.CheckPassword("secret"));
    }
}
