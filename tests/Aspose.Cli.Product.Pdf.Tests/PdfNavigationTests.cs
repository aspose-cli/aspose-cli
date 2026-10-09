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
    public void MovePages_RetargetsExactDestinationsAndCountsOnesWithAZeroCoordinate()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateNavigationDocument(fixture, "move.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new MovePagesOp { Pages = "3", To = 1 }] },
            Output = TestOutput.At(fixture.File("move.out.pdf")),
        });

        // "Three" and the explicit link are retargeted; "appendix" has left 0, which may stand
        // for an omitted coordinate, so it and the bookmark and link that use it are counted.
        Warning warning = Assert.Single(result.Warnings!, static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.True(warning.AffectsCompleteness);
        Assert.StartsWith("1 bookmark(s), 1 link(s) and 1 named destination(s)", warning.Message, StringComparison.Ordinal);
        using var moved = new Document(result.Output!.Path);
        var three = (XYZExplicitDestination)moved.Outlines.Single(static item => item.Title == "Three").Destination;
        Assert.Equal((1, 10d, 700d, 2d), (three.PageNumber, three.Left, three.Top, three.Zoom));
        Assert.Equal(0, ((ExplicitDestination)moved.NamedDestinations["appendix"]).PageNumber);
    }

    [LicensedFact]
    public void MovePages_KeepsEveryExactDestinationOnTheMovedPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("exact.pdf");
        using (var document = new Document())
        {
            for (int number = 1; number <= 3; number++)
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment($"Page {number}"));
            }

            Page two = document.Pages[2];
            Page three = document.Pages[3];
            document.NamedDestinations.Add("appendix", new XYZExplicitDestination(three, 20, 600, 0));
            var parent = new OutlineItemCollection(document.Outlines) { Title = "Parent", Destination = new FitExplicitDestination(two) };
            parent.Add(new OutlineItemCollection(document.Outlines) { Title = "Child", Destination = new FitRExplicitDestination(three, 0, 10, 300, 400) });
            document.Outlines.Add(parent);
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Action", Action = new GoToAction(new FitHExplicitDestination(three, 650)) });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Named", Destination = new NamedDestination(document, "appendix") });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines) { Title = "Bounded", Destination = new FitBVExplicitDestination(two, 30) });
            document.Pages[1].Annotations.Add(new LinkAnnotation(document.Pages[1], new Rectangle(100, 100, 200, 120))
            {
                Destination = new FitBExplicitDestination(three),
            });
            three.Annotations.Add(new LinkAnnotation(three, new Rectangle(100, 100, 200, 120))
            {
                Action = new GoToAction(new XYZExplicitDestination(two, 5, 500, 1)),
            });
            three.Annotations.Add(new LinkAnnotation(three, new Rectangle(100, 200, 200, 220))
            {
                Destination = new FitVExplicitDestination(three, 40),
            });
            three.Annotations.Add(new LinkAnnotation(three, new Rectangle(100, 300, 200, 320))
            {
                Destination = new FitHExplicitDestination(document.Pages[1], 70),
            });
            document.Save(input);
        }

        // Pages 2 and 3 move to the front: 2 becomes 1 and 3 becomes 2.
        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new MovePagesOp { Pages = "2-3", To = 1 }] },
            Output = TestOutput.At(fixture.File("exact.out.pdf")),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        using var moved = new Document(result.Output!.Path);
        Assert.Equal(["Page 2", "Page 3", "Page 1"], Enumerable.Range(1, 3).Select(number => Text(moved.Pages[number])));
        OutlineItemCollection parentItem = moved.Outlines.Single(static item => item.Title == "Parent");
        Assert.Equal("1 Fit", Describe(parentItem.Destination));
        Assert.Equal("2 FitR 0 10 300 400", Describe(parentItem.Single().Destination));
        Assert.Equal("2 FitH 650", Describe(((GoToAction)moved.Outlines.Single(static item => item.Title == "Action").Action).Destination));
        Assert.Equal("1 FitBV 30", Describe(moved.Outlines.Single(static item => item.Title == "Bounded").Destination));
        Assert.Equal("2 XYZ 20 600 0", Describe(moved.NamedDestinations["appendix"]));
        Assert.Equal("2 FitB", Describe(((LinkAnnotation)moved.Pages[3].Annotations[1]).Destination));
        Assert.Equal("1 XYZ 5 500 1", Describe(((GoToAction)((LinkAnnotation)moved.Pages[2].Annotations[1]).Action).Destination));
        Assert.Equal("2 FitV 40", Describe(((LinkAnnotation)moved.Pages[2].Annotations[2]).Destination));
        Assert.Equal("3 FitH 70", Describe(((LinkAnnotation)moved.Pages[2].Annotations[3]).Destination));
    }

    /// <summary>The page number, type and coordinates of an explicit destination.</summary>
    private static string Describe(IAppointment destination) => destination switch
    {
        FitExplicitDestination fit => $"{fit.PageNumber} Fit",
        FitBExplicitDestination fit => $"{fit.PageNumber} FitB",
        FitRExplicitDestination fit => $"{fit.PageNumber} FitR {fit.Left} {fit.Bottom} {fit.Right} {fit.Top}",
        FitHExplicitDestination fit => $"{fit.PageNumber} FitH {fit.Top}",
        FitVExplicitDestination fit => $"{fit.PageNumber} FitV {fit.Left}",
        FitBVExplicitDestination fit => $"{fit.PageNumber} FitBV {fit.Left}",
        XYZExplicitDestination xyz => $"{xyz.PageNumber} XYZ {xyz.Left} {xyz.Top} {xyz.Zoom}",
        _ => destination.GetType().Name,
    };

    private static string Text(Page page)
    {
        var absorber = new TextAbsorber();
        page.Accept(absorber);
        return absorber.Text.Trim();
    }

    [Fact]
    public void AnEditThatKeepsEveryPage_DisclosesNoNavigationLoss()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateNavigationDocument(fixture, "metadata.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetMetadataOp { Title = "Kept" }, new RotatePagesOp { Pages = "3", Angle = 90 }] },
            Output = TestOutput.At(fixture.File("metadata.out.pdf")),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
    }

    [Fact]
    public void AnEdit_OfADocumentWhoseNameTreeHoldsOnlyAttachments_Succeeds()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreateAttachmentOnlyNameTree(fixture, "attachments.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new SetMetadataOp { Title = "Kept" }] },
            Output = TestOutput.At(fixture.File("attachments.out.pdf")),
        });

        Assert.DoesNotContain(result.Warnings ?? [], static item => item.Code.Name == "NAVIGATION_DEGRADED");
        using var edited = new Document(result.Output!.Path);
        Assert.Single(edited.EmbeddedFiles);
    }

    /// <summary>One page and one attachment: the name tree has EmbeddedFiles and no Dests.</summary>
    internal static string CreateAttachmentOnlyNameTree(PdfEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        using var document = new Document();
        document.Pages.Add().Paragraphs.Add(new TextFragment("Attachments only"));
        document.EmbeddedFiles.Add("figures.csv", new FileSpecification(new MemoryStream("a,b\n"u8.ToArray()), "figures.csv", "Figures")
        {
            Name = "figures.csv",
            UnicodeName = "figures.csv",
        });
        document.Save(path);
        return path;
    }

    [Fact]
    public void Merge_CountsBookmarksReducedToFitAndDroppedNamedDestinations()
    {
        using var fixture = new PdfEngineFixture();
        string first = CreateNavigationDocument(fixture, "first.pdf");
        string second = fixture.CreateDocument("second.pdf", pages: 1);

        PdfWriteResult result = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(fixture.File("merged.pdf")),
            InputPaths = [first, second],
        });

        // "Two" was Fit and survives; "Three" (XYZ) becomes Fit and "Named" loses its name.
        Warning warning = Assert.Single(result.Warnings!, static item => item.Code.Name == "NAVIGATION_DEGRADED");
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
