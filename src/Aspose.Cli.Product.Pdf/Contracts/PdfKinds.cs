namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>The kinds of the PDF table that read lists only when --kind names them.</summary>
internal static class PdfKinds
{
    internal static IReadOnlyList<string> ListedWhenNamed { get; } = ["page", "text", "annotation", "font", "layer", "bookmark", "field"];
}
