using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells render</c>.</summary>
public sealed record CellsRenderRequest
{
    /// <summary>The workbook to render.</summary>
    public required string Input { get; init; }

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>Sheet to render; the active sheet when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>Restrict rendering to this range; the whole sheet when null.</summary>
    public RangeRef? Range { get; init; }

    /// <summary>
    /// Render every visible sheet, each to its own file derived from
    /// <see cref="Output"/> (<c>&lt;base&gt;.&lt;Sheet&gt;&lt;ext&gt;</c>).
    /// Mutually exclusive with <see cref="SheetName"/> and <see cref="Range"/>;
    /// the command layer enforces that before the request is built.
    /// </summary>
    public bool AllSheets { get; init; }

    /// <summary>Raster resolution in dots per inch (ignored for vector formats).</summary>
    public int Dpi { get; init; } = RenderPixelGuard.DefaultDpi;

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }
}
