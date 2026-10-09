using System.IO.Compression;
using Aspose.Cli.Sdk.Views;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>A presentation that links external media never makes the CLI reach the network.</summary>
public sealed class SlidesResourceLoadingTests
{
    // Authoring a link makes the SDK itself try the address, so the deck names an address
    // nothing listens on and only its saved relationships are pointed at the counting server.
    private const string Unreachable = "http://127.0.0.1:9";

    [Fact]
    public async Task LinkedPictureAndVideo_AreNeverFetchedWhileReadingRenderingOrConverting()
    {
        using var fixture = new SlidesEngineFixture();
        await using var server = new ResourceHttpServer();
        string input = fixture.File("linked.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            IPPImage image = presentation.Images.AddImage(ResourceHttpServer.Image);
            IPictureFrame picture = slide.Shapes.AddPictureFrame(ShapeType.Rectangle, 20, 20, 120, 120, image);
            picture.PictureFormat.Picture.LinkPathLong = $"{Unreachable}/linked.png";
            slide.Shapes.AddVideoFrame(200, 20, 160, 120, $"{Unreachable}/clip.mp4");
            presentation.Save(input, SaveFormat.Pptx);
        }
        Assert.True(Relink(input, Unreachable, server.Url) >= 2, "The deck must keep both external links.");

        PresentationReadResult read = SlidesRead.Run(fixture.Session, new PresentationReadRequest { Input = input, Scope = PresentationReadScopes.Full });
        SlidesRenderResult rendered = SlidesExport.Render(fixture.Session, new PresentationRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("slide.png"), format: "png"),
        });
        SlidesConvertResult converted = SlidesExport.Convert(fixture.Session, new PresentationConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("linked.pdf"), format: "pdf"),
        });
        ViewManifest view = SlidesView.Render(
            fixture.Session,
            input,
            new ViewRenderRequest { View = SlidesViews.Slides, MaxPartCount = 1, Purpose = ViewPurpose.Display },
            new MemoryArtifactSink());

        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.NotEmpty(Assert.Single(read.Slides).Shapes);
        Assert.All(rendered.Outputs, static item => Assert.True(item.Output.SizeBytes > 0));
        Assert.All(converted.Outputs, static output => Assert.True(new FileInfo(output.Path).Length > 0));
        Assert.Equal(1, view.TotalPartCount);
        Assert.False(view.SourceEncrypted);
        // Output that needed the linked picture discloses that it was left out.
        Assert.Contains(rendered.Warnings!, IsOmission);
        Assert.Contains(converted.Warnings!, IsOmission);
        Assert.Contains(view.Warnings!, IsOmission);
    }

    [Fact]
    public void LinkedPictureBesideTheDeck_IsSuppliedAndOneOutsideItIsOmitted()
    {
        using var fixture = new SlidesEngineFixture();
        string directory = Directory.CreateDirectory(fixture.File("deck")).FullName;
        string input = Path.Combine(directory, "local.pptx");
        File.WriteAllBytes(Path.Combine(directory, "beside.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(fixture.File("outside.png"), ResourceHttpServer.Image);
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            IPPImage image = presentation.Images.AddImage(ResourceHttpServer.Image);
            slide.Shapes.AddPictureFrame(ShapeType.Rectangle, 20, 20, 120, 120, image)
                .PictureFormat.Picture.LinkPathLong = "beside.png";
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesRenderResult beside = SlidesExport.Render(fixture.Session, new PresentationRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("beside.png"), format: "png"),
        });
        Assert.DoesNotContain(beside.Warnings ?? [], IsOmission);

        using (var presentation = new Presentation(input))
        {
            ((IPictureFrame)presentation.Slides[0].Shapes[0]).PictureFormat.Picture.LinkPathLong = "../outside.png";
            presentation.Save(input, SaveFormat.Pptx);
        }
        SlidesRenderResult outside = SlidesExport.Render(fixture.Session, new PresentationRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("outside-render.png"), format: "png"),
        });
        Assert.Contains(outside.Warnings!, IsOmission);
    }

    private static bool IsOmission(Warning warning) =>
        warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness;

    /// <summary>Points the external relationships of a package at another origin; returns how many changed.</summary>
    private static int Relink(string package, string from, string to)
    {
        int changed = 0;
        using ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Update);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(static entry => entry.FullName.EndsWith(".rels", StringComparison.Ordinal)).ToArray())
        {
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }
            int count = (xml.Length - xml.Replace(from, string.Empty, StringComparison.Ordinal).Length) / from.Length;
            if (count == 0)
            {
                continue;
            }
            changed += count;
            using var writer = new StreamWriter(entry.Open());
            writer.BaseStream.SetLength(0);
            writer.Write(xml.Replace(from, to, StringComparison.Ordinal));
        }
        return changed;
    }
}
