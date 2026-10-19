using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for page rendering.</summary>
public sealed record PdfRenderRequest
{
    /// <summary>The smallest <see cref="Grid"/> spacing in points.</summary>
    public const int MinimumGrid = 10;

    /// <summary>The largest <see cref="Grid"/> spacing in points.</summary>
    public const int MaximumGrid = 500;

    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    /// <summary>The pages to render, or null for every page.</summary>
    public PageRange? Pages { get; init; }
    public int Dpi { get; init; } = RenderPixelGuard.DefaultDpi;

    /// <summary>
    /// Spacing in points of a coordinate grid drawn on raster output, from <see cref="MinimumGrid"/>
    /// to <see cref="MaximumGrid"/>, or null for none.
    /// </summary>
    public int? Grid { get; init; }
    public Secret? Password { get; init; }
}
