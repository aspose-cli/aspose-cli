using Aspose.Cli.Sdk.Operations;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>The operations write what the reads report, under the same names and in the same direction.</summary>
public sealed class PdfOperationVocabularyTests
{
    [Fact]
    public void SetProperties_CustomEntriesReadBackUnderTheirKeys()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);

        string output = Edit(fixture, input, new SetPropertiesOp
        {
            Custom = new Dictionary<string, string> { ["Department"] = "Sales" },
        });

        IReadOnlyDictionary<string, string?> properties = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = output,
            Details = [PdfInspectDetails.Properties],
        }).Properties!;
        Assert.Equal("Sales", properties["Department"]);
    }

    [Fact]
    public void Hyperlink_AndAttachmentDescription_ReadBackAsTheOperationsWroteThem()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        string note = fixture.File("note.txt");
        File.WriteAllText(note, "note");

        string output = Edit(
            fixture,
            input,
            new AddHyperlinkOp { Page = 2, Rect = new PdfRectInput { X = 72, Y = 100, Width = 144, Height = 36 }, Url = "https://example.com/report" },
            new AddAttachmentOp { Path = note, Description = "Reviewer notes" });

        PdfInspectResult result = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = output,
            Details = [PdfInspectDetails.Links, PdfInspectDetails.Attachments],
        });
        PdfLinkInfo link = Assert.Single(result.Links!);
        Assert.Equal((2, "https://example.com/report"), (link.Page, link.Url));
        Assert.Equal((72d, 100d, 144d, 36d), (Math.Round(link.Rect.X), Math.Round(link.Rect.Y), Math.Round(link.Rect.Width), Math.Round(link.Rect.Height)));
        Assert.Equal("Reviewer notes", Assert.Single(result.Attachments!).Description);
    }

    [Fact]
    public void PageSize_ReadsBackAsThePageSizeTheOperationSet()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        using (var document = new Document(input))
        {
            // A crop box the page already has does not keep showing the old size.
            document.Pages[2].CropBox = new Rectangle(0, 0, 400, 500);
            document.Save(input);
        }

        string output = Edit(
            fixture,
            input,
            new InsertPageOp { At = 1, PageSize = new PageSizeInput { Width = 300, Height = 400 } },
            new SetPageSizeOp { Pages = "2", PageSize = new PageSizeInput { Preset = PageSizePresets.Letter } },
            new InsertPageOp { At = 4 });

        IReadOnlyList<PdfPageInfo> pages = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = output, IncludePreview = true }).Pages!;
        Assert.Equal((300d, 400d, (string?)null), (pages[0].PageSize.Width, pages[0].PageSize.Height, pages[0].PageSize.Preset));
        Assert.Equal((612d, 792d, PageSizePresets.Letter), (pages[1].PageSize.Width, pages[1].PageSize.Height, pages[1].PageSize.Preset));
        Assert.Equal(PageSizePresets.A4, pages[3].PageSize.Preset);
    }

    [Theory]
    [InlineData(45, false)]
    [InlineData(-45, true)]
    // The default turns the text as the engine's 45 degrees counterclockwise always did: rising.
    [InlineData(0, true)]
    public void WatermarkRotation_TurnsTheTextClockwise(double rotation, bool rises)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("blank.pdf", pages: 1);

        string output = Edit(fixture, input, rotation == 0
            ? new AddWatermarkTextOp { Text = "WIDE WATERMARK", Opacity = 1 }
            : new AddWatermarkTextOp { Text = "WIDE WATERMARK", Rotation = rotation, Opacity = 1 });

        using var document = new Document(output);
        var absorber = new TextFragmentAbsorber("WIDE WATERMARK");
        document.Pages[1].Accept(absorber);
        TextSegment segment = absorber.TextFragments.Cast<TextFragment>().SelectMany(static fragment => fragment.Segments.Cast<TextSegment>()).First();
        CharInfo first = segment.Characters[1];
        CharInfo last = segment.Characters[segment.Characters.Count];
        // The engine's y grows upward: text that rises from left to right ends higher than it starts.
        Assert.Equal(rises, last.Rectangle.LLY > first.Rectangle.LLY);
    }

    [Fact]
    public void CropPages_ReadsBackItsRectangleInTheFrameOfTheMediaBoxAsItIsSeen()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("pages.pdf");
        using (var document = new Document())
        {
            foreach (int index in Enumerable.Range(0, 4))
            {
                Page page = document.Pages.Add();
                page.SetPageSize(400, 600);
            }

            document.Pages[2].Rotate = Rotation.on90;
            // The third page already shows only part of its media box: 50 points in from the
            // left and from the top.
            document.Pages[3].CropBox = new Rectangle(50, 100, 350, 550);
            // The fourth is cropped and turned a quarter: its unrotated left edge is seen at the
            // top, so the crop starts 20 points in from the left and 50 down from the top.
            document.Pages[4].CropBox = new Rectangle(50, 20, 380, 550);
            document.Pages[4].Rotate = Rotation.on90;
            document.Save(input);
        }

        var rect = new PdfRectInput { X = 20, Y = 30, Width = 200, Height = 300 };
        string output = Edit(fixture, input, new CropPagesOp { Pages = "1-4", Rect = rect });

        IReadOnlyList<PdfPageInfo> pages = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = output, IncludePreview = true }).Pages!;
        static (double, double, double, double) Of(PdfRect box) => (Math.Round(box.X), Math.Round(box.Y), Math.Round(box.Width), Math.Round(box.Height));
        Assert.Equal((0d, 0d, 400d, 600d), Of(pages[0].MediaBox));
        Assert.Equal((20d, 30d, 200d, 300d), Of(pages[0].CropBox));
        Assert.Equal((0d, 0d, 600d, 400d), Of(pages[1].MediaBox));
        Assert.Equal((20d, 30d, 200d, 300d), Of(pages[1].CropBox));
        // The rectangle is measured on the page the crop sees, so it lands inside the earlier crop.
        Assert.Equal((70d, 80d, 200d, 300d), Of(pages[2].CropBox));
        Assert.Equal((0d, 0d, 600d, 400d), Of(pages[3].MediaBox));
        Assert.Equal((40d, 80d, 200d, 300d), Of(pages[3].CropBox));
        Assert.All(pages, static page => Assert.Equal((200d, 300d), (page.PageSize.Width, page.PageSize.Height)));
    }

    [Fact]
    public void WatermarkImage_DrawsTheImageAtItsScalePercent()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("blank.pdf", pages: 2);
        string image = fixture.File("mark.png");
        using (var bitmap = new SkiaSharp.SKBitmap(200, 100))
        {
            bitmap.Erase(SkiaSharp.SKColors.SteelBlue);
            using SkiaSharp.SKData png = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(image, png.ToArray());
        }

        string output = Edit(
            fixture,
            input,
            new AddWatermarkImageOp { Path = image, Pages = "1", ScalePercent = 50 },
            new AddWatermarkImageOp { Path = image, Pages = "2", ScalePercent = 100 });

        using var document = new Document(output);
        double Width(int number)
        {
            var absorber = new ImagePlacementAbsorber();
            document.Pages[number].Accept(absorber);
            return Assert.Single(absorber.ImagePlacements.Cast<ImagePlacement>()).Rectangle.Width;
        }

        // At 100 percent the image keeps its own size, which fits the page; a zoom of 100 would not.
        Assert.InRange(Width(2), 1, document.Pages[2].GetPageRect(considerRotation: true).Width);
        Assert.Equal(2, Width(2) / Width(1), 2);
    }

    [Fact]
    public void Links_ListsTheFirstLinksAndSaysWhenThereAreMore()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 1);
        using (var document = new Document(path))
        {
            for (int index = 0; index <= PdfInspectResult.LinkLimit; index++)
            {
                document.Pages[1].Annotations.Add(new Aspose.Pdf.Annotations.LinkAnnotation(document.Pages[1], new Rectangle(10, 10, 60, 20))
                {
                    Action = new Aspose.Pdf.Annotations.GoToURIAction($"https://example.com/{index}"),
                });
            }

            document.Save(path);
        }

        PdfInspectResult result = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = [PdfInspectDetails.Links] });

        Assert.Equal(PdfInspectResult.LinkLimit, result.Links!.Count);
        Assert.Equal("https://example.com/0", result.Links[0].Url);
        Warning truncated = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.ListTruncated);
        Assert.Equal("links", truncated.Details!["list"]!.GetValue<string>());
        Assert.Contains($"{PdfInspectResult.LinkLimit + 1}", truncated.Message, StringComparison.Ordinal);
    }

    private static string Edit(PdfEngineFixture fixture, string input, params PdfOp[] operations)
    {
        string output = fixture.File("edited.pdf");
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = operations },
            Output = TestOutput.At(output),
        });
        return output;
    }
}
