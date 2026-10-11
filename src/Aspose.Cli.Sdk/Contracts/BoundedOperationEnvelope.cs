namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Product-neutral fields of one ordered, bounded mutation document. The
/// operation payload and its addresses remain owned by the product.
/// </summary>
public record BoundedOperationEnvelope<TOperation>
{
    /// <summary>Optional canonical schema identifier for the owning product.</summary>
    public string? Schema { get; init; }

    /// <summary>Current envelope version; omitted input defaults to version 2.</summary>
    public int? SchemaVersion { get; init; } = 2;

    /// <summary>Optional SHA-256 precondition copied from an input fingerprint.</summary>
    public string? IfMatch { get; init; }

    /// <summary>
    /// Product-owned operations, executed in this order. A document may also be the bare array
    /// of operations, which reads as a document with only this field.
    /// </summary>
    public required IReadOnlyList<TOperation> Ops { get; init; }
}


/// <summary>Product-neutral fields shared by every bounded operation.</summary>
public abstract record BoundedOperation
{
    /// <summary>
    /// The most operations one document holds, the same in every product; a product limits
    /// what a batch may consume through its resource budgets, not through the operation count.
    /// </summary>
    public const int MaximumPerBatch = 10_000;

    /// <summary>Stable correlation ID; assigned deterministically when omitted.</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Applies the operation's rules that its declared constraints cannot express, after those
    /// constraints hold, and returns the operation to run, which may be normalized. The
    /// operation's summary states each rule, so the published schema tells agents.
    /// </summary>
    /// <exception cref="Operations.OperationInvalidException">A rule is broken.</exception>
    protected internal virtual BoundedOperation Validated() => this;
}
