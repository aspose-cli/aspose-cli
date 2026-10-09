using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Render, split, extract, validate and sign through the built CLI with <c>--output json</c>, so
/// the workspace checks each real result against the schema generated from its record.
/// </summary>
public sealed class PdfResultWorkflowTests : IDisposable
{
    private const string CertificatePasswordVariable = "PDF_RESULT_CERTIFICATE_PASSWORD";
    private const string CertificatePassword = "result-certificate-password";
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void RenderSplitExtractValidateAndSign_ReportResultsTheirSchemasAccept()
    {
        using (var document = new Document())
        {
            foreach (string text in new[] { "First page text.", "Second page text." })
            {
                document.Pages.Add().Paragraphs.Add(new TextFragment(text));
            }

            document.Save(_workspace.File("report.pdf"));
        }

        JsonNode render = Succeeds(_workspace.Run(
            "pdf", "render", "report.pdf", "--pages", "1", "--dpi", "72", "--grid", "50",
            "--out", "page.png", "--output", "json"));
        Assert.Equal(72, render["dpi"]!.GetValue<int>());
        Assert.Equal(50, render["grid"]!["spacing"]!.GetValue<int>());
        Assert.Equal(1, Assert.Single(render["outputs"]!.AsArray())!["page"]!.GetValue<int>());

        JsonNode split = Succeeds(_workspace.Run(
            "pdf", "split", "report.pdf", "--every", "1", "--out-dir", "parts", "--output", "json"));
        Assert.Equal(["1", "2"], split["outputs"]!.AsArray().Select(static part => part!["pages"]!.GetValue<string>()));

        JsonNode extract = Succeeds(_workspace.Run(
            "pdf", "extract", "report.pdf", "--what", "text", "--out-dir", "text", "--output", "json"));
        Assert.Equal("text", extract["what"]!.GetValue<string>());
        Assert.NotEmpty(extract["items"]!.AsArray());

        JsonNode validate = Succeeds(_workspace.Run(
            "pdf", "validate", "report.pdf", "--profile", "pdfa-1b", "--output", "json"));
        Assert.Equal("pdfa-1b", validate["profile"]!.GetValue<string>());

        WriteCertificate(_workspace.File("signer.pfx"));
        JsonNode sign = Succeeds(_workspace.RunWithEnv(
            new Dictionary<string, string?>(StringComparer.Ordinal) { [CertificatePasswordVariable] = CertificatePassword },
            "pdf", "sign", "report.pdf", "--certificate", "signer.pfx",
            "--certificate-password-env", CertificatePasswordVariable,
            "--visible", "--page", "2", "--rect", "36,48,180,60", "--out", "signed.pdf", "--output", "json"));
        Assert.True(sign["signature"]!["signed"]!.GetValue<bool>());
        Assert.Equal(2, sign["page"]!.GetValue<int>());
        Assert.NotNull(sign["rect"]);
    }

    private static JsonNode Succeeds(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return result.Json();
    }

    private static void WriteCertificate(string path)
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Aspose CLI PDF Result Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, CertificatePassword));
    }
}
