namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>cells read</c>: a window of the workbook's objects in document order, each with
/// every row of the table it has, and the whole workbook's inventory.
/// </summary>
public sealed record CellsReadResult : TableReadResult
{
    /// <summary>States what the read found.</summary>
    internal CellsReadResult(TableReadOutcome found)
        : base("read-result", found)
    {
    }
}
