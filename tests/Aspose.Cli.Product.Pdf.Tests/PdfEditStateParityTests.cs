using Aspose.Cli.Product.Pdf.Engine.Editing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An edit compares what the document stores, which can be finer than what a read reports:
/// blank document information and a box moved by less than a read rounds away count as changes,
/// and a verification reports the stored text, the field order and the bookmark destination.
/// </summary>
public sealed class PdfEditStateParityTests
{
    [Fact]
    public void ClearProperties_RemovesABlankEntry()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("blank-title.pdf");
        using (var document = new Document())
        {
            document.Pages.Add();
            document.Info.Title = "  ";
            document.Save(input);
        }

        PdfEditResult result = Edit(fixture, input, new ClearPropertiesOp { Xmp = false });

        Assert.Contains("property/title", result.Applied[0].Targets);
    }

    [Fact]
    public void CropPages_ByLessThanAReadRoundsChangesThePage()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("crop.pdf", pages: 1);
        double width;
        double height;
        using (var document = new Document(input))
        {
            width = document.Pages[1].CropBox.Width;
            height = document.Pages[1].CropBox.Height;
        }

        PdfEditResult result = Edit(fixture, input, new CropPagesOp
        {
            Pages = "1",
            Rect = new PdfRectInput { X = 0, Y = 0, Width = width - 0.0001, Height = height },
        });

        Assert.Equal(1, result.Applied[0].ItemsAffected);
        Assert.Equal(["page/1"], result.Applied[0].Targets);
    }

    [Fact]
    public void Verify_ReportsTheStoredTextOfAnEntry()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document();
        document.Pages.Add();
        document.Info["Reviewed"] = "  ";

        string message = Assert.Single(Verify(fixture, document, new SetPropertiesOp
        {
            Id = "op-0001",
            Custom = new Dictionary<string, string> { ["Reviewed"] = "yes" },
        })).Message;

        Assert.Contains("reads '  ' in the output", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ListsTheFieldsLeftInDocumentOrder()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document();
        Page page = document.Pages.Add();
        document.Form.Add(new TextBoxField(page, new Rectangle(72, 700, 200, 720)) { PartialName = "b" });
        document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 200, 670)) { PartialName = "a" });

        string message = Assert.Single(Verify(fixture, document, new FlattenFormsOp { Id = "op-0001" })).Message;

        Assert.Contains("'b', 'a'", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ReportsTheStoredDestinationOfABookmark()
    {
        using var fixture = new PdfEngineFixture();
        using var document = new Document();
        document.Pages.Add();
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Other" });

        // The recorded bookmark is not in the document, which also counts one bookmark too few.
        IReadOnlyList<VerificationIssue> issues = Verify(fixture, document, new AddBookmarkOp { Id = "op-0001", Title = "T", Page = 1 });

        Assert.Contains(issues, static issue => issue.Message.Contains("is 'Other' on page 0, not 'T' on page 1", StringComparison.Ordinal));
    }

    private static PdfEditResult Edit(PdfEngineFixture fixture, string input, params PdfOp[] ops) =>
        PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.File(Path.GetFileNameWithoutExtension(input) + ".out.pdf")),
        });

    /// <summary>The issues of verifying <paramref name="document"/> after recording <paramref name="op"/> against it.</summary>
    private static IReadOnlyList<VerificationIssue> Verify(PdfEngineFixture fixture, Document document, PdfOp op)
    {
        var verifier = new PdfEditVerifier(document);
        verifier.Record(op, AppliedOperations.Changed(PdfOp.Catalog, op, 1));
        return verifier.Verify(document).Issues;
    }
}
