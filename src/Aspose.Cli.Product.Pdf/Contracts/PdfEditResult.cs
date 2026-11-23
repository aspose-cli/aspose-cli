namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// Result of <c>pdf edit</c>: what each operation left, read from the saved document after it
/// was opened again, and the inventory counts that changed.
/// </summary>
public sealed record PdfEditResult : TableEditResult
{
    /// <summary>States what the edit found.</summary>
    internal PdfEditResult(TableEditOutcome found)
        : base("edit-result", found)
    {
    }
}
