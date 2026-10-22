namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// One file <c>pdf extract</c> wrote: an image or table with the page it comes from, an attachment
/// with the name the document gives it, or a file of neither, such as the text or the form data.
/// </summary>
public sealed record PdfExtractOutput : OutputItem
{
    /// <summary>Copies the facts of the written <paramref name="file"/>.</summary>
    public PdfExtractOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>
    /// The 1-based page an extracted image or table comes from; absent for any other file.
    /// </summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>
    /// The attachment's file name as the PDF states it, which <c>delete_attachment</c> accepts;
    /// present only for an attachment that has one. It is an untrusted value the document's
    /// author chose, so the file is written as <c>attachment-0001</c> and on, never under it.
    /// </summary>
    public string? Name { get; init; }
}
