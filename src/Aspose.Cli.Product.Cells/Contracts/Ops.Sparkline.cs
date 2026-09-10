namespace Aspose.Cli.Product.Cells.Contracts;

// Sparklines: tiny in-cell charts summarizing a row or column of data.

/// <summary>
/// Adds a sparkline group over a data block: one sparkline per data row (or
/// column), each drawn in one cell of <see cref="Location"/>.
/// </summary>
public sealed record AddSparklineOp() : Op(OpNames.AddSparkline)
{
    /// <summary>
    /// The data to plot, e.g. <c>B2:E10</c>. May be sheet-qualified
    /// (<c>Data!B2:E10</c>) so sparklines on a dashboard sheet can plot a data
    /// sheet; otherwise the op's sheet.
    /// </summary>
    public required string DataRange { get; init; }

    /// <summary>
    /// Where the sparklines are drawn, on the op's sheet: a single cell or a
    /// one-row/one-column strip, e.g. <c>F2</c> or <c>F2:F10</c>. One
    /// sparkline per data row (or column) lands in each location cell.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>Sparkline type; one of <see cref="SparklineTypes"/>, <c>line</c> when omitted.</summary>
    public string? Type { get; init; }

    /// <summary>Series color as <c>#RRGGBB</c>; the engine default when omitted.</summary>
    public string? Color { get; init; }
}

/// <summary>Accepted values of <see cref="AddSparklineOp.Type"/>.</summary>
public static class SparklineTypes
{
    /// <summary>A line through the values.</summary>
    public const string Line = "line";

    /// <summary>One column per value.</summary>
    public const string Column = "column";

    /// <summary>
    /// Win/loss columns (Excel's name for it): equal-height columns above or
    /// below the axis by sign.
    /// </summary>
    public const string WinLoss = "winloss";

    /// <summary>Every sparkline type, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Line, Column, WinLoss];
}
