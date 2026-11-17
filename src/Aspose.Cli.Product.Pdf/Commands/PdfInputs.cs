namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>The input documents PDF commands declare.</summary>
internal static class PdfInputs
{
    /// <summary>The PDF every reading command opens.</summary>
    public static readonly InputDocument Document = new("PDF document to open.", "the PDF");

    /// <summary>The password a writing command can put on the PDF it writes.</summary>
    public static readonly EncryptedOutput EncryptedPdf = new("the output PDF");
}
