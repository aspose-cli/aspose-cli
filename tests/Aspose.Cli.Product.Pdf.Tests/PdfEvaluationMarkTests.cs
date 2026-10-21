using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO.Output;
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
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Pages, null, [PageRange.Parse("1")], null),
            Output = new ResolvedDirectory(fixture.File("marked-part")),
        }));
        PdfSplitResult clean = fixture.Disclosed(session => PdfSplit.Run(session, new PdfSplitRequest
        {
            Input = input,
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Pages, null, [PageRange.Parse("2")], null),
            Output = new ResolvedDirectory(fixture.File("clean-part")),
        }));
        PdfConvertResult tiff = fixture.Disclosed(session => PdfConvert.Run(session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("page2.tiff"), format: "tiff"),
            Pages = PageRange.Parse("2"),
        }));
        Assert.StartsWith("The output keeps the evaluation marks", Disclosure(marked).Message, StringComparison.Ordinal);
        Assert.DoesNotContain(clean.Warnings ?? [], static warning => warning.Code == WarningCodes.EvaluationInputMarked);
        Assert.DoesNotContain(tiff.Warnings ?? [], static warning => warning.Code == WarningCodes.EvaluationInputMarked);
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

        Assert.DoesNotContain(cleanRender.Warnings ?? [], static warning => warning.Code == WarningCodes.EvaluationInputMarked);
        Assert.DoesNotContain(cleanText.Warnings ?? [], static warning => warning.Code == WarningCodes.EvaluationInputMarked);
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
        Assert.True(ResultJson.Intact(signed.Signature));
    }

    /// <summary>
    /// The signing facade saves the signed file, so the marks evaluation mode printed are read
    /// from that file: the disclosure names them, with a license or without.
    /// </summary>
    [Fact]
    public void Signing_DisclosesTheMarksTheSignedFileCarries()
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        // Without a license the input is written by hand, so only the signed file carries a notice.
        string input = fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed
            ? MarkedOnFirstPage(fixture)
            : fixture.CreateRawDocument("clean.pdf", pages: 2);
        string certificate = fixture.CreateCertificate(certificatePassword);

        PdfSignResult signed = fixture.Disclosed(session => PdfSign.Run(session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("signed-marks.pdf")),
            CertificatePath = certificate,
            CertificatePassword = new Secret(certificatePassword),
            Page = 1,
        }));

        Warning disclosure = Assert.Single(signed.Warnings!, static warning =>
            warning.Code == WarningCodes.EvaluationInputMarked || warning.Code == WarningCodes.EvaluationMode);
        Assert.Contains("the evaluation notice '... Created with Aspose.PDF", disclosure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every part of a split carries the notice, printed by evaluation mode or by the earlier
    /// save of a marked input, and the disclosure names each part.
    /// </summary>
    [Fact]
    public void Split_NamesEveryPartThatCarriesTheNotice()
    {
        using var fixture = new PdfEngineFixture();
        bool licensed = fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed;
        string input = licensed
            ? Marked(fixture, "parts.pdf", pages: 2, markedPages: 2)
            : fixture.CreateRawDocument("parts.pdf", pages: 2);

        PdfSplitResult split = fixture.Disclosed(session => PdfSplit.Run(session, new PdfSplitRequest
        {
            Input = input,
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Pages, null, [PageRange.Parse("1"), PageRange.Parse("2")], null),
            Output = new ResolvedDirectory(fixture.File("parts")),
        }));

        Warning disclosure = Assert.Single(split.Warnings!, warning =>
            warning.Code == (licensed ? WarningCodes.EvaluationInputMarked : WarningCodes.EvaluationMode));
        Assert.All(split.Outputs, output =>
            Assert.Contains($"'{Path.GetFileName(output.Path)}'", disclosure.Message, StringComparison.Ordinal));
        Assert.Contains("the evaluation notice '... Created with Aspose.PDF", disclosure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An evaluation edit that encrypts saves once, so its output carries one evaluation notice,
    /// unless the document information holds text beyond Latin-1: that is encrypted on a copy
    /// reopened from a first save (PDF-ENCRYPTED-INFO-TEXT), and still reads back as written.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void EvaluationEditThatEncrypts_PrintsOneNoticeUnlessTheInformationIsBeyondLatin1()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string input = fixture.CreateRawDocument("plain.pdf", pages: 1);
        var passwords = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PDF_OWNER"] = "owner-secret", ["PDF_NEW"] = "new-secret" };
        const string encrypt = """{"op":"encrypt_document","ownerPasswordEnv":"PDF_OWNER","userPasswordEnv":"PDF_NEW"}""";
        File.WriteAllText(workspace.File("latin.json"), $$"""{"ops":[{"op":"set_properties","title":"Quarterly Résumé"},{{encrypt}}]}""");
        File.WriteAllText(workspace.File("rotate.json"), """{"ops":[{"op":"rotate_pages","pages":"1","rotation":90}]}""");
        File.WriteAllText(workspace.File("chinese.json"), $$"""{"ops":[{"op":"set_properties","title":"采购申请"},{{encrypt}}]}""");
        File.WriteAllText(workspace.File("chinese-title.json"), """{"ops":[{"op":"set_properties","title":"采购申请"}]}""");

        CliResult latin = workspace.RunWithEnv(passwords,
            ["pdf", "edit", input, "--ops", "latin.json", "--out", "latin.pdf", "--license-mode", "evaluation", "--output-format", "json"]);
        CliResult rotated = workspace.RunWithEnv(passwords,
            ["pdf", "edit", input, "--ops", "rotate.json", "--out", "rotated.pdf", "--encrypt-env", "PDF_NEW", "--license-mode", "evaluation", "--output-format", "json"]);
        CliResult chinese = workspace.RunWithEnv(passwords,
            ["pdf", "edit", input, "--ops", "chinese.json", "--out", "chinese.pdf", "--license-mode", "evaluation", "--output-format", "json"]);

        Assert.True(latin.ExitCode == 0, latin.StdErr);
        Assert.True(rotated.ExitCode == 0, rotated.StdErr);
        Assert.True(chinese.ExitCode == 0, chinese.StdErr);
        Assert.Equal(1, Notices(workspace.File("latin.pdf"), "new-secret"));
        Assert.Equal(1, Notices(workspace.File("rotated.pdf"), "new-secret"));
        Assert.Equal(2, Notices(workspace.File("chinese.pdf"), "new-secret"));
        using var reopened = new Document(workspace.File("chinese.pdf"), "new-secret");
        Assert.Equal("采购申请", reopened.Info.Title);
        using var latinReopened = new Document(workspace.File("latin.pdf"), "new-secret");
        Assert.Equal("Quarterly Résumé", latinReopened.Info.Title);

        // An input that already has a Chinese title: encrypt_document alone saves once, while
        // --encrypt-env encrypts a saved copy that keeps the title.
        File.WriteAllText(workspace.File("encrypt.json"), $$"""{"ops":[{{encrypt}}]}""");
        CliResult titled = workspace.RunWithEnv(passwords,
            ["pdf", "edit", input, "--ops", "chinese-title.json", "--out", "titled.pdf", "--output-format", "json"]);
        Assert.True(titled.ExitCode == 0, titled.StdErr);
        int before = Notices(workspace.File("titled.pdf"), password: null);
        CliResult encrypted = workspace.RunWithEnv(passwords,
            ["pdf", "edit", "titled.pdf", "--ops", "encrypt.json", "--out", "titled.encrypted.pdf", "--license-mode", "evaluation", "--output-format", "json"]);
        CliResult protectedCopy = workspace.RunWithEnv(passwords,
            ["pdf", "edit", "titled.pdf", "--ops", "rotate.json", "--out", "titled.protected.pdf", "--encrypt-env", "PDF_NEW", "--license-mode", "evaluation", "--output-format", "json"]);
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        Assert.True(protectedCopy.ExitCode == 0, protectedCopy.StdErr);
        Assert.Equal(before + 1, Notices(workspace.File("titled.encrypted.pdf"), "new-secret"));
        Assert.Equal(before + 2, Notices(workspace.File("titled.protected.pdf"), "new-secret"));
        using var protectedReopened = new Document(workspace.File("titled.protected.pdf"), "new-secret");
        Assert.Equal("采购申请", protectedReopened.Info.Title);
    }

    /// <summary>How many evaluation notices the first page of a file shows.</summary>
    private static int Notices(string path, string? password)
    {
        using var document = password is null ? new Document(path) : new Document(path, password);
        // Each save prints the notice at the same place, so the copies are counted as fragments.
        var absorber = new TextFragmentAbsorber("Evaluation Only");
        document.Pages[1].Accept(absorber);
        return absorber.TextFragments.Count;
    }

    /// <summary>
    /// Only a saved PDF is reopened to read its notice; a file of any other format is left to the
    /// document it was written from.
    /// </summary>
    [Fact]
    public void InspectSaved_ReopensOnlyAPdf()
    {
        using var fixture = new PdfEngineFixture();
        string input = MarkedOnFirstPage(fixture);
        var profile = new PdfEvaluationProfile();

        Assert.True(profile.InspectSaved(input, "pdf")?.Any);
        Assert.Null(profile.InspectSaved(input, "pdfa-1b"));
        Assert.Null(profile.InspectSaved(input, "docx"));
        Assert.Null(profile.InspectSaved(input, "png"));
    }

    private static Warning Disclosure(ResultEnvelope result) =>
        Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.EvaluationInputMarked);

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
