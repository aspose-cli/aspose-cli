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
