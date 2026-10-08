using Aspose.Cli.Product.Pdf.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfSignTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sign_AppliesAndVerifiesInvisibleAndVisibleSignatures(bool visible)
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string input = fixture.CreateDocument("unsigned.pdf", pages: 2);
        string certificate = fixture.CreateCertificate(certificatePassword);
        string output = fixture.File(visible ? "visible.pdf" : "invisible.pdf");

        var result = fixture.Engine.Sign(input, new PdfSignRequest
        {
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret(certificatePassword),
            Visible = visible,
            Page = 2,
            Rect = visible ? new PdfSignatureRect(36, 48, 180, 60) : null,
            Reason = "Approved for test",
            Location = "Test lab",
            Contact = "quality@example.invalid",
        });

        Assert.True(File.Exists(output));
        Assert.True(result.Signature.Signed);
        Assert.True(result.Signature.Valid);
        Assert.Equal(visible, result.Visible);
        Assert.Equal(2, result.Page);
        Assert.Equal(visible, result.Rect is not null);

        var info = fixture.Engine.GetInfo(output, new PdfInfoRequest
        {
            Details = ["signatures"],
        });
        Assert.True(info.Pdf.Signed);
        Assert.Single(info.Signatures!);
        Assert.True(info.Signatures![0].Signed);
        Assert.True(info.Signatures[0].Valid);
    }

    [Fact]
    public void Sign_WrongCertificatePasswordWritesNoOutput()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("correct-password");
        string output = fixture.File("should-not-exist.pdf");

        var exception = Assert.Throws<Sdk.Errors.CliException>(() => fixture.Engine.Sign(input, new PdfSignRequest
        {
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("wrong-password"),
        }));
        Assert.Equal("SIGN_CERT_INVALID", exception.Code.Name);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    /// A certificate another program holds is reported as the locked file it is, read once through
    /// the SDK, not as an invalid certificate.
    /// </summary>
    [Fact]
    public void Sign_LockedCertificateIsTheFileLockedError()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("correct-password");
        string output = fixture.File("locked-certificate.pdf");

        Sdk.Errors.CliException exception;
        using (new FileStream(certificate, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            exception = Assert.Throws<Sdk.Errors.CliException>(() => fixture.Engine.Sign(input, new PdfSignRequest
            {
                Output = TestOutput.At(output),
                CertificatePath = certificate,
                CertificatePassword = new Secret("correct-password"),
            }));
        }

        Assert.Equal(Sdk.Errors.ErrorCodes.FileLocked, exception.Code);
        Assert.Equal(Path.GetFullPath(certificate), exception.Details!["path"]!.GetValue<string>());
        Assert.False(File.Exists(output));
    }
}
