using Aspose.Cli.Sdk.Rendering;
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

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["fonts"] });
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
        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = input, Details = ["fonts"] });
        var environment = new PdfFontEnvironment(fixture.Outputs(ProductTestBudgets.Start<PdfModule>().Writer), ProductTestBudgets.Create<PdfModule>());
        FontCheckResult result = environment.CheckFonts(input, new FontCheckRequest());

        Assert.Equal("Helvetica", Assert.Single(info.Fonts!).Name);
        Assert.Equal("Helvetica", Assert.Single(result.Fonts).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FontsCheck_BlankPageIsReportedWithoutInternalError(bool worker)
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateRawDocument("blank.pdf", pages: 1, textPages: new HashSet<int>());
        byte[] original = File.ReadAllBytes(input);
        string[] supervision = worker ? ["--timeout", TestDeadline.CliTimeout] : [];
        CliResult fonts = workspace.Run(["fonts", "check", input, "--output-format", "json", .. supervision]);

        Assert.True(fonts.ExitCode == 0, fonts.StdErr);
        Assert.Empty(JsonNode.Parse(fonts.StdOut)!["fonts"]!.AsArray());
        Assert.Equal(original, File.ReadAllBytes(input));
    }
}
