using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Page moves and merges that break bookmarks, links or named destinations publish their
/// output with a completeness warning that counts what broke; nothing is silently lost.
/// </summary>
public sealed class PdfNavigationTests
{
    [Fact]
    public void MovePages_CountsTheNavigationToMovedPagesThatLostItsTarget()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateNavigationDocument(fixture, "move.pdf");

        PdfEditResult result = fixture.Engine.ApplyOps(input,
            new PdfOpsBatch { Ops = [new MovePagesOp { Pages = "3", To = 1 }] },
            new PdfEditRequest { OutputPath = fixture.File("move.out.pdf") });

        Warning warning = Assert.Single(result.Warnings!, static item => item.Code == "NAVIGATION_DEGRADED");
        Assert.True(warning.AffectsCompleteness);
        Assert.StartsWith("2 bookmark(s), 2 link(s) and 1 named destination(s)", warning.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(result.Output!.Path));
    }

    [Fact]
    public void AnEditThatKeepsEveryPage_DisclosesNoNavigationLoss()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateNavigationDocument(fixture, "metadata.pdf");

        PdfEditResult result = fixture.Engine.ApplyOps(input,
            new PdfOpsBatch { Ops = [new SetMetadataOp { Title = "Kept" }, new RotatePagesOp { Pages = "3", Angle = 90 }] },
            new PdfEditRequest { OutputPath = fixture.File("metadata.out.pdf") });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code == "NAVIGATION_DEGRADED");
    }

    [Fact]
    public void Merge_CountsBookmarksReducedToFitAndDroppedNamedDestinations()
    {
        using var fixture = new PdfEngineFixture();
        string first = CreateNavigationDocument(fixture, "first.pdf");
        string second = fixture.CreateDocument("second.pdf", pages: 1);

        PdfWriteResult result = fixture.Engine.Merge(new PdfMergeRequest
        {
            InputPaths = [first, second],
            OutputPath = fixture.File("merged.pdf"),
        });

        // "Two" was Fit and survives; "Three" (XYZ) becomes Fit and "Named" loses its name.
        Warning warning = Assert.Single(result.Warnings!, static item => item.Code == "NAVIGATION_DEGRADED");
        Assert.StartsWith("2 bookmark(s), 1 link(s) and 1 named destination(s)", warning.Message, StringComparison.Ordinal);
        using var merged = new Document(result.Output.Path);
        Assert.Equal(3, merged.Outlines.Count);
    }

    /// <summary>
    /// Three pages: bookmarks Two (Fit, page 2), Three (XYZ, page 3) and Named (via the
    /// named destination "appendix" on page 3); on page 1 an explicit link and a named
    /// link, both to page 3.
    /// </summary>
    private static string CreateNavigationDocument(PdfEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        using var document = new Document();
        for (int number = 1; number <= 3; number++)
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment($"Page {number}"));
        }

        document.NamedDestinations.Add("appendix", new XYZExplicitDestination(document.Pages[3], 0, 600, 0));
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Two", Destination = new FitExplicitDestination(document.Pages[2]) });
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Three", Destination = new XYZExplicitDestination(document.Pages[3], 10, 700, 2) });
        document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Named", Destination = new NamedDestination(document, "appendix") });
        document.Pages[1].Annotations.Add(new LinkAnnotation(document.Pages[1], new Rectangle(100, 100, 200, 120))
        {
            Action = new GoToAction(new XYZExplicitDestination(document.Pages[3], 5, 500, 1)),
        });
        document.Pages[1].Annotations.Add(new LinkAnnotation(document.Pages[1], new Rectangle(100, 200, 200, 220))
        {
            Destination = new NamedDestination(document, "appendix"),
        });
        document.Save(path);
        return path;
    }
}
