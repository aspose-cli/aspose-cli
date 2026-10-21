namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>One signature field and whether its signature still covers the file unchanged.</summary>
public sealed record PdfSignatureInfo
{
    /// <summary>The signature field's name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the field holds a signature.</summary>
    public required bool Signed { get; init; }

    /// <summary>
    /// Whether the signature verifies against the bytes it signed and those bytes are the whole
    /// file, so nothing was appended after signing; false once any revision follows it, also a
    /// later signature. It says
    /// nothing about who signed: the certificate is not checked against any trust store, so a
    /// self-signed certificate is intact too. Absent when it could not be checked.
    /// </summary>
    public bool? Intact { get; init; }
}
