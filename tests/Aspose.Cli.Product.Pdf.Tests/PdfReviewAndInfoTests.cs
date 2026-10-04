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
    public void Review_NamesAScannedPageThatSearchAndRedactionCannotReach()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        // Page 3 is one image over the whole page with no text, as a scan is.
        string input = fixture.CreateRawDocument("scanned.pdf", pages: 3,
            textPages: new HashSet<int> { 1, 2 }, imagePages: new HashSet<int> { 3 }, imagePoints: 700,
            textContent: "BT /F1 12 Tf 72 720 Td (A line of readable text on a text page) Tj ET");

        CliResult review = workspace.Run(["review", input, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        JsonNode finding = Assert.Single(
            result["findings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>().StartsWith("PDF_PAGE_", StringComparison.Ordinal))!;
        Assert.Equal("PDF_PAGE_WITHOUT_TEXT_LAYER", finding["code"]!.GetValue<string>());
        Assert.Equal("info", finding["severity"]!.GetValue<string>());
        Assert.Equal("page 3", finding["location"]!.GetValue<string>());
        Assert.Contains("redact_area", finding["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(1, result["coverage"]!["metrics"]!.AsArray()
            .Single(static metric => metric!["name"]!.GetValue<string>() == "scannedPages")!["value"]!.GetValue<int>());
    }

    [Fact]
    public void Review_FlagsTheEvaluationWatermarkSavedIntoTheFile()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        // The sentence an unlicensed save stamps on every page, as a later licensed run reads it.
        string input = fixture.CreateRawDocument("plain.pdf", pages: 2);
        string stamped = fixture.File("stamped.pdf");
        using (var document = new Document(input))
        {
            document.Pages[2].Paragraphs.Add(new Aspose.Pdf.Text.TextFragment(
                "Evaluation Only. Created with Aspose.PDF. Copyright 2002-2026 Aspose Pty Ltd."));
            document.Save(stamped);
        }

        CliResult review = workspace.Run(["review", stamped, "--out", workspace.File("review"), "--output", "json"]);

        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode[] findings = [.. JsonNode.Parse(review.StdOut)!["findings"]!.AsArray()
            .Where(static item => item!["code"]!.GetValue<string>() == "PDF_EVALUATION_WATERMARK")
            .Select(static item => item!)];
        // Without a license the test's own save stamps every page.
        Assert.Equal(
            fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Licensed ? ["page 2"] : ["page 1", "page 2"],
            findings.Select(static item => item["location"]!.GetValue<string>()));
        Assert.All(findings, static item => Assert.Contains("license", item["hint"]!.GetValue<string>(), StringComparison.Ordinal));
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
