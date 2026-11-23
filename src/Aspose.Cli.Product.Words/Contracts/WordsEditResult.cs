namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// Result of <c>words edit</c>: what each operation left, read from the saved document after it
/// was opened again, and the inventory counts that changed.
/// </summary>
public sealed record WordsEditResult : TableEditResult
{
    /// <summary>States what the edit found.</summary>
    internal WordsEditResult(TableEditOutcome found)
        : base("edit-result", found)
    {
    }
}
