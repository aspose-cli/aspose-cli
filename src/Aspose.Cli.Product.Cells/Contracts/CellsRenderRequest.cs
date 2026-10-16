using Aspose.Cli.Sdk.Addressing;
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

    /// <summary>
    /// The sheets to render by 1-based position, each to its own file derived from
    /// <see cref="Output"/> (<c>&lt;stem&gt;.&lt;sheet&gt;&lt;ext&gt;</c>) when there are several;
    /// null or <c>all</c> renders every visible sheet. With <see cref="Range"/> it selects one sheet.
    /// </summary>
    public PageRange? Sheets { get; init; }

    /// <summary>
    /// The <c>--sheets</c> value as given when it may name a sheet: with no <see cref="Sheets"/>
    /// it names one, such as <c>Summary</c>, and is refused once the workbook is open, with each
    /// sheet and its position; a selection past the last sheet that a sheet has as its name,
    /// such as <c>2024</c>, is refused the same way.
    /// </summary>
    public string? SheetsText { get; init; }

    /// <summary>The sheet <see cref="Range"/> names; the selected or the active sheet when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>Restrict rendering to this range of one sheet; whole sheets when null.</summary>
    public RangeRef? Range { get; init; }

    /// <summary>Raster resolution in dots per inch; unused for a vector format.</summary>
    public int Dpi { get; init; } = RenderPixelGuard.DefaultDpi;

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }
}
