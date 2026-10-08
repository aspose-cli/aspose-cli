namespace Aspose.Cli.Product.Pdf.Ports;

/// <summary>Product-internal text layout facts used by the PDF review gate.</summary>
internal interface IPdfReviewLayoutPort
{
    PdfReviewLayout InspectReviewLayout(
        string filePath,
        Secret? password,
        int maxPages);
}
