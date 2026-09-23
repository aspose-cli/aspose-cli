using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Signing verifies what it added; PDF/A results state what the SDK found.</summary>
public sealed class PdfConformanceTests
{
    [Fact]
    public void Sign_VerifiesTheSignatureItAddedRatherThanAnEarlierOne()
    {
        using var fixture = new PdfEngineFixture();
        const string password = "test-certificate-password";
        string certificate = fixture.CreateCertificate(password);
        string input = fixture.CreateDocument("twice.pdf", pages: 1);
        PdfSignResult first = Sign(fixture, input, certificate, password, "first.pdf");

        PdfSignResult second = Sign(fixture, first.Output.Path, certificate, password, "second.pdf");

        Assert.True(second.Signature.Signed);
        Assert.NotEqual(first.Signature.Name, second.Signature.Name);
        PdfInfoResult info = fixture.Engine.GetInfo(second.Output.Path, new PdfInfoRequest { Details = ["signatures"] });
        Assert.Contains(info.Signatures!, signature => signature.Name == second.Signature.Name && signature.Signed);
    }

    [Fact]
    public void Validate_ReportsClauseSeverityAndMessageForEachProblem()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("plain.pdf", pages: 1);

        PdfValidateResult result = fixture.Engine.Validate(input, new PdfValidateRequest { Profile = "pdfa-1b" });

        Assert.False(result.Valid);
        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, static issue =>
        {
            Assert.DoesNotContain("<", issue, StringComparison.Ordinal);
            Assert.Matches(new Regex(@"^[\d.]+ \((error|warning)(, page \d+)?\): \S"), issue);
        });
    }

    [Fact]
    public void AnUnsuccessfulPdfaConversion_IsRejectedWithTheProblemsTheSdkCouldNotFix()
    {
        // The SDK cannot convert an encrypted document in place; its own result and log
        // drive the decision. (The convert command converts an unencrypted page copy.)
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument("user-secret", "owner-secret", "encrypted.pdf");
        using var document = new Aspose.Pdf.Document(input, "owner-secret");
        using var log = new MemoryStream();
        bool converted = document.Convert(log, Aspose.Pdf.PdfFormat.PDF_A_1B, Aspose.Pdf.ConvertErrorAction.Delete);

        CliException error = Assert.Throws<CliException>(() => PdfComplianceLog.EnsureConverted(converted, log, "pdfa-1b"));

        Assert.Equal("PDFA_CONVERSION_FAILED", error.Code.Name);
        Assert.Contains("1 problem(s) cannot be fixed automatically", error.Message, StringComparison.Ordinal);
        Assert.Single(error.Details!["problems"]!.AsArray());
    }

    [Fact]
    public void ASuccessfulPdfaConversion_IsPublished()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("convertible.pdf", pages: 1);

        PdfConvertResult result = fixture.Engine.Convert(input, new PdfConvertRequest
        {
            TargetFormatId = "pdfa-2b",
            OutputPath = fixture.File("convertible.pdfa.pdf"),
        });

        Assert.True(File.Exists(Assert.Single(result.Outputs).Path));
    }

    private static PdfSignResult Sign(PdfEngineFixture fixture, string input, string certificate, string password, string output) =>
        fixture.Engine.Sign(input, new PdfSignRequest
        {
            CertificatePath = certificate,
            CertificatePassword = password,
            OutputPath = fixture.File(output),
            Page = 1,
        });
}
