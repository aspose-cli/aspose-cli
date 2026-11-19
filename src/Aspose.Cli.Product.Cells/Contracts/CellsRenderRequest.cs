using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells render</c>.</summary>
public sealed record CellsRenderRequest
{
    /// <summary>The workbook to render.</summary>
    public required string Input { get; init; }

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The sheets to render, by name (<c>--sheet</c>), each to its own image when there are several; null renders every sheet.</summary>
    public IReadOnlyList<string>? Sheets { get; init; }

    /// <summary>The area of the one sheet to render (<c>--area</c>), such as <c>A1:G20</c>, set as its print area; null renders the sheet.</summary>
    public string? Area { get; init; }

    /// <summary>Raster resolution in dots per inch; unused for a vector format.</summary>
    public int Dpi { get; init; } = RenderPixelGuard.DefaultDpi;

    /// <summary>Password for an encrypted input.</summary>
    public Secret? Password { get; init; }
}
