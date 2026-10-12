namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>A rectangle in points from the top-left corner of the page.</summary>
public sealed record PdfRect
{
    /// <summary>Distance of the left edge from the page's left edge.</summary>
    public required double X { get; init; }

    /// <summary>Distance of the top edge from the page's top edge.</summary>
    public required double Y { get; init; }

    /// <summary>The width.</summary>
    [Minimum(0)]
    public required double Width { get; init; }

    /// <summary>The height.</summary>
    [Minimum(0)]
    public required double Height { get; init; }
}
