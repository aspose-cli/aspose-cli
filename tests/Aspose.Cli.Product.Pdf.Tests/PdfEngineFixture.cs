using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.TestKit;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Shared real-engine setup. The test license state is applied before any test authors a
/// document, so inputs are written in the state the engine reads them in.
/// </summary>
public sealed class PdfEngineFixture : IDisposable
{
    public ILicenseGate Gate { get; } = TestLicense.Apply(
        static (resolution, environment) => new PdfLicenseGate(resolution, environment));

    /// <summary>A new session, as one invocation's activation creates it.</summary>
    internal PdfSession Session =>
        ProductTestBudgets.StartEngine<PdfModule, PdfSession>(
            (budgets, writer) => new PdfSession(Outputs(writer), budgets, new PdfDocumentLoader(budgets)));

    /// <summary>The write pipeline of one invocation, as the product binding creates it.</summary>
    internal OutputPipeline<Document> Outputs(SafeFileWriter writer) => new(Gate, new PdfEvaluationProfile(), writer);

    /// <summary>
    /// Runs one handler as a command does: on its own session, whose write pipeline adds its
    /// evaluation disclosure to the result.
    /// </summary>
    internal TResult Disclosed<TResult>(Func<PdfSession, TResult> call)
        where TResult : ResultEnvelope
    {
        PdfSession session = Session;
        return (TResult)session.Outputs.Disclose(call(session));
    }

    public LicenseState LicenseState => Gate.EnsureApplied();

    public TempDirectory Temp { get; } = new();

    public string File(string fileName) => Temp.File(fileName);

    public string CreateDocument(string fileName = "document.pdf", int pages = 2)
    {
        string path = File(fileName);
        using var document = new Document();
        for (int pageNumber = 1; pageNumber <= pages; pageNumber++)
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment($"Portable PDF page {pageNumber}"));
        }

        document.Save(path);
        return path;
    }

    public string CreateEncryptedDocument(
        string userPassword,
        string ownerPassword,
        string fileName)
    {
        string path = CreateDocument(fileName, pages: 1);
        using var document = new Document(path);
        document.Encrypt(
            userPassword,
            ownerPassword,
            DocumentPrivilege.ForbidAll,
            CryptoAlgorithm.AESx256,
            usePdf20: false);
        document.Save(path);
        return path;
    }

    public string CreateRawDocument(string fileName, int pages,
        IReadOnlySet<int>? textPages = null, IReadOnlySet<int>? imagePages = null, int imageSide = 1,
        int imagePoints = 160, string? textContent = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pages, 1);
        string path = File(fileName);
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
        };
        var kids = new List<int>();
        int fontObject = 3;
        objects.Add(string.Empty);
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        int imageObject = objects.Count + 1;
        if (imagePages is { Count: > 0 })
        {
            // The declared size may exceed the one pixel of data: extraction must refuse
            // an oversized image from its dictionary, before decoding it.
            objects.Add($"<< /Type /XObject /Subtype /Image /Width {imageSide} /Height {imageSide} "
                + "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode "
                + "/Length 7 >>\nstream\n20B090>\nendstream");
        }
        for (int pageNumber = 1; pageNumber <= pages; pageNumber++)
        {
            int pageObject = objects.Count + 1;
            int contentObject = pageObject + 1;
            kids.Add(pageObject);
            bool hasText = textPages is null || textPages.Contains(pageNumber);
            string content = hasText
                ? textContent ?? $"BT /F1 12 Tf 72 720 Td (Portable PDF page {pageNumber}) Tj ET"
                : string.Empty;
            string resources = hasText ? $"/Font << /F1 {fontObject} 0 R >>" : string.Empty;
            if (imagePages?.Contains(pageNumber) == true)
            {
                resources += $" /XObject << /Im1 {imageObject} 0 R >>";
                content += $"\nq {imagePoints} 0 0 {imagePoints} 72 {560 - imagePoints} cm /Im1 Do Q";
            }
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Resources << {resources} >> /Contents {contentObject} 0 R >>");
            objects.Add(
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        objects[1] = $"<< /Type /Pages /Count {pages} /Kids [{string.Join(" ", kids.Select(static value => $"{value} 0 R"))}] >>";
        WriteRawPdf(path, objects);
        return path;
    }

    /// <summary>Writes numbered objects, the first being the catalog, as a PDF with a cross-reference table.</summary>
    public static void WriteRawPdf(string path, IReadOnlyList<string> objects)
    {
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (int index = 0; index < objects.Count; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            pdf.Append($"{offset:0000000000} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        System.IO.File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
    }

    /// <summary>
    /// The darkest red level in <paramref name="bitmap"/>, a render of <paramref name="page"/> at
    /// <paramref name="scale"/> pixels per point, of the band where the underscores of the text
    /// in <paramref name="box"/> are drawn.
    /// </summary>
    public static int DarkestUnderscorePixel(SkiaSharp.SKBitmap bitmap, Page page, Rectangle box, double scale)
    {
        int darkest = 255;
        for (int x = (int)((box.LLX + 1) * scale); x < (int)((box.URX - 1) * scale); x++)
        {
            for (int y = (int)((page.Rect.Height - box.URY) * scale); y < Math.Min(bitmap.Height, (int)((page.Rect.Height - box.LLY + 3) * scale)); y++)
            {
                darkest = Math.Min(darkest, bitmap.GetPixel(x, y).Red);
            }
        }

        return darkest;
    }

    public string CreateCertificate(string password, string fileName = "signing.pfx")
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Aspose CLI PDF Test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(2));
        string path = File(fileName);
        System.IO.File.WriteAllBytes(
            path,
            certificate.Export(X509ContentType.Pfx, password));
        return path;
    }

    public void Dispose() => Temp.Dispose();
}
