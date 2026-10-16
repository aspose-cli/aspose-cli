using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesContractDriftTests
{
    [Theory]
    [InlineData("16x9", null, null, true)]
    [InlineData("4x3", null, null, true)]
    [InlineData(null, 800.0, 450.0, true)]
    [InlineData(null, 800.5, 450.25, true)]
    [InlineData(null, 72.0, 7200.0, true)]
    [InlineData("16x9", 800.0, 450.0, false)]
    [InlineData(null, 800.0, null, false)]
    [InlineData(null, null, 450.0, false)]
    [InlineData(null, null, null, false)]
    [InlineData("a4", null, null, false)]
    [InlineData(null, 71.0, 450.0, false)]
    [InlineData(null, 800.0, 7201.0, false)]
    public void SetSlideSize_AcceptsAPresetOrBothSides(string? preset, double? width, double? height, bool valid)
    {
        var batch = new SlidesOpsBatch
        {
            Ops = [new SetSlideSizeOp { SlideSize = new SlidesSlideSizeInput { Preset = preset, Width = width, Height = height } }],
        };

        if (valid)
        {
            Assert.Single(SlidesOp.Catalog.Prepare(batch).Ops);
        }
        else
        {
            Assert.Equal(ErrorCodes.OpsInvalid, Assert.Throws<CliException>(() => SlidesOp.Catalog.Prepare(batch)).Code);
        }
    }

    [Theory]
    [InlineData("4x3", null, null, "4x3", 720.0, 540.0)]
    [InlineData("16x9", null, null, "16x9", 720.0, 405.0)]
    [InlineData(null, 800.0, 450.0, null, 800.0, 450.0)]
    public void SetSlideSize_ReadsBackAsTheSlideSizeInspectReports(
        string? preset, double? width, double? height, string? readPreset, double readWidth, double readHeight)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("size.pptx", slides: 1);
        string output = fixture.File("resized.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetSlideSizeOp { SlideSize = new SlidesSlideSizeInput { Preset = preset, Width = width, Height = height } }],
            },
            Output = TestOutput.At(output),
        });

        SlidesSlideSizeInfo size = SlidesInfo.Run(fixture.Session, new SlidesInspectRequest { Input = output }).Presentation.SlideSize;

        Assert.Equal(readPreset, size.Preset);
        Assert.Equal(readWidth, size.Width, 2);
        Assert.Equal(readHeight, size.Height, 2);
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

    [Fact]
    public void InspectMedia_OverTheListLimit_DisclosesTheCap()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("many-media.pptx", slides: 1);
        byte[] png = SlidesEngineFixture.Png(8);
        int count = SlidesInfo.MediaListLimit + 1;
        using (var deck = new Presentation(input))
        {
            // Trailing bytes after IEND keep each image distinct without re-rendering it; a picture
            // frame shows each one so the saved file keeps it.
            for (int index = 0; index < count; index++)
            {
                IPPImage image = deck.Images.AddImage([.. png, .. BitConverter.GetBytes(index)]);
                deck.Slides[0].Shapes.AddPictureFrame(ShapeType.Rectangle, index, index, 8, 8, image);
            }

            deck.Save(input, SaveFormat.Pptx);
        }

        SlidesInspectResult info = SlidesInfo.Run(fixture.Session, new SlidesInspectRequest { Input = input, Details = ["media"] });

        Assert.Equal(count, info.Presentation.MediaCount);
        Assert.Equal(SlidesInfo.MediaListLimit, info.Media!.Count);
        Warning warning = Assert.Single(info.Warnings ?? [], static warning => warning.Code == WarningCodes.ListTruncated);
        Assert.Equal("media", warning.Location);
        Assert.Contains("--what media", warning.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void InsertImage_SetsAltTextThatShapeReadsReport()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("alt.pptx", slides: 1);
        string picture = fixture.File("logo.png");
        File.WriteAllBytes(picture, SlidesEngineFixture.Png(40));
        string output = fixture.File("alt.out.pptx");

        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SlidesInsertImageOp { Slide = 1, Path = picture, AltText = "Logo" }] },
            Output = TestOutput.At(output),
        });
        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = output, Content = SlidesReadContents.Shapes });

        SlidesShapeData image = Assert.Single(Assert.Single(read.Slides).Shapes, static shape => shape.Kind == "image");
        Assert.Equal("Logo", image.AltText);
    }
}
