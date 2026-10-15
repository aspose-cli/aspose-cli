using System.Globalization;
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

/// <summary>Applies one PKCS#7 signature and verifies the saved field: <c>pdf sign</c>.</summary>
internal static class PdfSign
{
    internal static PdfSignResult Run(PdfSession session, PdfSignRequest request)
    {
        string filePath = request.Input;
        EnsureCertificate(session.Budgets, request.CertificatePath);
        // The certificate is read once, through the SDK, which reports a file it cannot open; the
        // signer gets the same bytes that were validated.
        byte[] certificate = session.Budgets.Inputs.ReadAllBytes(request.CertificatePath);
        string signer = ValidateCertificate(request.CertificatePath, certificate, request.CertificatePassword);
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        Page signedPage = PageAt(loaded.Document, request.Page);
        // The signature to verify is the one this command adds: a document may already
        // carry signed fields, and the first of them says nothing about the new one.
        HashSet<string> alreadySigned = SignedFields(loaded.Document)
            .Select(static field => field.FullName)
            .ToHashSet(StringComparer.Ordinal);

        PdfSignatureRect? visibleRect = request.Visible
            ? request.Rect ?? DefaultRect(signedPage)
            : null;
        if (visibleRect is { Width: <= 0 } or { Height: <= 0 })
        {
            throw CliErrors.OptionInvalid(
                "--rect",
                "signature width and height must be greater than zero",
                "Use x,y,width,height in PDF points from the page's top-left corner, for example 36,36,180,60.");
        }

        DrawingRectangle rectangle = visibleRect is null
            ? new DrawingRectangle(0, 0, 0, 0)
            : EngineRect(signedPage, visibleRect);
        // The facade closes the document when it is disposed, so it outlives the publication,
        // whose pipeline inspects the signed document after the save.
        using var facade = new PdfFileSignature(loaded.Document);
        using OutputSet<Document> transaction = session.Outputs.BeginSet([request.Output.Directory], "pdf-sign");
        // Signing saves the document with the encryption it was opened with.
        bool encrypted = PdfOutputEncryption.Encrypted(loaded.Document, passwords: null);
        // PDF-SIGN-FULL-FONT: the engine embeds the whole font program its appearance draws in.
        // ASCII text is drawn in Helvetica, which is not embedded; PDF/A requires embedded fonts,
        // so its documents keep the engine's choice, and so does text beyond ASCII.
        string[] beyondAscii = [.. new[] { ("--reason", request.Reason), ("--location", request.Location), ("--contact", request.Contact), ("the certificate's subject name", (string?)signer) }
            .Where(static entry => !StandardFontShows(entry.Item2))
            .Select(static entry => entry.Item1)];
        bool pdfa = loaded.Document.IsPdfaCompliant;
        bool standardFont = !pdfa && beyondAscii.Length == 0;
        StagedOutput write = transaction.Stage(request.Output.Path, request.Output.Overwrite, loaded.Document, temp =>
        {
            using var pkcs12 = new MemoryStream(certificate, writable: false);
            var signature = new PKCS7(pkcs12, request.CertificatePassword.Reveal())
            {
                Reason = request.Reason,
                Location = request.Location,
                ContactInfo = request.Contact,
            };
            if (standardFont)
            {
                signature.CustomAppearance = new SignatureCustomAppearance { FontFamilyName = "Helvetica" };
            }

            facade.Sign(request.Page, request.Visible, rectangle, signature);
            facade.Save(temp);
        }, encrypted: encrypted);

        // The staged candidate is the only readable copy before publication: a supervised
        // worker leaves the target to its parent, so reading it here would find nothing.
        PdfSignatureInfo signed = write.Read(
            candidate => VerifySignedOutput(session.Loader, candidate, request.Password, alreadySigned));
        transaction.Commit();
        InputInfo input = PdfInfoProjection.Source(filePath, loaded);
        OutputInfo output = FileObjects.Output(write.Published, request.Output.Format, encrypted);
        return new PdfSignResult
        {
            Input = input,
            Output = output,
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
            Warnings = standardFont ? null : [FontEmbedded(beyondAscii, output.SizeBytes - input.SizeBytes)],
        };
    }

    /// <summary>PDF-SIGN-FULL-FONT: the warning that the appearance embedded a whole font.</summary>
    private static Warning FontEmbedded(string[] beyondAscii, long growth) =>
        new(PdfDiagnostics.SignatureFontEmbedded, string.Create(CultureInfo.InvariantCulture,
            $"The signature appearance draws {(beyondAscii.Length > 0 ? $"text beyond ASCII from {string.Join(", ", beyondAscii)}" : "its text in an embedded font, as PDF/A requires")}, "
            + $"so Aspose.PDF embedded a whole font program: the file grew by {growth} bytes."))
        {
            Hint = beyondAscii.Length > 0
                ? "Sign again with --reason, --location and --contact in ASCII, and a certificate whose subject name is ASCII, to keep the file small."
                : "PDF/A requires embedded fonts, so a signature of this document grows it by a whole font whatever its text.",
        };

    /// <summary>
    /// The default visible rectangle in the bottom-left corner of the visible page: 180 by 60
    /// points, 36 points from its edges, and on a page too small for that, as large as fits with
    /// a margin of an eighth of the page.
    /// </summary>
    private static PdfSignatureRect DefaultRect(Page page)
    {
        Rectangle box = page.GetPageRect(considerRotation: true);
        double margin = Math.Min(36, Math.Min(box.Width, box.Height) / 8);
        double width = Math.Min(180, box.Width - (2 * margin));
        double height = Math.Min(60, box.Height - (2 * margin));
        return new PdfSignatureRect(margin, box.Height - margin - height, width, height);
    }

    /// <summary>
    /// The signing facade's rectangle of a rectangle measured from the top-left corner of the
    /// visible page. The facade places its rectangle in the page's unrotated space from the
    /// lower-left corner, but on a page turned by 90 or 270 degrees it swaps the rectangle's
    /// x with y and width with height first (PDF-SIGN-RECT-ROTATED), so the swap is undone here.
    /// </summary>
    private static DrawingRectangle EngineRect(Page page, PdfSignatureRect rect)
    {
        Rectangle placed;
        try
        {
            placed = ToPdfRect(page, new PdfRectInput { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height });
        }
        catch (OperationInvalidException exception)
        {
            throw CliErrors.OptionInvalid(
                "--rect",
                exception.Message,
                "Use x,y,width,height in PDF points from the page's top-left corner that fit on the page; 'pdf inspect --sample' reports its size.");
        }

        bool swapped = page.Rotate is Rotation.on90 or Rotation.on270;
        return new DrawingRectangle(
            checked((int)Math.Round(swapped ? placed.LLY : placed.LLX)),
            checked((int)Math.Round(swapped ? placed.LLX : placed.LLY)),
            checked((int)Math.Round(swapped ? placed.Height : placed.Width)),
            checked((int)Math.Round(swapped ? placed.Width : placed.Height)));
    }

    private static PdfSignatureInfo VerifySignedOutput(
        PdfDocumentLoader loader, string path, Secret? password, IReadOnlySet<string> alreadySigned)
    {
        using LoadedPdf reopened = loader.OpenPublishedCandidate(path, password);
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

    /// <summary>
    /// Whether the appearance can draw the text in Helvetica without the engine embedding a font:
    /// printable ASCII. With other characters, even Latin-1 ones, it embeds a whole font again.
    /// </summary>
    private static bool StandardFontShows(string? text) =>
        text is null || text.All(static character => character is >= ' ' and <= '~');

    /// <summary>Checks that the certificate opens and can sign now; returns the signer's name, which the appearance shows.</summary>
    private static string ValidateCertificate(string path, byte[] pkcs12, Secret password)
    {
        try
        {
            using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(
                pkcs12,
                password.Reveal(),
                X509KeyStorageFlags.EphemeralKeySet);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!certificate.HasPrivateKey)
            {
                throw SignCertificateInvalid("it holds no private key to sign with");
            }

            if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
            {
                throw SignCertificateInvalid(string.Create(CultureInfo.InvariantCulture,
                    $"it is valid only from {certificate.NotBefore.ToUniversalTime():yyyy-MM-dd} to {certificate.NotAfter.ToUniversalTime():yyyy-MM-dd} (UTC)"));
            }

            return certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        }
        catch (CryptographicException exception) when (exception.HResult == WrongPassword)
        {
            throw CliErrors.PasswordInvalid(Path.GetFullPath(path), passwordEnvironmentOption: "--certificate-password-env");
        }
        catch (CryptographicException exception)
        {
            throw SignCertificateInvalid("it is not a PKCS#12 file the system can read", exception);
        }
    }

    /// <summary>The error Windows reports for a PKCS#12 password that does not open the file (ERROR_INVALID_PASSWORD).</summary>
    private const int WrongPassword = unchecked((int)0x80070056);

    private static CliException SignCertificateInvalid(string reason, Exception? innerException = null) =>
        new(
            PdfDiagnostics.SignCertInvalid,
            $"The PKCS#12 signing certificate could not be used: {reason}.",
            hint: "Use a currently valid PKCS#12 certificate (.pfx or .p12) with a private key.",
            innerException: innerException);
}
