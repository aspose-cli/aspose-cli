using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Sparklines: tiny in-cell charts summarizing a row or column of data.

/// <summary>
/// Adds a sparkline group over a data block: one sparkline per data row (or column), each drawn
/// in one cell of location.
/// </summary>
[Operation("add_sparkline")]
public sealed record AddSparklineOp : Op
{
    /// <summary>
    /// The data to plot, such as B2:E10. It may name another sheet, such as Data!B2:E10, so
    /// sparklines on a dashboard sheet can plot a data sheet.
    /// </summary>
    [A1Reference] public required string DataRange { get; init; }

    /// <summary>
    /// A single cell or a one-row or one-column strip, such as F2 or F2:F10. One sparkline per
    /// data row (or column) lands in each location cell, so the cell count must match the data's
    /// row count (one per row) or column count (one per column).
    /// </summary>
    [A1Range] public required string Location { get; init; }

    /// <summary>winloss draws equal-height columns above or below the axis by sign.</summary>
    [AllowedValues(typeof(SparklineTypes))] public string Type { get; init; } = SparklineTypes.Line;

    /// <summary>The series color; the engine default when omitted.</summary>
    [HexColor] public string? Color { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        RangeRef location = A1.ParseRange(Location).Range;
        OperationInvalidException.Require(location.RowCount == 1 || location.ColumnCount == 1,
            "'location' must be a single cell or a one-row/one-column range, e.g. \"F2\" or \"F2:F10\"");
        return this;
    }
}

/// <summary>Accepted values of <see cref="AddSparklineOp.Type"/>.</summary>
public static class SparklineTypes
{
    public const string Line = "line";
    public const string Column = "column";
    public const string WinLoss = "winloss";
}
