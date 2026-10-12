using Aspose.Cli.Product.Slides.Commands;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>What <c>slides read</c> and <c>slides inspect</c> report back under the names the operations write.</summary>
public sealed class SlidesReadBackTests
{
    [Fact]
    public void InsertShape_EveryGeometry_ReadsBackAsTheShapesGeometry()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("geometry.pptx");
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(input) });
        string[] geometries =
        [
            SlidesShapeGeometries.Rectangle,
            SlidesShapeGeometries.RoundedRectangle,
            SlidesShapeGeometries.Ellipse,
            SlidesShapeGeometries.Line,
            SlidesShapeGeometries.Chevron,
        ];
        string output = fixture.File("geometry.out.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [.. geometries.Select((geometry, index) => (SlidesOp)new InsertShapeOp
                {
                    Slide = 1,
                    Geometry = geometry,
                    Rect = new SlidesRectInput { X = 20 + (index * 120), Y = 400, Width = 100, Height = 60 },
                })],
            },
            Output = TestOutput.At(output),
        });

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = output, Content = SlidesReadContents.Full });

        SlidesShapeData[] shapes = [.. read.Slides[0].Shapes.Where(static shape => !shape.EvaluationWatermark)];
        Assert.Equal(geometries, shapes.Where(static shape => shape.Placeholder is null).Select(static shape => shape.Geometry));
        SlidesShapeData[] placeholders = [.. shapes.Where(static shape => shape.Placeholder is not null)];
        Assert.NotEmpty(placeholders);
        Assert.All(placeholders, static shape => Assert.Null(shape.Geometry));
    }

    [Fact]
    public void Inspect_AnEmptySection_OmitsItsStartSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("sections.pptx");
        using (var presentation = new Presentation())
        {
            presentation.Sections.AddSection("Intro", presentation.Slides[0]);
            presentation.Sections.AppendEmptySection("Appendix");
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesInspectResult info = SlidesInfo.Run(fixture.Session, new SlidesInspectRequest
        {
            Input = input,
            Details = [SlidesInspectDetails.Sections],
        });

        Assert.Equal([1, null], info.Sections!.Select(static section => section.StartSlide));
        string text = RenderedText.Of(surface => InspectCommand.Table(info, surface), Aspose.Cli.Sdk.Extensibility.Output.TableFormat.Plain);
        string appendix = Assert.Single(text.Split('\n'), static line => line.StartsWith("Appendix", StringComparison.Ordinal));
        Assert.EndsWith(" -", appendix.TrimEnd());
    }

    [Fact]
    public void ShapeOperations_AcceptEveryPlaceholderRoleReadReports()
    {
        using var fixture = new SlidesEngineFixture();
        string input = Outline(fixture, "roles.pptx");
        string footed = fixture.File("roles.footer.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetFooterOp { Slides = "2", ShowNumber = true, ShowDate = true }] },
            Output = TestOutput.At(footed),
        });
        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = footed, Slides = PageRange.Parse("2") });
        string[] roles = [.. read.Slides[0].Shapes.Select(static shape => shape.Placeholder).OfType<string>()];
        Assert.Contains(SlidesPlaceholderRoles.SlideNumber, roles);

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = footed,
            Batch = new SlidesOpsBatch
            {
                Ops = [.. roles.Distinct(StringComparer.Ordinal).Select(static role => (SlidesOp)new FormatShapeOp
                {
                    Slide = 2,
                    Placeholder = role,
                    Style = new SlidesShapeStyleInput { FillColor = "#FFEEDD" },
                })],
            },
            Output = TestOutput.At(fixture.File("roles.out.pptx")),
        });

        Assert.All(result.Applied, static outcome => Assert.Equal(1, outcome.ItemsAffected));
    }

    [Fact]
    public void SetBody_ListsReadBackAsWritten()
    {
        using var fixture = new SlidesEngineFixture();
        string input = Outline(fixture, "lists.pptx");
        string output = fixture.File("lists.out.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SetBodyOp
                    {
                        Slide = 2,
                        Paragraphs =
                        [
                            new SlidesParagraphInput { Text = "Plain" },
                            new SlidesParagraphInput { Text = "Point", List = new SlidesListInput { Level = 1 } },
                            new SlidesParagraphInput { Text = "Step", List = new SlidesListInput { Kind = "number", Level = 1 } },
                            new SlidesParagraphInput { Text = "Inner", List = new SlidesListInput { Kind = "bullet", Level = 2 } },
                        ],
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest
        {
            Input = output,
            Slides = PageRange.Parse("2"),
            Content = SlidesReadContents.Full,
        });

        SlidesParagraphData[] paragraphs = Assert.Single(read.Slides[0].Shapes, static shape => shape.Placeholder == SlidesPlaceholderRoles.Body).Paragraphs!.ToArray();
        Assert.Equal(
            [null, "bullet/1", "number/1", "bullet/2"],
            paragraphs.Select(static paragraph => paragraph.List is { } list ? $"{list.Kind}/{list.Level}" : null));
    }

    [Fact]
    public void SetBody_ListItemWhereTheBodyDefinesNoBullet_StatesItsOwn()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("blank.pptx");
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].LayoutSlide = presentation.LayoutSlides.GetByType(SlideLayoutType.Blank);
            presentation.Save(input, SaveFormat.Pptx);
        }

        string output = fixture.File("blank.out.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SetBodyOp
                    {
                        Slide = 1,
                        Paragraphs = [new SlidesParagraphInput { Text = "Point", List = new SlidesListInput { Level = 1 } }],
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = output, Content = SlidesReadContents.Full });

        SlidesParagraphData paragraph = Assert.Single(
            read.Slides[0].Shapes.Where(static shape => !shape.EvaluationWatermark && shape.Paragraphs is not null).SelectMany(static shape => shape.Paragraphs!));
        Assert.Equal(("bullet", 1), (paragraph.List!.Kind, paragraph.List.Level));
    }

    private static string Outline(SlidesEngineFixture fixture, string name)
    {
        string markdown = fixture.File(name + ".md");
        File.WriteAllText(markdown, "# A\n\n## B\n\n- one\n\n## C\n\n- two\n");
        string path = fixture.File(name);
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(path), MarkdownPath = markdown });
        return path;
    }
}
