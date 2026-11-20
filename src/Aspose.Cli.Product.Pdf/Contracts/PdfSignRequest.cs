using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>One <c>pdf sign</c>: the parameters of PdfFileSignature.Sign, as the caller states them.</summary>
public sealed record PdfSignRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }

    /// <summary>The PKCS #12 file of the signing certificate.</summary>
    public required string CertificatePath { get; init; }

    /// <summary>The password the PKCS #12 file opens with.</summary>
    public required Secret CertificatePassword { get; init; }

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The password the PDF opens with.</summary>
    public Secret? Password { get; init; }

    /// <summary>The page of the signature, from 1.</summary>
    public required int Page { get; init; }

    /// <summary>Whether the signature has an appearance on the page.</summary>
    public required bool Visible { get; init; }

    /// <summary>The signature's rectangle, the engine's System.Drawing.Rectangle; null for an invisible one.</summary>
    public System.Drawing.Rectangle? Rect { get; init; }

    /// <summary>PKCS7.Reason.</summary>
    public string? Reason { get; init; }

    /// <summary>PKCS7.Location.</summary>
    public string? Location { get; init; }

    /// <summary>PKCS7.ContactInfo.</summary>
    public string? Contact { get; init; }

    /// <summary>SignatureCustomAppearance.FontFamilyName, or null for the engine's default appearance.</summary>
    public string? Font { get; init; }
}
