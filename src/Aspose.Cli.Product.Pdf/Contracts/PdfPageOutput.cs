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
    /// whole-document file does.
    /// </summary>
    [Minimum(1)]
    public int? Page { get; init; }
}
