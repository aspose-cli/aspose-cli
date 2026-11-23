namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>
/// Result of <c>slides read</c>: a window of the presentation's objects in document order, each
/// with every row of the table it has, and the whole presentation's inventory.
/// </summary>
public sealed record SlidesReadResult : TableReadResult
{
    /// <summary>States what the read found.</summary>
    internal SlidesReadResult(TableReadOutcome found)
        : base("read-result", found)
    {
    }
}
