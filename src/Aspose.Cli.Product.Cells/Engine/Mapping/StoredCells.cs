using Aspose.Cells;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Collects only stored cells, admitting work and sort storage before allocation.</summary>
internal static class StoredCells
{
    internal static IReadOnlyList<Cell> InAddressOrder(
        ResourceBudgetLedger budgets, Worksheet sheet, string phase)
    {
        budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, 128, "bytes", phase);
        var cells = new List<Cell>();
        foreach (Cell cell in sheet.Cells)
        {
            budgets.Consume(CellsBudgetDomains.Cells, 1, "items", phase);
            // Reserve both live buffers during List growth before adding a reference.
            budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, 3L * IntPtr.Size, "bytes", phase);
            cells.Add(cell);
        }
        budgets.Deadline.ThrowIfExpired(phase);
        cells.Sort(static (left, right) => left.Row == right.Row
            ? left.Column.CompareTo(right.Column) : left.Row.CompareTo(right.Row));
        budgets.Deadline.ThrowIfExpired(phase);
        return cells;
    }
}
