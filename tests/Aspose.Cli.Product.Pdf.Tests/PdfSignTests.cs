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

        var result = PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
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

        var info = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = output,
            Details = ["signatures"],
        });
        Assert.True(info.Pdf.Signed);
        Assert.Single(info.Signatures!);
        Assert.True(info.Signatures![0].Signed);
        Assert.True(info.Signatures[0].Valid);
    }

    [Fact]
    public void Sign_PlacesTheVisibleRectangleFromTheTopLeftCornerOfThePage()
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.CreateCertificate(certificatePassword);
        string output = fixture.File("signed.pdf");

        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret(certificatePassword),
            Visible = true,
            Rect = new PdfSignatureRect(36, 40, 180, 60),
        });

        // The signature's widget lies where redact_area and render --grid measure the same numbers.
        PdfFormField field = Assert.Single(fixture.Forms(output).Fields, static field => field.Kind == PdfFormFieldKinds.Signature);
        Assert.Equal(1, field.Page);
        Assert.Equal(36, field.Rect!.X, 1);
        Assert.Equal(40, field.Rect.Y, 1);
        Assert.Equal(180, field.Rect.Width, 1);
        Assert.Equal(60, field.Rect.Height, 1);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Sign_PlacesTheVisibleRectangleOnARotatedPageAsItIsSeen(int rotation)
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string input = fixture.File("page.pdf");
        using (var document = new Aspose.Pdf.Document())
        {
            Aspose.Pdf.Page page = document.Pages.Add();
            page.SetPageSize(400, 600);
            page.Rotate = rotation switch { 90 => Aspose.Pdf.Rotation.on90, 180 => Aspose.Pdf.Rotation.on180, _ => Aspose.Pdf.Rotation.on270 };
            document.Save(input);
        }

        string output = fixture.File("signed.pdf");
        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = fixture.CreateCertificate(certificatePassword),
            CertificatePassword = new Secret(certificatePassword),
            Visible = true,
            Rect = new PdfSignatureRect(36, 40, 180, 60),
        });

        PdfRect rect = Assert.Single(fixture.Forms(output).Fields, static field => field.Kind == PdfFormFieldKinds.Signature).Rect!;
        Assert.Equal((36d, 40d, 180d, 60d), (Math.Round(rect.X), Math.Round(rect.Y), Math.Round(rect.Width), Math.Round(rect.Height)));
    }

    [Theory]
    [InlineData(100, 50, 0)]
    [InlineData(595, 842, 90)]
    [InlineData(150, 300, 270)]
    [InlineData(400, 600, 180)]
    public void Sign_PlacesTheDefaultRectangleInsideTheBottomLeftOfTheVisiblePage(double width, double height, int rotation)
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string input = fixture.File("page.pdf");
        using (var document = new Aspose.Pdf.Document())
        {
            Aspose.Pdf.Page page = document.Pages.Add();
            page.SetPageSize(width, height);
            page.Rotate = rotation switch { 90 => Aspose.Pdf.Rotation.on90, 270 => Aspose.Pdf.Rotation.on270, _ => Aspose.Pdf.Rotation.None };
            document.Save(input);
        }

        string output = fixture.File("signed.pdf");
        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = fixture.CreateCertificate(certificatePassword),
            CertificatePassword = new Secret(certificatePassword),
            Visible = true,
        });

        // The visible page is the rotated one: its width and height swap at 90 and 270 degrees.
        (double visibleWidth, double visibleHeight) = rotation is 90 or 270 ? (height, width) : (width, height);
        PdfRect rect = Assert.Single(fixture.Forms(output).Fields, static field => field.Kind == PdfFormFieldKinds.Signature).Rect!;
        Assert.InRange(rect.X, 0, visibleWidth / 2);
        Assert.InRange(rect.X + rect.Width, 0, visibleWidth + 0.5);
        Assert.InRange(rect.Y, 0, visibleHeight);
        Assert.InRange(rect.Y + rect.Height, visibleHeight / 2, visibleHeight + 0.5);
        Assert.True(rect.Width > 0 && rect.Height > 0);
    }

    [Fact]
    public void Sign_WrongCertificatePasswordWritesNoOutput()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("correct-password");
        string output = fixture.File("should-not-exist.pdf");

        var exception = Assert.Throws<Sdk.Errors.CliException>(() => PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
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
            exception = Assert.Throws<Sdk.Errors.CliException>(() => PdfSign.Run(fixture.Session, new PdfSignRequest
            {
                Input = input,
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
