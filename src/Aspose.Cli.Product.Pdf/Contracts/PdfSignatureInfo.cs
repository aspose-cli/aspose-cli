namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>One signature field and the verification state the engine reports.</summary>
public sealed record PdfSignatureInfo
{
    /// <summary>The signature field's name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the field holds a signature.</summary>
    public required bool Signed { get; init; }

    /// <summary>Whether the signature verifies against the signed bytes; absent when it was not verified.</summary>
    public bool? Valid { get; init; }
}
