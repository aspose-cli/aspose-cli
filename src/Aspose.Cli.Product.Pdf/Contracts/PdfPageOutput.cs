namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>One file a command wrote, and the page of the PDF it holds when it holds one.</summary>
public sealed record PdfPageOutput : OutputItem
{
    /// <summary>Copies the facts of the written <paramref name="file"/>.</summary>
    public PdfPageOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>
    /// The 1-based page the file holds or comes from; absent when it holds no single page, as a
    /// whole-document file, an attachment or form data does.
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
