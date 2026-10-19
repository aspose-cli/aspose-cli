using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Workbook and sheet cosmetics: default font, tab colors, on-screen view.

/// <summary>
/// Sets the workbook default font. It changes the Normal
/// style every unstyled cell derives its font from and the unit column widths are measured in,
/// so set it first in a new workbook, before content and column widths. Cells with an explicit
/// font keep it.
/// </summary>
[Operation("set_default_font")]
public sealed record SetDefaultFontOp : CellsOp
{
    /// <summary>The font family name, such as Calibri; surrounding spaces are removed.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsDefaultFontInfo/properties/font")]
    [Pattern(@"\S")] public required string Font { get; init; }

    /// <summary>The font size in points; unchanged when omitted.</summary>
    [MistakenFor("size")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsDefaultFontInfo/properties/fontSize")]
    [Measured(MeasureUnit.Points), Minimum(1), Maximum(409)] public double? FontSize { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => this with { Font = Font.Trim() };
}

/// <summary>Sets a sheet's tab color; clear_tab_color removes it.</summary>
[Operation("set_tab_color", Hint = "To remove the tab color, use clear_tab_color.")]
public sealed record SetTabColorOp : CellsSheetOp
{
    /// <summary>The tab color.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsTabColorInfo/properties/color")]
    [HexColor] public required string Color { get; init; }
}

/// <summary>Removes a sheet's tab color.</summary>
[Operation("clear_tab_color")]
public sealed record ClearTabColorOp : CellsSheetOp;

/// <summary>
/// Changes a sheet's on-screen view: at least one of gridlines, zoomPercent and headings. These are
/// view settings for Excel and the live preview, not for the printed page or rendered images;
/// draw borders (set_borders) when a grid must appear in renders.
/// </summary>
[Operation("set_sheet_view")]
[AtLeastOneOf("gridlines", "zoomPercent", "headings")]
public sealed record SetSheetViewOp : CellsSheetOp
{
    /// <summary>Whether the on-screen gridlines show; unchanged when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetViewInfo/properties/gridlines")]
    public bool? Gridlines { get; init; }

    /// <summary>The zoom percentage; unchanged when omitted.</summary>
    [MistakenFor("zoom")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetViewInfo/properties/zoomPercent")]
    [Measured(MeasureUnit.Percent), Minimum(10), Maximum(400)] public int? ZoomPercent { get; init; }

    /// <summary>Whether the row numbers and column letters show; unchanged when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSheetViewInfo/properties/headings")]
    public bool? Headings { get; init; }
}
