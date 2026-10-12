namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for a budgeted page-text read.</summary>
public sealed record PdfReadRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public PageRange? Pages { get; init; }
    public string Content { get; init; } = PdfReadContents.Text;
    public int MaxCharacters { get; init; } = 20_000;
    public Secret? Password { get; init; }
}

/// <summary>What <c>pdf read --content</c> returns of each page.</summary>
public static class PdfReadContents
{
    /// <summary>The page's text in reading order.</summary>
    public const string Text = "text";

    /// <summary>The page's text with the spacing of its layout kept.</summary>
    public const string Layout = "layout";

    /// <summary>Every value, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Text, Layout];
}
