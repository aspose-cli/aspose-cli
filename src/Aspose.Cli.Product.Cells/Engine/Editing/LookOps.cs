using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Workbook and sheet cosmetics: the workbook default (Normal) font, sheet
/// tab colors and the on-screen view (gridlines, zoom, headings). None
/// report a cell count.
/// </summary>
internal static class LookOps
{
    /// <summary>
    /// Changes the workbook default (Normal) font by get-mutate-set on
    /// <see cref="Workbook.DefaultStyle"/> — never <c>CreateStyle</c>, so the
    /// disk-opened-workbook pre-set-style hazard (see
    /// <see cref="StyleWriter.Build"/>) structurally cannot apply here.
    /// Probe-verified: the reassigned default persists through save and
    /// reopen, unstyled cells pick up the new font, cells with an explicitly
    /// set font keep it, and stored column-width unit values are unchanged.
    /// </summary>
    public static long? SetDefaultFont(Workbook workbook, SetDefaultFontOp op)
    {
        Style style = workbook.DefaultStyle;
        style.Font.Name = op.Font;
        if (op.Size is { } size)
        {
            style.Font.DoubleSize = size;
        }

        workbook.DefaultStyle = style;
        return null;
    }

    /// <summary>
    /// Sets or removes a sheet's tab color. <see cref="System.Drawing.Color.Empty"/>
    /// removes the stored tab-color element entirely, and a never-set tab reads
    /// back as <c>Empty</c> too (probe-verified) — a clean tri-state.
    /// </summary>
    public static long? SetTabColor(Worksheet sheet, SetTabColorOp op)
    {
        sheet.TabColor = op.Color is { } color ? StyleWriter.ParseHex(color) : System.Drawing.Color.Empty;
        return null;
    }

    /// <summary>Applies the view fields that are present; the rest stay as they are.</summary>
    public static long? SetSheetView(Worksheet sheet, SetSheetViewOp op)
    {
        if (op.Gridlines is { } gridlines)
        {
            sheet.IsGridlinesVisible = gridlines;
        }

        if (op.Zoom is { } zoom)
        {
            // The setter silently ignores an out-of-range value and keeps the
            // old zoom (probe-verified); the ops parser guards 10-400.
            sheet.Zoom = zoom;
        }

        if (op.Headings is { } headings)
        {
            sheet.IsRowColumnHeadersVisible = headings;
        }

        return null;
    }
}
