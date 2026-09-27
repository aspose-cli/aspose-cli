using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Sparklines — tiny in-cell charts. A data block fans out to one sparkline per
/// row (or column) in one group, each landing in one cell of the location
/// strip. Takes the workbook because the group's series colour is
/// a <c>CellsColor</c> only the workbook can create (as the CF ops do).
/// </summary>
internal static class SparklineOps
{
    public static long? AddSparkline(Workbook workbook, Worksheet sheet, AddSparklineOp op)
    {
        (Worksheet dataSheet, RangeRef data) = Sheets.ResolveRange(sheet, op.DataRange);
        RangeRef location = A1.ParseRange(op.Location).Range;
        long locationCells = location.CellCount;

        // The orientation is not exposed on the wire; it is inferred
        // from the shapes: N location cells serving N data
        // rows means one sparkline per row (isVertical false); M cells serving
        // M data columns means one per column (true). When both match (a
        // square data block) per-row wins — the overwhelmingly common reading.
        // Neither matching would be a silent partial draw, so it is rejected
        // with both counts spelled out.
        bool isVertical;
        if (locationCells == data.RowCount)
        {
            isVertical = false;
        }
        else if (locationCells == data.ColumnCount)
        {
            isVertical = true;
        }
        else
        {
            // The batch runner attaches the op index.
            throw new OperationInvalidException(
                $"the location has {locationCells} cells but the data range has {data.RowCount} rows "
                + $"and {data.ColumnCount} columns; make them match one of the two",
                hint: "Give one location cell per data row (one sparkline per row) or one per data column.");
        }

        // The engine's one-call SparklineGroups.Add(type, range, isVertical, area)
        // throws Invalid "'" when the data sheet or this sheet has an apostrophe
        // in its name, however the name is quoted, so the group is built from its
        // parts: an empty group with the settings the one-call Add applies (an
        // empty group has no colours, and reading its PresetStyle throws), then
        // one sparkline per data row or column, each range written as start:end.
        // The contract keeps the location a one-row or one-column strip, so
        // sparkline i lands in its i-th cell. Tests pin the result to the
        // one-call Add's.
        SparklineType type = ToType(op.Type);
        SparklineGroup group = sheet.SparklineGroups[sheet.SparklineGroups.Add(type)];
        group.PresetStyle = SparklinePresetStyleType.Style1;
        group.ShowNegativePoints = type == SparklineType.WinLoss;
        bool locationIsRow = location.RowCount == 1;
        for (int index = 0; index < locationCells; index++)
        {
            (CellRef first, CellRef last) = isVertical
                ? (new CellRef(data.Start.Row, data.Start.Column + index), new CellRef(data.End.Row, data.Start.Column + index))
                : (new CellRef(data.Start.Row + index, data.Start.Column), new CellRef(data.Start.Row + index, data.End.Column));
            group.Sparklines.Add(
                Sheets.QuotedName(dataSheet) + "!" + A1.FormatCell(first) + ":" + A1.FormatCell(last),
                location.Start.Row + (locationIsRow ? 0 : index),
                location.Start.Column + (locationIsRow ? index : 0));
        }

        if (op.Color is { } color)
        {
            CellsColor seriesColor = workbook.CreateCellsColor();
            seriesColor.Color = StyleWriter.ParseHex(color);
            group.SeriesColor = seriesColor;
        }

        return locationCells;
    }

    /// <summary>
    /// Wire name to the engine's <see cref="SparklineType"/>. Excel calls the
    /// third type "win/loss"; the engine exposes it as <c>WinLoss</c>.
    /// </summary>
    private static SparklineType ToType(string type) => type switch
    {
        SparklineTypes.Line => SparklineType.Line,
        SparklineTypes.Column => SparklineType.Column,
        SparklineTypes.WinLoss => SparklineType.WinLoss,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Sparkline type is missing from the engine mapper."),
    };
}
