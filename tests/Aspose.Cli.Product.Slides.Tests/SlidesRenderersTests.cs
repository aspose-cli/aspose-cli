using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesRenderersTests
{
    [Theory]
    [InlineData(TableFormat.Plain, "")]
    [InlineData(TableFormat.Markdown, "### ")]
    public void Info_ShowsEveryRequestedDetailUnderItsHeadingAndNoneWhenEmpty(TableFormat format, string heading)
    {
        var result = new PresentationInfoResult
        {
            Source = new SourceInfo { Path = "deck.pptx", Format = "pptx", SizeBytes = 10 },
            Presentation = new PresentationSummary
            {
                SlideCount = 1, WidthPoints = 720, HeightPoints = 540, Orientation = "landscape",
                MasterCount = 1, LayoutCount = 1, SectionCount = 1, CommentCount = 1, MediaCount = 1, HasMacros = false,
            },
            Slides = [],
            Sections = [new PresentationSectionInfo { Name = "Intro", SectionId = "s1", StartSlide = 1 }],
            Masters = [new PresentationMasterInfo { Name = "Office Theme", SlideCount = 1 }],
            Layouts = [new PresentationLayoutInfo { Name = "Title Slide", Master = "Office Theme", SlideCount = 1 }],
            Media = [new PresentationMediaInfo { Index = 0, Type = "image", ContentType = "image/png", SizeBytes = 2_048 }],
            Notes = [new PresentationNotesInfo { Slide = 1, Present = true, CharacterCount = 42 }],
            Comments = [new PresentationCommentInfo { Slide = 1, Author = "Ann", Text = "Check the date" }],
            Fonts = ["Calibri", "Arial"],
            Properties = new Dictionary<string, string?>(),
        };
        using var writer = new StringWriter();

        SlidesRenderers.Render(result, new TableSurface(writer, format));

        string text = writer.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        string colon = heading.Length == 0 ? ":" : string.Empty;
        foreach (string section in new[] { "sections", "masters", "layouts", "media", "notes", "comments", "fonts" })
        {
            Assert.Contains($"\n{heading}{section}{colon}\n", text, StringComparison.Ordinal);
        }

        foreach (string value in new[] { "Intro", "Office Theme", "Title Slide", "image/png", "42", "Check the date", "Calibri, Arial" })
        {
            Assert.Contains(value, text, StringComparison.Ordinal);
        }

        Assert.EndsWith($"\n{heading}properties{colon}\n{(heading.Length == 0 ? string.Empty : "\n")}none\n", text, StringComparison.Ordinal);
    }
}
