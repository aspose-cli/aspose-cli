using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>A section the presentation names with nothing is addressed <c>section/%00</c>, so an edit around it still runs.</summary>
public sealed class SlidesTargetNameTests
{
    [Fact]
    public void InsertSection_BesideASectionWithAnEmptyName_Applies()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("unnamed-section.pptx");
        using (var presentation = new Presentation())
        {
            presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
            presentation.Sections.AddSection(string.Empty, presentation.Slides[0]);
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new InsertSectionOp { Name = "Results", At = 2 }] },
            Output = TestOutput.At(fixture.File("unnamed-section.out.pptx")),
        });

        Assert.Equal(["section/Results"], Assert.Single(result.Applied).Targets);
        using var reopened = new Presentation(fixture.File("unnamed-section.out.pptx"));
        Assert.Equal([string.Empty, "Results"], reopened.Sections.Select(static section => section.Name));
        Assert.Equal("section/%00", SlidesSnapshots.SectionAddress(string.Empty));
    }
}
