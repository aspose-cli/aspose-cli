using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells render</c>: visual images of sheets (or a range of one), the
/// primary way for an agent to "look at" a spreadsheet.
/// </summary>
public sealed record CellsRenderResult() : EngineResultEnvelope("render-result", 2)
{
    /// <summary>The rendered input file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>
    /// The image of each rendered sheet, in workbook order: one file for one sheet, the
    /// <c>--out</c> path itself, or <c>&lt;stem&gt;.&lt;sheet&gt;.&lt;ext&gt;</c> per sheet.
    /// </summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<CellsSheetOutput> Outputs { get; init; }

    /// <summary>A1 range that was rendered; omitted when whole sheets were rendered.</summary>
    public string? Range { get; init; }

    /// <summary>Raster resolution in dots per inch; omitted for vector output.</summary>
    [Minimum(RenderPixelGuard.MinimumDpi), Maximum(RenderPixelGuard.MaximumDpi)]
    public int? Dpi { get; init; }
}

/// <summary>The image of one rendered sheet.</summary>
public sealed record CellsSheetOutput : OutputItem
{
    /// <summary>Describes the image <paramref name="file"/> of one sheet.</summary>
    /// <param name="file">The published image, as <c>FileObjects.Output</c> describes it.</param>
    public CellsSheetOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>Name of the rendered sheet.</summary>
    [JsonPropertyOrder(-1)]
    public required string Sheet { get; init; }
}
