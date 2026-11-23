namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>cells edit</c>: what each operation left, read from the saved workbook after it
/// was opened again, and the inventory counts that changed.
/// </summary>
public sealed record CellsEditResult : TableEditResult
{
    /// <summary>States what the edit found.</summary>
    internal CellsEditResult(TableEditOutcome found)
        : base("edit-result", found)
    {
    }
}
