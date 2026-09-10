using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells render</c>: a visual image of one sheet (or a
/// range of it), the primary way for an agent to "look at" a spreadsheet.
/// </summary>
public sealed record RenderResult() : ResultEnvelope(CellsSchemaIds.RenderResult, 2)
{
    /// <summary>The rendered input file.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The produced image file.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>Name of the rendered sheet.</summary>
    public required string Sheet { get; init; }

    /// <summary>A1 range that was rendered; omitted when the whole sheet was rendered.</summary>
    public string? Range { get; init; }

    /// <summary>Raster resolution in dots per inch; omitted for vector output.</summary>
    public int? Dpi { get; init; }

    /// <summary>
    /// Per-sheet output files of an <c>--all-sheets</c> render, in workbook
    /// order. Omitted for a single-sheet render. When present,
    /// <see cref="Output"/> and <see cref="Sheet"/> describe the first
    /// rendered sheet — a stable summary for single-output consumers.
    /// </summary>
    public IReadOnlyList<SheetRenderOutput>? Outputs { get; init; }
}

/// <summary>One rendered sheet of an <c>--all-sheets</c> render.</summary>
public sealed record SheetRenderOutput
{
    /// <summary>Name of the rendered sheet.</summary>
    public required string Sheet { get; init; }

    /// <summary>Absolute path of the produced image file.</summary>
    public required string Path { get; init; }

    /// <summary>Size of the produced image file in bytes.</summary>
    public required long SizeBytes { get; init; }
}
