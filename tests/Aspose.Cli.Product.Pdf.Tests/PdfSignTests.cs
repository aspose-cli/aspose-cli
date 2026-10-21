using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
        Assert.True(ResultJson.Intact(result.Signature));
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
        Assert.True(ResultJson.Intact(info.Signatures[0]));
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
            page.Rotate = rotation switch { 90 => Aspose.Pdf.Rotation.on90, 180 => Aspose.Pdf.Rotation.on180, 270 => Aspose.Pdf.Rotation.on270, _ => Aspose.Pdf.Rotation.None };
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

    /// <summary>
    /// PDF-SIGN-FULL-FONT: a signature whose appearance text is Latin-1 draws it in a standard
    /// font, so signing does not embed a whole font program into the file.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sign_WithLatinTextAddsNoWholeFont(bool visible)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("small.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("pw");
        string output = fixture.File("small.signed.pdf");

        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = visible,
            Reason = "Approved",
            Location = "Zurich",
        });

        long growth = new FileInfo(output).Length - new FileInfo(input).Length;
        Assert.True(growth < 200_000, $"signing added {growth} bytes");
        using var signed = new Aspose.Pdf.Document(output);
        Assert.DoesNotContain(signed.FontUtilities.GetAllFonts(), static font => font.IsEmbedded && !font.IsSubset);
        var verify = new Aspose.Pdf.Facades.PdfFileSignature(signed);
        Assert.All(verify.GetSignatureNames(), name => Assert.True(verify.VerifySignature(name)));
    }

    /// <summary>
    /// PDF-SIGN-FULL-FONT: appearance text beyond ASCII makes the engine embed a whole font, which
    /// a warning discloses with the growth; ASCII text warns of nothing.
    /// </summary>
    [Theory]
    [InlineData("Zürich", true)]
    [InlineData("上海", true)]
    [InlineData("Zurich", false)]
    public void Sign_WarnsWhenTheAppearanceEmbedsAWholeFont(string location, bool warns)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("located.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("pw");

        PdfSignResult result = PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("located.signed.pdf")),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Reason = "Approved",
            Location = location,
        });

        Warning? warning = result.Warnings?.SingleOrDefault(static item => item.Code.Name == "SIGNATURE_FONT_EMBEDDED");
        Assert.Equal(warns, warning is not null);
        if (warning is not null)
        {
            Assert.Contains("--location", warning.Message, StringComparison.Ordinal);
            Assert.Contains("ASCII", warning.Hint, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Sign_RectangleBeyondThePageIsAnInvalidRectOption()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("outside.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("pw");
        string output = fixture.File("outside.signed.pdf");

        var exception = Assert.Throws<Sdk.Errors.CliException>(() => PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = true,
            Rect = new PdfSignatureRect(36, 900, 180, 60),
        }));

        Assert.Equal("OPTION_INVALID", exception.Code.Name);
        Assert.Equal("--rect", exception.Details!["option"]!.GetValue<string>());
        Assert.False(File.Exists(output));
    }

    /// <summary>A certificate the system cannot read, or cannot sign with now, is an invalid certificate, not a wrong password.</summary>
    [Theory]
    [InlineData("garbage", "not a PKCS#12 file")]
    [InlineData("truncated", "not a PKCS#12 file")]
    [InlineData("no-key", "no private key")]
    [InlineData("expired", "valid only from")]
    public void Sign_UnusableCertificateIsSignCertInvalid(string kind, string reason)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.File(kind + ".pfx");
        byte[] valid = File.ReadAllBytes(fixture.CreateCertificate("pw", "valid.pfx"));
        File.WriteAllBytes(certificate, kind switch
        {
            "garbage" => System.Text.Encoding.ASCII.GetBytes("this is not a certificate"),
            "truncated" => valid[..(valid.Length / 2)],
            "no-key" => PublicOnly(valid),
            _ => Expired(),
        });

        var exception = Assert.Throws<Sdk.Errors.CliException>(() => PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("unsigned.signed.pdf")),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
        }));

        Assert.Equal("SIGN_CERT_INVALID", exception.Code.Name);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);

        static byte[] PublicOnly(byte[] pfx)
        {
            using var full = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(pfx, "pw");
            using var bare = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(full.RawData);
            return bare.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12, "pw");
        }

        static byte[] Expired()
        {
            using var key = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=Expired", key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            using var old = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
            return old.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "pw");
        }
    }

    /// <summary>
    /// The certificate is read as PKCS #12 under a .pfx or .p12 name, in any case; any other
    /// name is an unsupported format named by its extension, and nothing is written.
    /// </summary>
    [Theory]
    [InlineData("signing.P12", null)]
    [InlineData("SIGNING.PFX", null)]
    [InlineData("signing.pem", "pem")]
    [InlineData("signing.PEM", "PEM")]
    [InlineData("signing", "")]
    public void Sign_ReadsACertificateOnlyUnderAPkcs12Name(string name, string? requested)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = fixture.CreateCertificate("correct-password", name);
        string output = fixture.File("signed.pdf");

        Exception? error = Record.Exception(() => PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("correct-password"),
        }));

        if (requested is null)
        {
            Assert.Null(error);
            Assert.True(File.Exists(output));
            return;
        }
        var refused = Assert.IsType<Sdk.Errors.CliException>(error);
        Sdk.Errors.CliException expected = Sdk.Errors.CliErrors.FormatUnsupported(requested, ["pfx", "p12"]);
        Assert.Equal(Sdk.Errors.ErrorCodes.FormatUnsupported, refused.Code);
        Assert.Equal(expected.Message, refused.Message);
        Assert.Equal(expected.Hint, refused.Hint);
        Assert.Equal(expected.Details!.ToJsonString(), refused.Details!.ToJsonString());
        Assert.False(File.Exists(output));
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
        Assert.Equal("PASSWORD_INVALID", exception.Code.Name);
        Assert.Equal(certificate, exception.Details!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.Contains("--certificate-password-env", exception.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    /// The visible appearance names the signer by the common name of the certificate's subject,
    /// in a font size small enough for the rectangle's width, and draws no line for contact
    /// information the command was not given.
    /// </summary>
    [Fact]
    public void Sign_ShowsTheWholeCommonNameOfALongSubjectAndNoEmptyContactLine()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = Certificate(fixture, "CN=Zhang Wei (Chief Financial Officer), OU=Finance, O=Contoso Holdings Limited, L=Shanghai, C=CN");
        string output = fixture.File("signed.pdf");

        PdfSignResult result = PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = true,
            Rect = new PdfSignatureRect(330, 600, 220, 80),
            Reason = "Approved",
            Location = "Shanghai",
        });

        (Aspose.Pdf.Rectangle box, List<Aspose.Pdf.Text.TextFragment> lines) = AppearanceLines(output);
        Aspose.Pdf.Text.TextFragment subject = Assert.Single(lines, static line => line.Text.StartsWith("Digitally signed by", StringComparison.Ordinal));
        Assert.Contains("Zhang Wei (Chief Financial Officer)", subject.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("OU=Finance", subject.Text, StringComparison.Ordinal);
        Assert.All(lines, line => Assert.True(line.Rectangle.URX <= box.URX + 0.5, $"'{line.Text}' ends at {line.Rectangle.URX}, beyond {box.URX}"));
        Assert.Contains(lines, static line => line.Text == "Reason: Approved");
        Assert.DoesNotContain(lines, static line => line.Text.StartsWith("Contact", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code.Name == "SIGNATURE_TEXT_CLIPPED");
    }

    /// <summary>
    /// A line too long for the rectangle's width at the smallest font size, in Latin, Cyrillic or
    /// Chinese text, or more lines than its height holds at 12 points each, is clipped, and a
    /// warning says so.
    /// </summary>
    [Theory]
    [InlineData("CN=Procurement Approval Authority Procurement Approval Authority Procurement Approval Authority Procurement Approval Authority", "Approved", 180, 60)]
    [InlineData("CN=Procurement Approval Authority", "Approved", 180, 30)]
    [InlineData("CN=Иван Петрович Сидоров", "Approved", 120, 60)]
    [InlineData("CN=Signer", "批准采购申请批准采购申请批准采购申请批准", 120, 60)]
    public void Sign_WarnsWhenTheAppearanceTextCannotFitTheRectangle(string subject, string reason, double width, double height)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = Certificate(fixture, subject);

        PdfSignResult result = PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("clipped.pdf")),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = true,
            Rect = new PdfSignatureRect(36, 36, width, height),
            Reason = reason,
            Location = "Shanghai",
        });

        Warning clipped = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "SIGNATURE_TEXT_CLIPPED");
        Assert.Contains("--rect", clipped.Hint, StringComparison.Ordinal);
    }

    /// <summary>A certificate whose subject has no common name is named by its whole subject.</summary>
    [Fact]
    public void Sign_ShowsTheWholeSubjectOfACertificateWithoutACommonName()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = Certificate(fixture, "O=Contoso Holdings, C=CN");
        string output = fixture.File("signed.pdf");

        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = true,
            Rect = new PdfSignatureRect(330, 600, 220, 80),
            Reason = "Approved",
        });

        (_, List<Aspose.Pdf.Text.TextFragment> lines) = AppearanceLines(output);
        Aspose.Pdf.Text.TextFragment subject = Assert.Single(lines, static line => line.Text.StartsWith("Digitally signed by", StringComparison.Ordinal));
        Assert.Contains("O=Contoso Holdings", subject.Text, StringComparison.Ordinal);
        Assert.Contains("C=CN", subject.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// PDF-SIGN-CJK-SIGNER-FONT: a signer name in Chinese is drawn although the reason has no
    /// Chinese text, which would otherwise leave the engine drawing in Arial, without the name.
    /// </summary>
    [Theory]
    [InlineData("Approved")]
    [InlineData("Café")]
    public void Sign_ShowsAChineseSignerNameWhenNoOtherTextIsChinese(string reason)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = Certificate(fixture, "CN=张伟");
        string output = fixture.File("signed.pdf");

        PdfSignResult result = PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(output),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = true,
            Rect = new PdfSignatureRect(330, 600, 220, 80),
            Reason = reason,
        });

        (_, List<Aspose.Pdf.Text.TextFragment> lines) = AppearanceLines(output);
        Aspose.Pdf.Text.TextFragment subject = Assert.Single(lines, static line => line.Text.StartsWith("Digitally signed by", StringComparison.Ordinal));
        Assert.Contains("张伟", subject.Text, StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Text == $"Reason: {reason}");
        Assert.Contains(result.Warnings ?? [], static warning => warning.Code.Name == "SIGNATURE_FONT_EMBEDDED");
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code.Name == "SIGNATURE_TEXT_CLIPPED");
    }

    /// <summary>
    /// PDF-SIGN-CJK-SIGNER-FONT: without a font that draws Chinese, a Chinese signer name cannot
    /// be shown, and the clipped-text warning says so.
    /// </summary>
    [Fact]
    public void Sign_WarnsThatAChineseSignerNameIsNotShownWithoutAChineseFont()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("unsigned.pdf", pages: 1);
        string certificate = Certificate(fixture, "CN=张伟");
        List<string> probed = [];

        PdfSignResult result = PdfSign.Sign(fixture.Session, new PdfSignRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("signed.pdf")),
            CertificatePath = certificate,
            CertificatePassword = new Secret("pw"),
            Visible = true,
            Rect = new PdfSignatureRect(330, 600, 220, 80),
            Reason = "Approved",
        }, family =>
        {
            probed.Add(family);
            return false;
        });

        Assert.Equal(["SimSun", "Microsoft YaHei"], probed);
        Warning clipped = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "SIGNATURE_TEXT_CLIPPED");
        Assert.Contains("signer name 'CN=张伟'", clipped.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--rect", clipped.Hint, StringComparison.Ordinal);
    }

    /// <summary>A PKCS#12 certificate with the password "pw" whose subject is <paramref name="subject"/>.</summary>
    private static string Certificate(PdfEngineFixture fixture, string subject)
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            subject, key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
        string path = fixture.File("subject.pfx");
        File.WriteAllBytes(path, certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "pw"));
        return path;
    }

    /// <summary>The signature field's rectangle and the lines of text its appearance draws, read from a flattened copy.</summary>
    private static (Aspose.Pdf.Rectangle Box, List<Aspose.Pdf.Text.TextFragment> Lines) AppearanceLines(string path)
    {
        using var document = new Aspose.Pdf.Document(path);
        Aspose.Pdf.Rectangle box = Assert.Single(document.Form.Fields.OfType<Aspose.Pdf.Forms.SignatureField>()).Rect;
        document.Form.Flatten();
        var absorber = new Aspose.Pdf.Text.TextFragmentAbsorber();
        document.Pages[1].Accept(absorber);
        List<Aspose.Pdf.Text.TextFragment> lines = [.. absorber.TextFragments.Where(fragment =>
            fragment.Rectangle.LLY >= box.LLY - 12 && fragment.Rectangle.URY <= box.URY + 12 && fragment.Rectangle.LLX >= box.LLX - 0.5)];
        Assert.NotEmpty(lines);
        return (box, lines);
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

    /// <summary>
    /// A revision appended after signing can replace what a page shows while the signed bytes
    /// still verify: a signature is intact only when it also covers the whole file.
    /// </summary>
    [Fact]
    public void Inspect_ReportsASignatureNotIntactOnceARevisionIsAppendedAfterIt()
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string signed = fixture.File("signed.pdf");
        PdfSignResult result = PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = fixture.CreateDocument("unsigned.pdf", pages: 1),
            Output = TestOutput.At(signed),
            CertificatePath = fixture.CreateCertificate(certificatePassword),
            CertificatePassword = new Secret(certificatePassword),
            Page = 1,
        });
        string tampered = fixture.File("tampered.pdf");
        AppendRevision(signed, tampered, "PAY 1000000 TO MALLORY");

        PdfSignatureInfo Signature(string path) => Assert.Single(PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = path,
            Details = ["signatures"],
        }).Signatures!);

        Assert.True(ResultJson.Intact(result.Signature));
        Assert.True(ResultJson.Intact(Signature(signed)));
        using (var document = new Aspose.Pdf.Document(tampered))
        {
            var text = new Aspose.Pdf.Text.TextAbsorber();
            document.Pages[1].Accept(text);
            Assert.Contains("MALLORY", text.Text, StringComparison.Ordinal);
        }

        PdfSignatureInfo after = Signature(tampered);
        Assert.True(after.Signed);
        Assert.False(ResultJson.Intact(after));
    }

    /// <summary>
    /// Signing a signed file again appends the new signature after the first, so the first no
    /// longer covers the whole file and only the last is intact.
    /// </summary>
    [Fact]
    public void Inspect_ReportsOnlyTheLastOfTwoSignaturesIntact()
    {
        using var fixture = new PdfEngineFixture();
        const string certificatePassword = "test-certificate-password";
        string certificate = fixture.CreateCertificate(certificatePassword);
        string once = fixture.File("once.pdf");
        string twice = fixture.File("twice.pdf");
        foreach ((string input, string output) in new[] { (fixture.CreateDocument("unsigned.pdf", pages: 1), once), (once, twice) })
        {
            PdfSign.Run(fixture.Session, new PdfSignRequest
            {
                Input = input,
                Output = TestOutput.At(output),
                CertificatePath = certificate,
                CertificatePassword = new Secret(certificatePassword),
                Page = 1,
            });
        }

        IReadOnlyList<PdfSignatureInfo> signatures = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = twice,
            Details = ["signatures"],
        }).Signatures!;

        Assert.Equal(2, signatures.Count);
        Assert.Equal(new bool?[] { false, true }, signatures.OrderBy(static signature => signature.Name, StringComparer.Ordinal).Select(ResultJson.Intact));
    }

    /// <summary>
    /// Appends an incremental update to <paramref name="source"/> that gives its last page a new
    /// content stream drawing <paramref name="text"/>, as an attacker edits a signed file.
    /// </summary>
    private static void AppendRevision(string source, string target, string text)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        string pdf = Encoding.Latin1.GetString(File.ReadAllBytes(source));
        int previous = int.Parse(Regex.Matches(pdf, @"startxref\s+(\d+)")[^1].Groups[1].Value, invariant);
        Match page = Regex.Matches(pdf, @"(\d+) 0 obj\s*(<<(?:(?!endobj).)*?/Type\s*/Page[^s](?:(?!endobj).)*?)endobj", RegexOptions.Singleline)[^1];
        int size = Regex.Matches(pdf, @"/Size (\d+)").Max(match => int.Parse(match.Groups[1].Value, invariant));
        string content = $"BT /Tampered 28 Tf 1 0 0 1 20 300 Tm ({text}) Tj ET";
        string body = Regex.Replace(page.Groups[2].Value.Trim(), @"/Contents\s*(\[[^\]]*\]|\d+ 0 R)", $"/Contents {size} 0 R");
        body = new Regex(@"/Font\s*<<").Replace(body, $"/Font << /Tampered {size + 1} 0 R ", 1);
        var update = new StringBuilder(pdf);
        if (!pdf.EndsWith('\n'))
        {
            update.Append('\n');
        }

        var offsets = new SortedDictionary<int, int>
        {
            [int.Parse(page.Groups[1].Value, invariant)] = update.Length,
        };
        update.Append($"{page.Groups[1].Value} 0 obj\n{body}\nendobj\n");
        offsets[size] = update.Length;
        update.Append($"{size} 0 obj\n<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n");
        offsets[size + 1] = update.Length;
        update.Append($"{size + 1} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        int xref = update.Length;
        update.Append("xref\n");
        foreach ((int number, int offset) in offsets)
        {
            update.Append(invariant, $"{number} 1\n{offset:0000000000} 00000 n \n");
        }

        string root = Regex.Matches(pdf, @"/Root (\d+ 0 R)")[^1].Groups[1].Value;
        update.Append($"trailer\n<< /Size {size + 2} /Root {root} /Prev {previous} >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(target, Encoding.Latin1.GetBytes(update.ToString()));
    }
}
