using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Extraction and stamping are charged for what they actually read and decode.</summary>
public sealed class PdfBudgetTests
{
    [Fact]
    public void ExtractImages_WritesEachImageThroughTheExtractionBudget()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("images.pdf", pages: 2, imagePages: new HashSet<int> { 1, 2 });

        PdfExtractResult result = PdfExtract.Assets(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("images")),
            What = "images",
        });

        Assert.Equal([1, 2], result.Items.Select(static item => item.Page));
        Assert.All(result.Items, static item =>
        {
            Assert.Equal(new FileInfo(item.Path).Length, item.SizeBytes);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(item.Path)[..4]);
        });
    }

    [Fact]
    public void TextOutputs_SeparateEveryPageEvenWhenAPageHasNoText()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("blank-first.pdf", pages: 2, textPages: new HashSet<int> { 2 });
        string converted = fixture.File("blank-first.txt");

        PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(converted, format: "txt") });
        string extracted = Assert.Single(PdfExtract.Assets(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("blank-first")),
            What = "text",
        }).Items).Path;

        foreach (string text in new[] { File.ReadAllText(converted), File.ReadAllText(extracted) })
        {
            string[] pages = text.Split('\f');
            Assert.Equal(2, pages.Length);
            Assert.DoesNotContain("page 2", pages[0], StringComparison.Ordinal);
            Assert.Contains("Portable PDF page 2", pages[1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExtractImages_RefusesAnOversizedImageBeforeDecodingIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateRawDocument("bomb.pdf", pages: 1, imagePages: new HashSet<int> { 1 }, imageSide: 60_000);
        string output = fixture.File("bomb");

        CliException error = Assert.Throws<CliException>(() => PdfExtract.Assets(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(output),
            What = "images",
        }));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.Contains("60000x60000", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFiles(output).Any());
    }

    [Fact]
    public void WatermarkImage_ChargesTheImageOnceWhateverThePageCount()
    {
        using var fixture = new PdfEngineFixture();
        string image = fixture.File("mark.png");
        string source = fixture.CreateRawDocument("with-image.pdf", pages: 1, imagePages: new HashSet<int> { 1 });
        string extracted = Assert.Single(PdfExtract.Assets(fixture.Session, new PdfExtractRequest
        {
            Input = source,
            Output = new ResolvedDirectory(fixture.File("mark")),
            What = "images",
        }).Items).Path;
        File.Copy(extracted, image);
        string input = fixture.CreateDocument("pages.pdf", pages: 3);

        long onePage = InputBytesCharged(fixture, input, "1", "one.pdf");
        long threePages = InputBytesCharged(fixture, input, "1-3", "three.pdf");

        Assert.Equal(onePage, threePages);

        long InputBytesCharged(PdfEngineFixture owner, string path, string pages, string output)
        {
            ProductTestInvocation invocation = ProductTestBudgets.Start<PdfModule>();
            long before = invocation.ResourceBudgets.Remaining(ResourceBudgetKinds.InputBytes);
            var session = new PdfSession(
                owner.Outputs(invocation.Writer), invocation.ResourceBudgets, new PdfDocumentLoader(invocation.ResourceBudgets));
            PdfEdit.Run(session, new PdfEditRequest
            {
                Input = path,
                Batch = new PdfOpsBatch { Ops = [new AddWatermarkImageOp { Path = image, Pages = pages }] },
                Output = TestOutput.At(owner.File(output)),
            });
            return before - invocation.ResourceBudgets.Remaining(ResourceBudgetKinds.InputBytes);
        }
    }
}
