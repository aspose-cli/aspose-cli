using System.Collections.Concurrent;
using System.Text;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The Aspose.PDF defects in KNOWN-ISSUES.md, reproduced with the SDK alone. Each passes while
/// the pinned SDK still has its defect.
/// </summary>
public sealed class PdfKnownIssueTests
{
    [LicensedFact]
    public async Task HtmlAndMarkdownImports_FetchNetworkResourcesPastTheLoader()
    {
        using var fixture = new PdfEngineFixture();
        await using var server = new ResourceHttpServer();
        string html = fixture.File("input.html");
        File.WriteAllText(html, $"""
            <html><head><link rel="stylesheet" href="{server.Url}/style.css"></head>
            <body><p>Local content</p><img src="{server.Url}/image.png"></body></html>
            """);
        var loaderCalls = new ConcurrentQueue<string>();
        var options = new HtmlLoadOptions(fixture.Temp.Path + Path.DirectorySeparatorChar)
        {
            // An empty result that is not cancelled keeps the SDK's own loader off.
            CustomLoaderOfExternalResources = uri =>
            {
                loaderCalls.Enqueue(uri);
                return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
            },
        };
        using (var document = new Document(html, options))
        {
            document.Save(fixture.File("html.pdf"));
        }
        int htmlRequests = server.RequestCount;
        Assert.NotEmpty(loaderCalls);

        // MdLoadOptions has no resource hook to install.
        string markdown = fixture.File("input.md");
        File.WriteAllText(markdown, $"# Local content\n\n![image]({server.Url}/markdown.png)\n");
        using (var document = new Document(markdown, new MdLoadOptions()))
        {
            document.Save(fixture.File("markdown.pdf"));
        }
        int markdownRequests = server.RequestCount - htmlRequests;

        KnownIssue.Reproduces(
            "PDF-HTML-EGRESS",
            htmlRequests > 0 && markdownRequests > 0,
            $"HTML made {htmlRequests} request(s) past a refusing loader, Markdown {markdownRequests}");
    }

    [LicensedFact]
    public void Destinations_ReadAnOmittedCoordinateAsZero()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("destinations.pdf");
        WriteDestinationDocument(input);

        using var document = new Document(input);
        var omitted = (XYZExplicitDestination)document.Outlines.Single(static item => item.Title == "Omitted").Destination;
        var zero = (XYZExplicitDestination)document.Outlines.Single(static item => item.Title == "Zero").Destination;

        KnownIssue.Reproduces(
            "PDF-MOVE-BOOKMARK",
            (omitted.Left, omitted.Top, omitted.Zoom) == (zero.Left, zero.Top, zero.Zoom),
            $"/XYZ null null null reads as {omitted.Left} {omitted.Top} {omitted.Zoom}");
    }

    [LicensedFact]
    public void OutlineDelete_RemovesBookmarksByTitle()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("same-titles.pdf");
        using (var document = new Document())
        {
            Page first = document.Pages.Add();
            Page second = document.Pages.Add();
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Results",
                Destination = new FitExplicitDestination(first),
            });
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Results",
                Destination = new FitExplicitDestination(second),
            });
            document.Save(input);
        }

        string output = fixture.File("same-titles.out.pdf");
        using (var document = new Document(input))
        {
            document.Outlines.Skip(1).First().Delete();
            document.Save(output);
        }

        using var reopened = new Document(output);
        int keptPage = ((ExplicitDestination)reopened.Outlines.Single().Destination).PageNumber;
        KnownIssue.Reproduces(
            "PDF-OUTLINE-DELETE-TITLE",
            keptPage == 2,
            $"deleting the second of two bookmarks titled Results kept the one on page {keptPage}");
    }

    [LicensedFact]
    public void NamedDestinations_ThrowForANameTreeWithoutDests()
    {
        using var fixture = new PdfEngineFixture();
        string input = PdfNavigationTests.CreateAttachmentOnlyNameTree(fixture, "attachments.pdf");

        using var document = new Document(input);
        Exception? names = Record.Exception(() => document.NamedDestinations.Names);
        Exception? count = Record.Exception(() => document.NamedDestinations.Count);

        KnownIssue.Reproduces(
            "PDF-NAMES-WITHOUT-DESTS",
            names is NullReferenceException && count is NullReferenceException,
            $"for a name tree with only EmbeddedFiles, Names threw {names?.GetType().Name ?? "nothing"} and Count threw {count?.GetType().Name ?? "nothing"}");
    }

    /// <summary>One page and two bookmarks: one omits every coordinate, one names 0.</summary>
    private static void WriteDestinationDocument(string path)
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Outlines 5 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Outlines /First 6 0 R /Last 7 0 R /Count 2 >>",
            "<< /Title (Omitted) /Parent 5 0 R /Next 7 0 R /Dest [3 0 R /XYZ null null null] >>",
            "<< /Title (Zero) /Parent 5 0 R /Prev 6 0 R /Dest [3 0 R /XYZ 0 0 0] >>",
        ];
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (int index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            pdf.Append($"{offset:0000000000} 00000 n \n");
        }
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
    }
}
