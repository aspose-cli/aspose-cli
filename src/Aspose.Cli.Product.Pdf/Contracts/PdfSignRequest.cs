using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for applying one PKCS#7 PDF signature.</summary>
public sealed record PdfSignRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required string CertificatePath { get; init; }
    public required Secret CertificatePassword { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public Secret? Password { get; init; }
    public int Page { get; init; } = 1;
    public bool Visible { get; init; }
    public PdfSignatureRect? Rect { get; init; }
    public string? Reason { get; init; }
    public string? Location { get; init; }
    public string? Contact { get; init; }
}

/// <summary>
/// A visible-signature rectangle in PDF points from the top-left corner of the visible, rotated
/// page box, as <c>redact_area</c> and <c>pdf render --grid</c> measure.
/// </summary>
public sealed record PdfSignatureRect(double X, double Y, double Width, double Height);
