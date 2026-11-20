using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// <c>pdf sign</c>: PdfFileSignature.Sign(page, visible, rect, new PKCS7(certificate, password) { Reason,
/// Location, ContactInfo, CustomAppearance }) with what the caller states, then PdfFileSignature.Save.
/// The signature fields the saved file holds signed, which the input did not, are read back.
/// </summary>
internal static class PdfSign
{
    internal static PdfSignResult Run(PdfSession session, PdfSignRequest request)
    {
        if (PdfFormats.Certificates.NamedBy(request.CertificatePath).Count == 0)
        {
            throw InputFailures.ExtensionUnsupported(request.CertificatePath, PdfFormats.Certificates);
        }

        // The certificate is an input the command opens with its password: read once, through the SDK.
        byte[] certificate = session.Budgets.Inputs.ReadAllBytes(request.CertificatePath);
        EnsurePassword(request.CertificatePath, certificate, request.CertificatePassword);
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(request.Input, request.Password);
        HashSet<string> alreadySigned = [.. SignedFields(loaded.Document).Select(static field => field.FullName)];
        // The facade closes the document when it is disposed, so it outlives the publication,
        // whose pipeline inspects the signed document after the save.
        using var facade = new PdfFileSignature(loaded.Document);
        using OutputSet<Document> transaction = session.Outputs.BeginSet([request.Output.Directory], "pdf-sign");
        // Signing saves the document with the encryption it was opened with.
        bool encrypted = loaded.Document.IsEncrypted;
        StagedOutput write = transaction.Stage(request.Output.Path, request.Output.Format, request.Output.Overwrite, loaded.Document, temp =>
        {
            using var pkcs12 = new MemoryStream(certificate, writable: false);
            var signature = new PKCS7(pkcs12, request.CertificatePassword.Reveal())
            {
                Reason = request.Reason,
                Location = request.Location,
                ContactInfo = request.Contact,
            };
            if (request.Font is { } font)
            {
                signature.CustomAppearance = new SignatureCustomAppearance { FontFamilyName = font };
            }

            facade.Sign(request.Page, request.Visible, request.Rect ?? default, signature);
            facade.Save(temp);
        }, encrypted: encrypted, saved: true);

        // The staged candidate is the only readable copy before publication.
        IReadOnlyList<PdfSignatureInfo> signed = write.Read(candidate =>
        {
            using LoadedPdf reopened = session.Loader.OpenPublishedCandidate(candidate, request.Password);
            return (IReadOnlyList<PdfSignatureInfo>)[.. SignedFields(reopened.Document)
                .Where(field => !alreadySigned.Contains(field.FullName))
                .Select(static field => new PdfSignatureInfo { Name = field.FullName, Verify = field.Signature!.Verify() })];
        });
        transaction.Commit();
        return new PdfSignResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Protection.Encrypted),
            Output = FileObjects.Output(write.Published, request.Output.Format, encrypted),
            Signatures = signed,
            License = EnvelopeParts.License(state),
        };
    }

    private static IEnumerable<SignatureField> SignedFields(Document document) =>
        PdfTable.Fields(document).OfType<SignatureField>().Where(static field => field.Signature is not null);

    /// <summary>Opens the PKCS #12 file with its password, so a password that does not open it is reported as the input's.</summary>
    private static void EnsurePassword(string path, byte[] pkcs12, Secret password)
    {
        try
        {
            using X509Certificate2 opened = X509CertificateLoader.LoadPkcs12(pkcs12, password.Reveal(), X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException exception) when (exception.HResult == WrongPassword)
        {
            throw CliErrors.PasswordInvalid(Path.GetFullPath(path));
        }
    }

    /// <summary>The error Windows reports for a PKCS#12 password that does not open the file, the Win32 error 86 (0x56).</summary>
    private const int WrongPassword = unchecked((int)0x80070056);
}
