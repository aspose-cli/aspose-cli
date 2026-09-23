using System.Text.Json.Nodes;
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
            static item => item!["code"]!.GetValue<string>() == "PDF_UNUSUAL_PAGE_SIZE")!;
        Assert.Equal("page 2", finding["location"]!.GetValue<string>());
        Assert.Contains("60 x 40 pt", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
