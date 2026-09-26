namespace Aspose.Cli.Product.Cells.Contracts.Addressing;

/// <summary>A single cell position, zero-based.</summary>
/// <param name="Row">Zero-based row index (A1 row 1 is index 0).</param>
/// <param name="Column">Zero-based column index (column A is index 0).</param>
public readonly record struct CellRef(int Row, int Column)
{
    /// <summary>Formats the cell in A1 notation, e.g. <c>B3</c>.</summary>
    public override string ToString() => A1.FormatCell(this);
}
