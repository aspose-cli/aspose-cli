using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesContractDriftTests
{
    [Theory]
    [InlineData("800x450pt", true)]
    [InlineData("800.5x450.25pt", true)]
    [InlineData("72x7200pt", true)]
    [InlineData("800x450PT", false)]
    [InlineData("800 x 450pt", false)]
    [InlineData("71x450pt", false)]
    [InlineData("800x7201pt", false)]
    [InlineData("1e3x450pt", false)]
    public void SetSlideSize_AcceptsExactlyTheSchemaSpelling(string size, bool valid)
    {
        var batch = new SlidesOpsBatch { Ops = [new SetSlideSizeOp { Size = size }] };

        if (valid)
        {
            Assert.Single(SlidesOp.Catalog.Prepare(batch).Ops);
        }
        else
        {
            Assert.Equal(ErrorCodes.OpsInvalid, Assert.Throws<CliException>(() => SlidesOp.Catalog.Prepare(batch)).Code);
        }
    }

    [Fact]
    public void ExtractMedia_WithSlides_WritesOnlyTheMediaThoseSlidesShow()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("media.pptx", slides: 2);
        byte[] first = Png(100);
        byte[] second = Png(200);
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

        SlidesExtractResult all = fixture.Engine.Extract(input, new PresentationExtractRequest
        {
            What = PresentationExtractKinds.Media,
            OutputDirectory = fixture.File("all"),
        });
        SlidesExtractResult selected = fixture.Engine.Extract(input, new PresentationExtractRequest
        {
            What = PresentationExtractKinds.Media,
            OutputDirectory = fixture.File("selected"),
            Slides = PageRange.Parse("2"),
        });

        Assert.Equal(2, all.Items.Count);
        SlidesExtractedItem item = Assert.Single(selected.Items);
        Assert.Equal(2, item.Index);
        Assert.Equal(File.ReadAllBytes(all.Items[1].Path), File.ReadAllBytes(item.Path));
    }

    [Fact]
    public void InspectMedia_OverTheListLimit_DisclosesTheCap()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("many-media.pptx", slides: 1);
        byte[] png = Png(8);
        int count = SlidesReadService.MediaListLimit + 1;
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

        PresentationInfoResult info = fixture.Engine.GetInfo(input, new PresentationInfoRequest { Details = ["media"] });

        Assert.Equal(count, info.Presentation.Media);
        Assert.Equal(SlidesReadService.MediaListLimit, info.Media!.Count);
        Warning warning = Assert.Single(info.Warnings ?? [], static warning => warning.Code == WarningCodes.ListTruncated);
        Assert.Equal("media", warning.Location);
        Assert.Contains("--what media", warning.Hint, StringComparison.Ordinal);
    }

    private static byte[] Png(int size)
    {
        using var source = new Presentation();
        source.SlideSize.SetSize(size, size, SlideSizeScaleType.DoNotScale);
        using IImage image = source.Slides[0].GetImage(1f, 1f);
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
