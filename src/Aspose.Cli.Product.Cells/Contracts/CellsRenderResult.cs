using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>cells render</c>: the image of each sheet named, as the engine draws it.</summary>
public sealed record CellsRenderResult() : EngineResultEnvelope("render-result")
{
    /// <summary>The rendered input file.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>
    /// The image of each rendered sheet, in the order --sheet named them: the <c>--out</c> path
    /// itself for one sheet, <c>&lt;stem&gt;.&lt;sheet&gt;.&lt;ext&gt;</c> for each of several.
    /// </summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    public required IReadOnlyList<CellsSheetOutput> Outputs { get; init; }

    /// <summary>The area rendered, as --area gave it; omitted when whole sheets were rendered.</summary>
    public string? Area { get; init; }

    /// <summary>Raster resolution in dots per inch; omitted for vector output.</summary>
    [Minimum(RenderPixelGuard.MinimumDpi), Maximum(RenderPixelGuard.MaximumDpi)]
    public int? Dpi { get; init; }

    /// <summary>The warnings Aspose.Cells reported while it loaded the workbook, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
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
