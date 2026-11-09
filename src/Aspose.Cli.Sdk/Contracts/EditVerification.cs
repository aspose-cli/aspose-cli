namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The core every product's <c>edit --verify</c> reports: whether the staged output passed, the
/// issues found, and the operations checked and not checked. A product derives one record that
/// declares only its own evidence, which its result schema then lists beside the core; the record
/// may be inherited across assemblies.
/// </summary>
public abstract record EditVerification
{
    /// <summary>Whether the verification found no issue.</summary>
    public required bool Ok { get; init; }

    /// <summary>The problems found, empty when <c>ok</c> is true.</summary>
    public required IReadOnlyList<VerificationIssue> Issues { get; init; }

    /// <summary>
    /// In batch order, the ids of the operations whose effect the verification read back from the
    /// staged output.
    /// </summary>
    public required IReadOnlyList<string> CheckedOps { get; init; }

    /// <summary>
    /// In batch order, each operation of the batch the verification did not read back from the
    /// staged output, with the reason; omitted when it read back every operation.
    /// </summary>
    [MinItems(1)]
    public IReadOnlyList<UncheckedOperation>? UncheckedOps { get; init; }
}

/// <summary>An operation of the batch that an edit's verification did not read back.</summary>
[SchemaId("unchecked-operation")]
public sealed record UncheckedOperation
{
    /// <summary>The operation's id in the batch.</summary>
    [MinLength(1)]
    public required string Id { get; init; }

    /// <summary>Why the verification did not read the operation back.</summary>
    [MinLength(1)]
    public required string Reason { get; init; }
}
