using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Workbook and sheet cosmetics: default font, tab colors, on-screen view.

/// <summary>
/// Sets the workbook default font; the operation's sheet is ignored. It changes the Normal
/// style every unstyled cell derives its font from and the unit column widths are measured in,
/// so set it first in a new workbook, before content and column widths. Cells with an explicit
/// font keep it.
/// </summary>
[Operation("set_default_font")]
public sealed record SetDefaultFontOp : CellsOp
{
    /// <summary>The font family name, such as Calibri; surrounding spaces are removed.</summary>
    [Pattern(@"\S")] public required string Font { get; init; }

    /// <summary>The font size in points; unchanged when omitted.</summary>
    [Minimum(1), Maximum(409)] public double? Size { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => this with { Font = Font.Trim() };
}

/// <summary>Sets a sheet's tab color, or removes it when color is omitted.</summary>
[Operation("set_tab_color")]
public sealed record SetTabColorOp : CellsOp
{
    /// <summary>The tab color; omitted removes it.</summary>
    [HexColor] public string? Color { get; init; }
}

/// <summary>
/// Changes a sheet's on-screen view: at least one of gridlines, zoom and headings. These are
/// view settings for Excel and the live preview, not for the printed page or rendered images;
/// draw borders (set_borders) when a grid must appear in renders.
/// </summary>
[Operation("set_sheet_view")]
[AtLeastOneOf("gridlines", "zoom", "headings")]
public sealed record SetSheetViewOp : CellsOp
{
    /// <summary>Whether the on-screen gridlines show; unchanged when omitted.</summary>
    public bool? Gridlines { get; init; }

    /// <summary>The zoom percentage; unchanged when omitted.</summary>
    [Minimum(10), Maximum(400)] public int? Zoom { get; init; }

    /// <summary>Whether the row numbers and column letters show; unchanged when omitted.</summary>
    public bool? Headings { get; init; }
}
