using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Extraction is charged for what it actually reads and decodes.</summary>
public sealed class PdfBudgetTests
{
    [Fact]
    public void ExtractImages_WritesEachImageThroughTheExtractionBudget()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("images.pdf", pages: 2, imagePages: new HashSet<int> { 1, 2 });

        PdfExtractResult result = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("images")),
            What = "images",
        });

        Assert.Equal([1, 2], result.Outputs.Select(static item => item.Page));
        Assert.All(result.Outputs, static item =>
        {
            Assert.Equal(new FileInfo(item.Path).Length, item.SizeBytes);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(item.Path)[..4]);
        });
    }

    [Fact]
    public void ExtractImages_RefusesAnOversizedImageBeforeDecodingIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("bomb.pdf", pages: 1, imagePages: new HashSet<int> { 1 }, imageSide: 60_000);
        string output = fixture.File("bomb");

        CliException error = Assert.Throws<CliException>(() => PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(output),
            What = "images",
        }));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.Contains("60000x60000", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFiles(output).Any());
    }

}
