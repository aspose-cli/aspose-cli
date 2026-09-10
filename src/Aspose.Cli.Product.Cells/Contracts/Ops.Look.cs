namespace Aspose.Cli.Product.Cells.Contracts;

// Workbook and sheet cosmetics: default font, tab colors, on-screen view.

/// <summary>
/// Sets the workbook default font. Workbook-scoped: the op's <c>sheet</c> is
/// ignored. This changes the Normal style that every unstyled cell derives
/// its font from AND the unit that column widths are measured in — call it
/// FIRST in a new workbook, before content and column widths, so later
/// widths are chosen against the font that will actually render. Cells with
/// an explicitly set font keep it.
/// </summary>
public sealed record SetDefaultFontOp() : Op(OpNames.SetDefaultFont)
{
    /// <summary>Font family name, e.g. <c>Calibri</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Font size in points (1–409); unchanged when omitted.</summary>
    public double? Size { get; init; }
}

/// <summary>Sets or removes a sheet's tab color.</summary>
public sealed record SetTabColorOp() : Op(OpNames.SetTabColor)
{
    /// <summary>Tab color as <c>#RRGGBB</c>; omit to remove the tab color.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Adjusts a sheet's on-screen view: gridline visibility, zoom and row/column
/// headings. Only the fields present are applied. These are view settings —
/// they affect Excel and the live preview, not the printed page or the PNG
/// <c>render</c> output (draw borders when a grid must appear in renders).
/// </summary>
public sealed record SetSheetViewOp() : Op(OpNames.SetSheetView)
{
    /// <summary>Show the on-screen gridlines; unchanged when omitted.</summary>
    public bool? Gridlines { get; init; }

    /// <summary>Zoom percentage (10–400); unchanged when omitted.</summary>
    public int? Zoom { get; init; }

    /// <summary>Show the row numbers and column letters; unchanged when omitted.</summary>
    public bool? Headings { get; init; }
}
