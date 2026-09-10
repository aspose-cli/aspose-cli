namespace Aspose.Cli.Product.Cells.Addressing;

/// <summary>
/// A rectangular cell range with inclusive bounds. Always normalized:
/// <see cref="Start"/> is the top-left corner, <see cref="End"/> the
/// bottom-right corner.
/// </summary>
public readonly record struct RangeRef
{
    public RangeRef(CellRef start, CellRef end)
    {
        // Normalize so that callers can never construct an inverted range.
        Start = new CellRef(Math.Min(start.Row, end.Row), Math.Min(start.Column, end.Column));
        End = new CellRef(Math.Max(start.Row, end.Row), Math.Max(start.Column, end.Column));
    }

    /// <summary>Top-left corner.</summary>
    public CellRef Start { get; }

    /// <summary>Bottom-right corner (inclusive).</summary>
    public CellRef End { get; }

    /// <summary>Number of rows in the range.</summary>
    public int RowCount => End.Row - Start.Row + 1;

    /// <summary>Number of columns in the range.</summary>
    public int ColumnCount => End.Column - Start.Column + 1;

    /// <summary>Total number of cells covered by the range.</summary>
    public long CellCount => (long)RowCount * ColumnCount;

    /// <summary>Creates a range covering a single cell.</summary>
    public static RangeRef Single(CellRef cell) => new(cell, cell);

    /// <summary>Formats the range in A1 notation, e.g. <c>A1:C10</c> or <c>B2</c>.</summary>
    public override string ToString() => A1.FormatRange(this);
}
