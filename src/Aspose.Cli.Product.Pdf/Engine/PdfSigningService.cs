using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Aspose.Cli.Product.Pdf.Engine;

// Signing implementation.
internal sealed class PdfSigningService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfSigningService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    public PdfSignResult Sign(string filePath, PdfSignRequest request)
    {
        EnsureCertificate(_resourceBudgets, request.CertificatePath);
        ValidateCertificate(request.CertificatePath, request.CertificatePassword.Reveal());
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password?.Reveal());
        _ = PageAt(loaded.Document, request.Page);
        // The signature to verify is the one this command adds: a document may already
        // carry signed fields, and the first of them says nothing about the new one.
        HashSet<string> alreadySigned = SignedFields(loaded.Document)
            .Select(static field => field.FullName)
            .ToHashSet(StringComparer.Ordinal);

        PdfSignatureRect? visibleRect = request.Visible
            ? request.Rect ?? new PdfSignatureRect(36, 36, 180, 60)
            : null;
        if (visibleRect is { Width: <= 0 } or { Height: <= 0 })
        {
            throw CliErrors.OptionInvalid(
                "--rect",
                "signature width and height must be greater than zero",
                "Use x,y,width,height in PDF points, for example 36,36,180,60.");
        }

        DrawingRectangle rectangle = visibleRect is null
            ? new DrawingRectangle(0, 0, 0, 0)
            : new DrawingRectangle(
                checked((int)Math.Round(visibleRect.X)),
                checked((int)Math.Round(visibleRect.Y)),
                checked((int)Math.Round(visibleRect.Width)),
                checked((int)Math.Round(visibleRect.Height)));
        using var transaction = new AtomicOutputSetWriter(
            _writer, request.Output.Directory, "pdf-sign");
        StagedOutput write = transaction.Stage(request.Output.Path, request.Output.Overwrite, temp =>
        {
            using var facade = new PdfFileSignature(loaded.Document);
            var signature = new PKCS7(request.CertificatePath, request.CertificatePassword.Reveal())
            {
                Reason = request.Reason,
                Location = request.Location,
                ContactInfo = request.Contact,
            };
            facade.Sign(request.Page, request.Visible, rectangle, signature);
            facade.Save(temp);
        });

        // The staged candidate is the only readable copy before publication: a supervised
        // worker leaves the target to its parent, so reading it here would find nothing.
        PdfSignatureInfo signed = write.Read(
            candidate => VerifySignedOutput(candidate, request.Password?.Reveal(), alreadySigned));
        transaction.Commit();
        return new PdfSignResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Output = new OutputInfo
            {
                Path = Path.GetFullPath(request.Output.Path),
                Format = "pdf",
                SizeBytes = write.SizeBytes,
            },
            Signature = signed,
            Visible = request.Visible,
            Page = request.Page,
            Rect = visibleRect is null
                ? null
                : new PdfRect
                {
                    X = visibleRect.X,
                    Y = visibleRect.Y,
                    Width = visibleRect.Width,
                    Height = visibleRect.Height,
                },
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.OutputWarnings(state),
        };
    }

    private PdfSignatureInfo VerifySignedOutput(string path, string? password, IReadOnlySet<string> alreadySigned)
    {
        using LoadedPdf reopened = _loader.OpenPublishedCandidate(path, password);
        SignatureField? field = SignedFields(reopened.Document)
            .FirstOrDefault(value => !alreadySigned.Contains(value.FullName));
        if (field?.Signature is null)
        {
            const string message = "The saved PDF did not contain the new signed signature field.";
            throw new EngineOpException(message, new InvalidOperationException(message));
        }

        bool? valid;
        try
        {
            valid = field.Signature.Verify();
        }
        catch (Exception exception) when (exception.GetType().Assembly.GetName().Name == "Aspose.PDF")
        {
            valid = null;
        }

        return new PdfSignatureInfo
        {
            Name = field.FullName ?? field.PartialName ?? string.Empty,
            Signed = true,
            Valid = valid,
        };
    }

    private static IEnumerable<SignatureField> SignedFields(Document document) =>
        document.Form.Fields.OfType<SignatureField>().Where(static field => field.Signature is not null);

    private static void EnsureCertificate(
        ResourceBudgetLedger resourceBudgets,
        string path)
    {
        string extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".pfx", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".p12", StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.FormatUnsupported(extension.TrimStart('.'), ["pfx", "p12"]);
        }

        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }
        InputSizeGuard.Ensure(resourceBudgets, path);
    }

    private static void ValidateCertificate(string path, string password)
    {
        try
        {
            using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(
                path,
                password,
                X509KeyStorageFlags.EphemeralKeySet);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!certificate.HasPrivateKey
                || now < certificate.NotBefore.ToUniversalTime()
                || now > certificate.NotAfter.ToUniversalTime())
            {
                throw SignCertificateInvalid();
            }
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CryptographicException
            or IOException
            or UnauthorizedAccessException)
        {
            throw SignCertificateInvalid(exception);
        }
    }

    private static CliException SignCertificateInvalid(Exception? innerException = null) =>
        new(
            PdfDiagnostics.SignCertInvalid,
            "The PKCS#12 signing certificate could not be used.",
            hint: "Verify the certificate file and password, then use a currently valid certificate with a private key.",
            innerException: innerException);
}
