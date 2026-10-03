using System.Globalization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Views rendered by the Words product.</summary>
public static class WordsViews
{
    /// <summary>The fixed-layout pages of the document, one image per page.</summary>
    public const string Pages = "pages";

    /// <summary>The id of the <see cref="Pages"/> view part that shows a 1-based page.</summary>
    internal static string PagePart(int page) =>
        string.Create(CultureInfo.InvariantCulture, $"page-{page}");
}
