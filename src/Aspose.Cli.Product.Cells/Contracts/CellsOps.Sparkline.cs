using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Sparklines: tiny in-cell charts summarizing a row or column of data.

/// <summary>
/// Adds a sparkline group over a data block: one sparkline per data row (or column), each drawn
/// in one cell of at. A group a read reports as lists, such as at D5,D7,D8 over dataRange
/// B5:C5,B7:C7,B8:C8, is written back with the same lists.
/// </summary>
[Operation("add_sparkline")]
public sealed record AddSparklineOp : CellsSheetOp
{
    /// <summary>
    /// The data to plot, such as B2:E10: one sparkline per row (or column) of it. It may name
    /// another sheet, such as Data!B2:E10, so sparklines on a dashboard sheet can plot a data
    /// sheet. A list separated by commas, such as B5:C5,B7:C7,B8:C8, gives each sparkline its own
    /// row or column of data, in the order of the cells of at.
    /// </summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSparklineInfo/properties/dataRange")]
    [A1ReferenceList] public required string DataRange { get; init; }

    /// <summary>
    /// The cells the sparklines are drawn in, one each: a single cell or a one-row or one-column
    /// strip, such as F2 or F2:F10, or a list of them separated by commas, such as D5,D7,D8. Over
    /// one data block, the cell count must match its row count (one per row) or column count
    /// (one per column); over a list of data ranges, its range count.
    /// </summary>
    [MistakenFor("location")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsSparklineInfo/properties/at")]
    [A1RangeList] public required string At { get; init; }

    /// <summary>winloss draws equal-height columns above or below the axis by sign.</summary>
    [MistakenFor("type")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsSparklineInfo/properties/kind")]
    [AllowedValues(typeof(CellsSparklineTypes))] public string Kind { get; init; } = CellsSparklineTypes.Line;

    /// <summary>The series color; the engine default when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsSparklineInfo/properties/color")]
    [HexColor] public string? Color { get; init; }

    /// <summary>
    /// Each sparkline of the group in order: the data it plots, sheet-qualified as written, and
    /// the cell it is drawn in.
    /// </summary>
    /// <exception cref="OperationInvalidException">The data and the cells do not pair up.</exception>
    internal IReadOnlyList<(RangeSpec Data, CellRef Cell)> Sparklines()
    {
        var cells = new List<CellRef>();
        foreach (string item in A1.SplitList(At))
        {
            RangeRef strip = A1.ParseRange(item).Range;
            OperationInvalidException.Require(strip.RowCount == 1 || strip.ColumnCount == 1,
                $"each part of 'at' must be a single cell or a one-row/one-column range, e.g. \"F2\" or \"F2:F10\"; '{item.Trim()}' is neither");
            for (int row = strip.Start.Row; row <= strip.End.Row; row++)
            {
                for (int column = strip.Start.Column; column <= strip.End.Column; column++)
                {
                    cells.Add(new CellRef(row, column));
                }
            }
        }

        IReadOnlyList<string> parts = A1.SplitList(DataRange);
        if (parts.Count > 1)
        {
            RangeSpec[] lines = [.. parts.Select(A1.ParseRange)];
            foreach ((RangeSpec line, string part) in lines.Zip(parts))
            {
                OperationInvalidException.Require(line.Range.RowCount == 1 || line.Range.ColumnCount == 1,
                    $"each range of a 'dataRange' list is the data of one sparkline, a single row or column; '{part.Trim()}' is neither");
            }

            OperationInvalidException.Require(lines.Length == cells.Count,
                $"'dataRange' lists {lines.Length} ranges but 'at' has {cells.Count} cells; give one cell of 'at' per range of 'dataRange', in the same order");
            return [.. lines.Zip(cells)];
        }

        // One block: one sparkline per row when the cells match its rows, else one per column.
        // When both match (a square block) per row wins, the common reading; neither would be a
        // silent partial draw.
        RangeSpec block = A1.ParseRange(parts[0]);
        RangeRef data = block.Range;
        if (cells.Count == data.RowCount)
        {
            return [.. cells.Select((cell, index) => (block with
            {
                Range = new RangeRef(new CellRef(data.Start.Row + index, data.Start.Column), new CellRef(data.Start.Row + index, data.End.Column)),
            }, cell))];
        }

        if (cells.Count == data.ColumnCount)
        {
            return [.. cells.Select((cell, index) => (block with
            {
                Range = new RangeRef(new CellRef(data.Start.Row, data.Start.Column + index), new CellRef(data.End.Row, data.Start.Column + index)),
            }, cell))];
        }

        throw new OperationInvalidException(
            $"'at' has {cells.Count} cells but 'dataRange' has {data.RowCount} rows and {data.ColumnCount} columns; make them match one of the two",
            hint: "Give one cell of 'at' per data row (one sparkline per row) or one per data column.");
    }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        _ = Sparklines();
        return this;
    }
}

/// <summary>Accepted values of <see cref="AddSparklineOp.Kind"/>.</summary>
public static class CellsSparklineTypes
{
    public const string Line = "line";
    public const string Column = "column";
    public const string WinLoss = "winloss";
}
