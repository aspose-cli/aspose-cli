using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
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
    public ILicenseGate Gate { get; } = TestLicense.Apply(PdfActivation.ApplyLicense);

    /// <summary>A new session, as one invocation's activation creates it.</summary>
    internal PdfSession Session =>
        ProductTestBudgets.StartEngine<PdfModule, PdfSession>(
            (budgets, writer) => PdfActivation.Session(Outputs(writer), budgets));

    /// <summary>The write pipeline of one invocation, as the product binding creates it.</summary>
    internal OutputPipeline Outputs(SafeFileWriter writer) => new(Gate, new PdfEvaluationProfile(), writer);

    /// <summary>A loader over <paramref name="budgets"/> and a licensed write pipeline that publishes nothing.</summary>
    internal static Engine.Mapping.PdfDocumentLoader Loader(Aspose.Cli.Sdk.IO.Budgets.ResourceBudgetLedger budgets) =>
        new(budgets, new OutputPipeline(EvaluationLimits.Gate(LicenseState.Licensed), new PdfEvaluationProfile(), new SafeFileWriter(budgets)));

    public LicenseState LicenseState => Gate.EnsureApplied();

    public TempDirectory Temp { get; } = new();

    public string File(string fileName) => Temp.File(fileName);

    /// <summary>
    /// Skips a test that calls the engine in this process without a license, before the test opens
    /// a document: evaluation mode gives the first four objects of a collection only.
    /// </summary>
    public static void RequireLicense() => TestLicense.Require(EvaluationChanges);

    private const string EvaluationChanges =
        "Aspose.PDF evaluation mode gives only the first four objects of any collection and stamps every page it saves, which the in-process engine suite compares against.";

    /// <summary>A request of <c>pdf edit</c> over a parsed operation document.</summary>
    internal static PdfEditRequest Edit(string input, string output, string ops, Aspose.Cli.Sdk.Contracts.Secret? password = null, Aspose.Cli.Sdk.Contracts.Secret? encrypt = null) => new()
    {
        Input = input,
        Batch = Aspose.Cli.Sdk.Tables.TableBatch.Parse(ops, PdfTable.Definition, new Inputs()),
        Output = new EditOutput(output),
        Password = password,
        EncryptPassword = encrypt,
    };

    /// <summary>The files and secrets of a batch, as the command line reads them.</summary>
    internal sealed class Inputs : Aspose.Cli.Sdk.Tables.ITableInputs
    {
        public string ResolvePath(string path) => Path.GetFullPath(path);

        public byte[] ReadImage(string path) => System.IO.File.ReadAllBytes(path);


        public byte[] ReadFile(string path) => System.IO.File.ReadAllBytes(path);

        public Aspose.Cli.Sdk.Contracts.Secret? ReadSecret(string variable) =>
            Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? new Aspose.Cli.Sdk.Contracts.Secret(value) : null;
    }

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

    /// <summary>
    /// A PDF with one object of every kind of the table: six pages (the third rotated), a nested
    /// outline, text, check box, radio group, combo and list fields, a signature field, a field
    /// with two widgets, links of four actions, text, highlight, free text and file attachment
    /// annotations, two attachments of one name, page labels, custom document information, XMP
    /// entries and a layer.
    /// </summary>
    public string CreateRich(string fileName = "rich.pdf")
    {
        string path = File(fileName);
        using var d = new Document();
        for (int i = 1; i <= 6; i++)
        {
            d.Pages.Add().Paragraphs.Add(new TextFragment($"Page {i} marker PAGE-{i}"));
        }

        d.Pages[3].Rotate = Rotation.on90;
        var a = new OutlineItemCollection(d.Outlines) { Title = "A", Destination = new Aspose.Pdf.Annotations.GoToAction(d.Pages[1]) };
        var a1 = new OutlineItemCollection(d.Outlines) { Title = "A.1", Destination = new Aspose.Pdf.Annotations.GoToAction(d.Pages[2]) };
        a1.Add(new OutlineItemCollection(d.Outlines) { Title = "A.1.a", Destination = new Aspose.Pdf.Annotations.GoToAction(d.Pages[3]) });
        a.Add(a1);
        d.Outlines.Add(a);
        d.Outlines.Add(new OutlineItemCollection(d.Outlines) { Title = "B", Destination = new Aspose.Pdf.Annotations.GoToAction(d.Pages[4]) });
        Page first = d.Pages[1];
        d.Form.Add(new Aspose.Pdf.Forms.TextBoxField(first, new Rectangle(50, 700, 250, 720)) { PartialName = "name", Value = "Ann" }, 1);
        d.Form.Add(new Aspose.Pdf.Forms.CheckboxField(first, new Rectangle(50, 660, 70, 680)) { PartialName = "ok", Checked = true }, 1);
        var radio = new Aspose.Pdf.Forms.RadioButtonField(first) { PartialName = "color" };
        radio.AddOption("red", new Rectangle(50, 620, 65, 635));
        radio.AddOption("green", new Rectangle(80, 620, 95, 635));
        d.Form.Add(radio, 1);
        var combo = new Aspose.Pdf.Forms.ComboBoxField(first, new Rectangle(50, 580, 200, 600)) { PartialName = "size" };
        combo.AddOption("S");
        combo.AddOption("M");
        d.Form.Add(combo, 1);
        var list = new Aspose.Pdf.Forms.ListBoxField(first, new Rectangle(50, 500, 200, 570)) { PartialName = "fruit" };
        list.AddOption("apple");
        list.AddOption("pear");
        d.Form.Add(list, 1);
        d.Form.Add(new Aspose.Pdf.Forms.SignatureField(d.Pages[2], new Rectangle(50, 100, 250, 150)) { PartialName = "sig1" }, 2);
        var total = new Aspose.Pdf.Forms.TextBoxField(first, new Rectangle(300, 700, 450, 720)) { PartialName = "total", Value = "42" };
        d.Form.Add(total, 1);
        d.Form.AddFieldAppearance(total, 2, new Rectangle(300, 700, 450, 720));
        Page second = d.Pages[2];
        second.Annotations.Add(new Aspose.Pdf.Annotations.LinkAnnotation(second, new Rectangle(50, 400, 200, 420)) { Action = new Aspose.Pdf.Annotations.GoToURIAction("https://example.com/") });
        second.Annotations.Add(new Aspose.Pdf.Annotations.LinkAnnotation(second, new Rectangle(50, 370, 200, 390)) { Action = new Aspose.Pdf.Annotations.GoToAction(d.Pages[5]) });
        second.Annotations.Add(new Aspose.Pdf.Annotations.LinkAnnotation(second, new Rectangle(50, 340, 200, 360)) { Action = new Aspose.Pdf.Annotations.GoToRemoteAction("other.pdf", 2) });
        second.Annotations.Add(new Aspose.Pdf.Annotations.TextAnnotation(second, new Rectangle(300, 400, 320, 420)) { Title = "Rev", Contents = "note text", Subject = "s" });
        second.Annotations.Add(new Aspose.Pdf.Annotations.HighlightAnnotation(second, new Rectangle(300, 300, 400, 320)) { Contents = "hl" });
        second.Annotations.Add(new Aspose.Pdf.Annotations.FileAttachmentAnnotation(second, new Rectangle(300, 200, 320, 220),
            new FileSpecification(new MemoryStream([1, 2, 3]), "annot.bin", "in annot")));
        d.EmbeddedFiles.Add(new FileSpecification(new MemoryStream("AB"u8.ToArray()), "a.txt", "first a"));
        d.EmbeddedFiles.Add(new FileSpecification(new MemoryStream("C"u8.ToArray()), "b.txt", "b"));
        d.EmbeddedFiles.Add(new FileSpecification(new MemoryStream("D"u8.ToArray()), "a.txt", "second a"));
        d.PageLabels.UpdateLabel(0, new PageLabel { NumberingStyle = NumberingStyle.NumeralsRomanLowercase });
        d.PageLabels.UpdateLabel(2, new PageLabel { NumberingStyle = NumberingStyle.NumeralsArabic, Prefix = "A-", StartingValue = 1 });
        d.Info.Title = "Rich";
        d.Info.Author = "Probe";
        d.Info["Custom"] = "cv";
        d.Metadata["xmp:CreatorTool"] = new XmpValue("Probe tool");
        d.Pages[4].Layers = [new Layer("oc1", "Layer one")];
        d.Save(path);
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

    /// <summary>
    /// Writes numbered objects, the first being the catalog, as a PDF with a cross-reference table;
    /// <paramref name="info"/> numbers the document information dictionary, if any.
    /// </summary>
    public static void WriteRawPdf(string path, IReadOnlyList<string> objects, int? info = null)
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

        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{(info is int number ? $" /Info {number} 0 R" : string.Empty)} >>\nstartxref\n{xref}\n%%EOF\n");
        System.IO.File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
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
