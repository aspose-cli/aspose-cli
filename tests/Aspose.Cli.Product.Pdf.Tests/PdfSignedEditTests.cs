using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An edit of a signed PDF reports SIGNATURE_INVALIDATED exactly when a signature that was intact
/// in its input is no longer intact in its output, as a read of the output finds it, whatever the
/// operations report they changed.
/// </summary>
public sealed class PdfSignedEditTests
{
    public static TheoryData<string> Operations => ["optimize", "redact-no-match", "set-properties"];

    [Theory]
    [MemberData(nameof(Operations))]
    public void Edit_OfASignedPdf_WarnsExactlyWhenTheOutputSignatureIsNoLongerIntact(string name)
    {
        using var fixture = new PdfEngineFixture();
        string signed = Signed(fixture);
        PdfOp operation = name switch
        {
            "optimize" => new OptimizeDocumentOp(),
            "redact-no-match" => new RedactTextOp { Find = "not in the document" },
            _ => new SetPropertiesOp { Title = "Signed report" },
        };
        string output = fixture.File("edited.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = signed,
            Batch = new PdfOpsBatch { Ops = [operation] },
            Output = TestOutput.At(output),
        });

        bool warned = (result.Warnings ?? []).Any(static warning => warning.Code.Name == "SIGNATURE_INVALIDATED");
        Assert.Equal(!Intact(fixture, output), warned);
    }

    /// <summary>A dry run writes nothing, so it reports the intact signature as one the edit would invalidate.</summary>
    [Fact]
    public void DryRun_OfASignedPdf_WarnsThatTheEditWouldInvalidateItsSignature()
    {
        using var fixture = new PdfEngineFixture();
        string signed = Signed(fixture);
        string output = fixture.File("edited.pdf");

        PdfEditResult result = PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = signed,
            Batch = new PdfOpsBatch { Ops = [new RedactTextOp { Find = "not in the document" }] },
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { DryRun = true },
        });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == "SIGNATURE_INVALIDATED");
        Assert.Contains("would no longer be intact", warning.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private static string Signed(PdfEngineFixture fixture)
    {
        const string certificatePassword = "test-certificate-password";
        string signed = fixture.File("signed.pdf");
        PdfSign.Run(fixture.Session, new PdfSignRequest
        {
            Input = fixture.CreateDocument("unsigned.pdf", pages: 1),
            Output = TestOutput.At(signed),
            CertificatePath = fixture.CreateCertificate(certificatePassword),
            CertificatePassword = new Secret(certificatePassword),
        });
        Assert.True(Intact(fixture, signed));
        return signed;
    }

    private static bool Intact(PdfEngineFixture fixture, string path) =>
        ResultJson.Intact(Assert.Single(PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["signatures"] }).Signatures!)) == true;
}
