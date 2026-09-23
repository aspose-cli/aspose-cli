using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Sparklines — tiny in-cell charts. One engine <c>Add</c> call fans a data
/// block out to one sparkline per row (or column), each landing in one cell of
/// the location strip. Takes the workbook because the group's series colour is
/// a <c>CellsColor</c> only the workbook can create (as the CF ops do).
/// </summary>
internal static class SparklineOps
{
    public static long? AddSparkline(Workbook workbook, Worksheet sheet, AddSparklineOp op)
    {
        (Worksheet dataSheet, RangeRef data) = Sheets.ResolveRange(sheet, op.DataRange);
        RangeRef location = A1.ParseRange(op.Location).Range;
        long locationCells = location.CellCount;

        // The engine's isVertical is not exposed on the wire; it is inferred
        // from the shapes (probe-verified): N location cells serving N data
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

        // The engine wants the data reference qualified; an unqualified range
        // means the op's sheet (as create_pivot's sourceRange).
        CellArea area = CellArea.CreateCellArea(
            location.Start.Row, location.Start.Column, location.End.Row, location.End.Column);
        int groupIndex = sheet.SparklineGroups.Add(
            ToType(op.Type), Sheets.Reference(dataSheet, data), isVertical, area);

        if (op.Color is { } color)
        {
            SparklineGroup group = sheet.SparklineGroups[groupIndex];
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
    private static SparklineType ToType(string? type) => type switch
    {
        null or SparklineTypes.Line => SparklineType.Line,
        SparklineTypes.Column => SparklineType.Column,
        SparklineTypes.WinLoss => SparklineType.WinLoss,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Sparkline type is missing from the engine mapper."),
    };
}
