using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;
using PageRange = Aspose.Cli.Sdk.Addressing.PageRange;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// With a license, the write pipeline reports the evaluation notice an earlier save without a
/// license printed on a PDF's pages: from the document an output holds, which for a split part
/// or a page selection is only its pages, and as the source's for page images.
/// </summary>
public sealed class PdfEvaluationMarkTests
{
    private const string Notice = "Evaluation Only. Created with Aspose.PDF. Copyright 2002-2026 Aspose Pty Ltd.";

    [Fact]
    public void SplitAndPageSelection_InspectThePagesTheOutputHolds()
    {
        TestLicense.Require("Only a licensed engine reads a notice an earlier evaluation save printed without adding its own.");
        using var fixture = new PdfEngineFixture();
        string input = MarkedOnFirstPage(fixture);

        PdfSplitResult marked = fixture.Disclosed(session => PdfSplit.Run(session, new PdfSplitRequest
        {
            Input = input,
            PageGroups = [PageRange.Parse("1")],
            Output = new ResolvedDirectory(fixture.File("marked-part")),
        }));
        PdfSplitResult clean = fixture.Disclosed(session => PdfSplit.Run(session, new PdfSplitRequest
        {
            Input = input,
            PageGroups = [PageRange.Parse("2")],
            Output = new ResolvedDirectory(fixture.File("clean-part")),
        }));
        PdfConvertResult tiff = fixture.Disclosed(session => PdfConvert.Run(session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("page2.tiff"), format: "tiff"),
            Pages = PageRange.Parse("2"),
        }));
        Assert.StartsWith("The output keeps the evaluation marks", Disclosure(marked).Message, StringComparison.Ordinal);
        Assert.DoesNotContain(clean.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalInputMarked);
        Assert.DoesNotContain(tiff.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalInputMarked);
    }

    /// <summary>
    /// A page image or page text shows only the selected pages, so it discloses the source's
    /// marks only when those pages carry them: a document marked on its first four pages and
    /// clean after them, as a merge of a marked and a clean one is.
    /// </summary>
    [Fact]
    public void PageRenders_DiscloseOnlyTheMarksOfTheSelectedPages()
    {
        TestLicense.Require("Only a licensed engine reads a notice an earlier evaluation save printed without adding its own.");
        using var fixture = new PdfEngineFixture();
        string input = Marked(fixture, "combined.pdf", pages: 12, markedPages: 4);

        PdfRenderResult cleanRender = fixture.Disclosed(session => PdfRender.Run(session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("page6.png"), format: "png"),
            Pages = PageRange.Parse("6"),
        }));
        PdfConvertResult cleanConvert = fixture.Disclosed(session => PdfConvert.Run(session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("page6-convert.png"), format: "png"),
            Pages = PageRange.Parse("6"),
        }));
        PdfConvertResult cleanText = fixture.Disclosed(session => PdfConvert.Run(session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("pages6-8.txt"), format: "txt"),
            Pages = PageRange.Parse("6-8"),
        }));
        PdfRenderResult markedRender = fixture.Disclosed(session => PdfRender.Run(session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("page2.png"), format: "png"),
            Pages = PageRange.Parse("2,6"),
        }));

        Assert.DoesNotContain(cleanRender.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalInputMarked);
        Assert.DoesNotContain(cleanConvert.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalInputMarked);
        Assert.DoesNotContain(cleanText.Warnings ?? [], static warning => warning.Code == WarningCodes.EvalInputMarked);
        Assert.StartsWith("The rendered source carries the evaluation marks", Disclosure(markedRender).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SigningAPdfAnEvaluationSaveMarked_DisclosesTheMarksAndVerifiesTheSignature()
    {
        TestLicense.Require("Only a licensed engine reads a notice an earlier evaluation save printed without adding its own.");
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string input = MarkedOnFirstPage(fixture);
        string certificate = fixture.CreateCertificate(certificatePassword);

        PdfSignResult signed = fixture.Disclosed(session => PdfSign.Run(session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("signed.pdf")),
            CertificatePath = certificate,
            CertificatePassword = new Secret(certificatePassword),
            Page = 1,
        }));

        Assert.Contains("Aspose.PDF", Disclosure(signed).Message, StringComparison.Ordinal);
        Assert.True(signed.Signature.Signed);
        Assert.True(signed.Signature.Valid);
    }

    private static Warning Disclosure(ResultEnvelope result) =>
        Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.EvalInputMarked);

    /// <summary>Three pages, the first carrying the notice an evaluation save prints.</summary>
    private static string MarkedOnFirstPage(PdfEngineFixture fixture) => Marked(fixture, "marked.pdf", pages: 3, markedPages: 1);

    /// <summary>A document whose first <paramref name="markedPages"/> pages carry the notice an evaluation save prints.</summary>
    private static string Marked(PdfEngineFixture fixture, string name, int pages, int markedPages)
    {
        string path = fixture.File(name);
        using var document = new Document();
        for (int number = 1; number <= pages; number++)
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment($"Portable PDF page {number}"));
            if (number <= markedPages)
            {
                page.Paragraphs.Add(new TextFragment(Notice));
            }
        }

        document.Save(path);
        return path;
    }
}
