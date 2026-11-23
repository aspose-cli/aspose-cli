namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// Result of <c>words read</c>: a window of the document's objects in document order, each with
/// every row of the table it has, and the whole document's inventory.
/// </summary>
public sealed record WordsReadResult : TableReadResult
{
    /// <summary>States what the read found.</summary>
    internal WordsReadResult(TableReadOutcome found)
        : base("read-result", found)
    {
    }
}
