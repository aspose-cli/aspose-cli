using Aspose.Cli.Sdk.Ports;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfFontDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FontlessPages_HaveEmptyFontResults(bool imageOnly)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("fontless.pdf", pages: 1,
            textPages: new HashSet<int>(),
            imagePages: imageOnly ? new HashSet<int> { 1 } : null);
        byte[] original = File.ReadAllBytes(input);
        using (var document = new Document(input))
        {
            Assert.Null(document.Pages[1].Resources.Fonts);
            Assert.Equal(imageOnly ? 1 : 0, document.Pages[1].Resources.Images?.Count ?? 0);
        }

        PdfInfoResult info = fixture.Engine.GetInfo(input, new PdfInfoRequest { Details = ["fonts"] });
        var environment = new PdfFontEnvironment(fixture.Outputs(ProductTestBudgets.Start<PdfModule>().Writer), ProductTestBudgets.Create<PdfModule>());
        FontCheckResult result = environment.CheckFonts(input, new FontCheckRequest());

        Assert.Empty(info.Fonts!);
        Assert.Empty(result.Fonts);
        Assert.True(result.AllAvailable);
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Fact]
    public void MixedPages_RetainFontsFromTextPageAfterFontlessPages()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("mixed.pdf", pages: 3,
            textPages: new HashSet<int> { 3 }, imagePages: new HashSet<int> { 2 });
        PdfInfoResult info = fixture.Engine.GetInfo(input, new PdfInfoRequest { Details = ["fonts"] });
        var environment = new PdfFontEnvironment(fixture.Outputs(ProductTestBudgets.Start<PdfModule>().Writer), ProductTestBudgets.Create<PdfModule>());
        FontCheckResult result = environment.CheckFonts(input, new FontCheckRequest());

        Assert.Equal("Helvetica", Assert.Single(info.Fonts!).Name);
        Assert.Equal("Helvetica", Assert.Single(result.Fonts).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Review_BlankPageIsRenderedAndReportedWithoutInternalError(bool worker)
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateRawDocument("blank.pdf", pages: 1, textPages: new HashSet<int>());
        byte[] original = File.ReadAllBytes(input);
        string output = workspace.File("review");
        string[] supervision = worker ? ["--timeout", "30"] : [];
        CliResult fonts = workspace.Run(
            ["fonts", "check", input, "--output", "json", .. supervision]);
        CliResult review = workspace.Run(
            ["review", input, "--out", output, "--output", "json", .. supervision]);

        Assert.True(fonts.ExitCode == 0, fonts.StdErr);
        Assert.Empty(JsonNode.Parse(fonts.StdOut)!["fonts"]!.AsArray());
        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode result = JsonNode.Parse(review.StdOut)!;
        Assert.Equal(1, result["coverage"]!["renderedItemCount"]!.GetValue<int>());
        Assert.Equal(0, result["coverage"]!["omittedItemCount"]!.GetValue<int>());
        Assert.Contains(result["findings"]!.AsArray(),
            finding => finding!["code"]!.GetValue<string>() == "PDF_PAGE_WITHOUT_READABLE_CONTENT");
        Assert.DoesNotContain(result["findings"]!.AsArray(),
            finding => finding!["code"]!.GetValue<string>() == "FONTS_NOT_CHECKED");
        Assert.True(File.Exists(Path.Combine(output, "artifacts", "page-0001.png")));
        Assert.Equal(original, File.ReadAllBytes(input));
    }
}
