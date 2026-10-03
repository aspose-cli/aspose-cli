using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Page geometry reported by inspect and judged by the review gate.</summary>
public sealed class PdfReviewAndInfoTests
{
    [Theory]
    [InlineData(Rotation.None, 0)]
    [InlineData(Rotation.on90, 90)]
    [InlineData(Rotation.on180, 180)]
    [InlineData(Rotation.on270, 270)]
    public void InspectPreview_ReportsRotationInDegrees(Rotation rotation, int degrees)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument("rotated.pdf", pages: 1);
        using (var document = new Document(path))
        {
            document.Pages[1].Rotate = rotation;
            document.Save(path);
        }

        PdfPageInfo page = Assert.Single(fixture.Engine.GetInfo(path, new PdfInfoRequest { IncludePreview = true }).Pages!);

        Assert.Equal(degrees, page.Rotation);
    }

    [Fact]
    public void Review_FlagsAnUnusualPageSizeOnTheInspectedPage()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateDocument("tiny.pdf", pages: 2);
        using (var document = new Document(input))
        {
            document.Pages[2].SetPageSize(60, 40);
            document.Save(input);
        }

        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode finding = Assert.Single(
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>() == "PDF_PAGE_SIZE_UNUSUAL")!;
        Assert.Equal("page 2", finding["location"]!.GetValue<string>());
        Assert.Contains("60 x 40 pt", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // The evidence is the image of that page alone.
        Assert.EndsWith(
            "page-0002.png",
            Assert.Single(finding["evidence"]!.AsArray())!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Review_CountsALargeImageAsPageContent()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        // Pages 1 and 2 carry one short line of text, pages 1 and 3 an image over a third of the page.
        string input = fixture.CreateRawDocument("pictures.pdf", pages: 3,
            textPages: new HashSet<int> { 1, 2 }, imagePages: new HashSet<int> { 1, 3 }, imagePoints: 400);

        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        Assert.Equal(
            [("PDF_PAGE_UTILIZATION_LOW", "page 2")],
            JsonNode.Parse(review.StdOut)!["findings"]!.AsArray()
                .Select(static item => (item!["code"]!.GetValue<string>(), item["location"]!.GetValue<string>()))
                .Where(static item => item.Item1.StartsWith("PDF_PAGE_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Review_DeclaresEveryCheckItsAssessmentReports()
    {
        IReadOnlyList<ReviewCheck> declared = new PdfViewAdapter().Checks;

        // Findings are built only from PdfReviewChecks members, so each must be declared.
        Assert.Equal(
            typeof(PdfReviewChecks).GetProperties()
                .Where(static property => property.PropertyType == typeof(ReviewCheck))
                .Select(static property => (ReviewCheck)property.GetValue(null)!)
                .OrderBy(static check => check.Code, StringComparer.Ordinal),
            declared.OrderBy(static check => check.Code, StringComparer.Ordinal));
    }
}
