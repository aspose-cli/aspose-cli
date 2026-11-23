using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCoreWorkflowTests
{
    [Fact]
    public void CreateSearchAndExtract_CoverCoreArtifactFlows()
    {
        using var fixture = new SlidesEngineFixture();
        string presentation = fixture.File("briefing.pptx");

        SlidesCreateResult created = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest
        {
            Output = TestOutput.At(presentation),
        });

        Assert.True(File.Exists(presentation));
        Assert.Equal(1, created.SlideCount);
        Assert.Empty(created.Inputs);

        string searchable = fixture.CreatePresentation("searchable.pptx", slides: 1);

        SlidesSearchResult search = SlidesSearch.Run(
            fixture.Session,
            new TableSearchRequest
            {
                Input = searchable,
                Query = new SearchQuery(TextSearch.Create("Slide", regex: false, caseSensitive: true), 1),
            });
        TableHit hit = Assert.Single(search.Hits);
        Assert.Equal("Slide", hit.Text);
        Assert.Equal("text", hit.Row);

        string media = fixture.File("media.pptx");
        using (var deck = new Presentation())
        {
            IPPImage image = deck.Images.AddImage(SlidesEngineFixture.Png(16));
            deck.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, 10, 10, 50, 50, image);
            deck.Save(media, SaveFormat.Pptx);
        }

        SlidesExtractResult extracted = SlidesExtract.Run(
            fixture.Session,
            new SlidesExtractRequest
            {
                Input = media,
                Output = new ResolvedDirectory(fixture.File("media")),
                What = SlidesExtractKinds.Media,
            });
        SlidesExtractedOutput item = Assert.Single(extracted.Outputs);
        Assert.Equal(1, item.Index);
        Assert.True(File.Exists(item.Path));
    }

    /// <summary>
    /// A search its hit budget cuts short names, in <c>window.next</c>, the <c>--skip</c> that
    /// continues it: the matches passed over and the hits returned.
    /// </summary>
    [Fact]
    public void Search_CutShort_NamesTheSkipThatContinuesIt()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("deck.pptx", slides: 3);

        SlidesSearchResult cut = SlidesSearch.Run(fixture.Session, Search(input, skip: 1));
        SlidesSearchResult last = SlidesSearch.Run(fixture.Session, Search(input, skip: 2));

        Assert.True(cut.Window.Truncated);
        Assert.Equal(new Dictionary<string, string> { ["skip"] = "2" }, cut.Window.Next);
        Assert.False(last.Window.Truncated);
        Assert.Null(last.Window.Next);

        static TableSearchRequest Search(string input, int skip) => new()
        {
            Input = input,
            Query = new SearchQuery(TextSearch.Create("Slide", regex: false, caseSensitive: true), 1, skip),
        };
    }

    [Fact]
    public void ExtractMedia_WithSlides_WritesOnlyTheMediaThoseSlidesShow()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("media.pptx", slides: 2);
        byte[] first = SlidesEngineFixture.Png(100);
        byte[] second = SlidesEngineFixture.Png(200);
        using (var deck = new Presentation(input))
        {
            IPPImage picture = deck.Images.AddImage(first);
            deck.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, 10, 10, 50, 50, picture);
            IPPImage background = deck.Images.AddImage(second);
            deck.Slides[1].Background.Type = BackgroundType.OwnBackground;
            deck.Slides[1].Background.FillFormat.FillType = FillType.Picture;
            deck.Slides[1].Background.FillFormat.PictureFillFormat.Picture.Image = background;
            deck.Save(input, SaveFormat.Pptx);
        }

        SlidesExtractResult all = SlidesExtract.Run(fixture.Session, new SlidesExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("all")),
            What = SlidesExtractKinds.Media,
        });
        SlidesExtractResult selected = SlidesExtract.Run(fixture.Session, new SlidesExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("selected")),
            What = SlidesExtractKinds.Media,
            Slides = PageRange.Parse("2"),
        });

        Assert.Equal(2, all.Outputs.Count);
        SlidesExtractedOutput item = Assert.Single(selected.Outputs);
        Assert.Equal(2, item.Index);
        Assert.Equal(File.ReadAllBytes(all.Outputs[1].Path), File.ReadAllBytes(item.Path));
    }

    [Theory]
    [InlineData("pptx")]
    [InlineData("pdf")]
    [InlineData("gif")]
    [InlineData("md")]
    public void Convert_ProducesOneRepresentativeFromEachExporterFamily(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("convert.pptx", slides: 2);
        string output = fixture.File("converted" + SlidesFormats.Definitions.ExtensionOf(format));

        SlidesConvertResult result = fixture.Disclosed(session => SlidesExport.Convert(
            session,
            new SlidesConvertRequest
            {
                Input = input,
                Output = TestOutput.At(output, format: format),
                Slides = PageRange.Parse("1"),
            }));

        SlidesConvertOutput artifact = Assert.Single(result.Outputs);
        Assert.Equal(format, artifact.Format);
        Assert.True(File.Exists(artifact.Path));
        Assert.True(artifact.SizeBytes > 0);
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Equal(LicenseModes.Evaluation, result.License?.Mode);
        }
    }
}
