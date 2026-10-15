using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The addresses an edit reports name what the reads report: a section by its escaped name,
/// including a name with nothing in it (<c>section/%00</c>), and a shape by its slide's number and its id.
/// </summary>
public sealed class SlidesTargetNameTests
{
    [Fact]
    public void Edit_TargetsTheAddressesOfWhatTheReadsReport_EscapingTheSectionName()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("escaped.pptx");
        long shapeId;
        using (var presentation = new Presentation())
        {
            presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
            shapeId = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 100, 50).OfficeInteropShapeId;
            presentation.Sections.AddSection("Intro", presentation.Slides[0]);
            presentation.Save(input, SaveFormat.Pptx);
        }

        const string name = " Q3 / plan 100% ";
        string output = fixture.File("escaped.out.pptx");
        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertSectionOp { Id = "section", Name = name, At = 2 },
                    new SetTextOp { Id = "text", Slide = 1, ShapeId = shapeId, Text = "Hi" },
                ],
            },
            Output = TestOutput.At(output),
            Verify = true,
        });

        SlidesInspectResult inspect = SlidesInfo.Run(fixture.Session, new SlidesInspectRequest { Input = output, Details = [SlidesInspectDetails.Sections] });
        SlidesSectionInfo section = Assert.Single(inspect.Sections!, static section => section.Name.Contains("Q3", StringComparison.Ordinal));
        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = output, Content = SlidesReadContents.Shapes });
        SlidesShapeData shape = Assert.Single(read.Slides[0].Shapes, shape => shape.ShapeId == shapeId);
        Assert.Equal(name, section.Name);
        Assert.Equal(["section/%20Q3 %2F plan 100%25%20"], result.Applied[0].Targets);
        Assert.Equal([SlidesStateProjection.SectionAddress(section.Name)], result.Applied[0].Targets);
        Assert.Equal([SlidesStateProjection.ShapeAddress(read.Slides[0].Slide, shape.ShapeId)], result.Applied[1].Targets);
        Assert.True(result.Verification!.Ok);
        Assert.Contains("section", result.Verification.CheckedOps);
    }

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
        Assert.Equal("section/%00", SlidesStateProjection.SectionAddress(string.Empty));
    }

    [Fact]
    public void InsertSection_IntoADeckWithoutSections_TargetsTheNewSection()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("sectionless.pptx");
        string output = fixture.File("sectioned.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new InsertSectionOp { Name = "Results", At = 2 }] },
            Output = TestOutput.At(output),
        });

        Assert.Contains("section/Results", result.Applied[0].Targets);
    }
}
