namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>A signature field the signing added, as the saved file holds it.</summary>
public sealed record PdfSignatureInfo
{
    /// <summary>The signature field's full name.</summary>
    public required string Name { get; init; }

    /// <summary>What SignatureField.Signature.Verify() returns on the saved file.</summary>
    public required bool Verify { get; init; }
}
