namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// Result of <c>pdf read</c>: a window of the document's objects in document order, each with
/// every row of the table it has, and the whole document's inventory.
/// </summary>
public sealed record PdfReadResult : TableReadResult
{
    /// <summary>States what the read found.</summary>
    internal PdfReadResult(TableReadOutcome found)
        : base("read-result", found)
    {
    }
}
