namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Views rendered by the PDF product.</summary>
public static class PdfViews
{
    /// <summary>The pages of the document, one image per page.</summary>
    public const string Pages = "pages";

    /// <summary>The id of the part of <see cref="Pages"/> that shows a 1-based page.</summary>
    internal static string PagePart(int page) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"page-{page}");
}
