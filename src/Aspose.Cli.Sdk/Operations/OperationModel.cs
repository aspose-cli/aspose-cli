using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// What a product supplies so the SDK can apply a batch and say truthfully what each operation
/// changed: the operation itself, and a snapshot of the objects it can change. The SDK takes
/// the snapshot before and after each operation and derives the outcome's
/// <c>itemsAffected</c> and <c>targets</c>, the pages the batch touched and the
/// <c>OP_NO_EFFECT</c> warning of an operation that changed nothing; the product counts nothing.
/// </summary>
/// <typeparam name="TOp">The product's operation vocabulary.</typeparam>
public interface IOperationModel<in TOp>
    where TOp : BoundedOperation
{
    /// <summary>
    /// The state of every object of the document the operation can change, such as the pages
    /// it selects, with their <c>&lt;unit&gt;/&lt;address&gt;</c> addresses. It changes nothing.
    /// Taken before the operation, it may refuse an operation whose targets do not exist, as
    /// the operation itself would.
    /// </summary>
    OperationSnapshot Snapshot(TOp operation);

    /// <summary>Applies the operation to the document; it reports nothing.</summary>
    void Apply(TOp operation);

    /// <summary>The addresses an operation that failed attempted to change, such as <c>page/2-4</c>.</summary>
    IReadOnlyList<string> AttemptedTargets(TOp operation);

    /// <summary>
    /// Why an operation that applied changed nothing, when the product can say more than that
    /// what it addresses already had the state it sets: for example that its text matched
    /// nothing. Null for the general explanation.
    /// </summary>
    NoEffect? Explain(TOp operation) => null;

    /// <summary>
    /// The objects of each unit the snapshots add with <see cref="OperationSnapshot.AddObjects"/>,
    /// as the instances the engine keeps them as, in their current order. The SDK reads them
    /// before the first operation and after the last, and reports each changed object at its
    /// position in the output, or in the input when the batch removed it. Empty for a product
    /// that adds no such sequence.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() => new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal);

    /// <summary>
    /// The objects whose names begin the addresses of the objects they hold, such as the
    /// worksheets of <c>cell/Sales!B2</c>, by the instances the engine keeps them as. The SDK reads
    /// them before the first operation and after each, so an address an operation reported under a
    /// name a later operation changed is reported under the name the output has, or, for an object
    /// the batch removed, the name the input had. Null for a product whose addresses no renamed
    /// object begins.
    /// </summary>
    NamedScopes? Scopes() => null;
}

/// <summary>
/// The objects whose names begin the addresses of the objects they hold (<see cref="IOperationModel{TOp}.Scopes"/>).
/// An address of one of <paramref name="Units"/> begins with such a name when its part after the
/// unit is the escaped name (<see cref="Addressing.TargetName.Escape"/>), or starts with it and a
/// <c>/</c>, or with the name escaped together with <c>!</c> and a <c>!</c>, as in <c>cell/Sales!B2</c>.
/// </summary>
/// <param name="Units">The units whose addresses begin with a scope's name, such as <c>sheet</c> and <c>cell</c>.</param>
/// <param name="Names">Each scope's instance and its current name.</param>
public sealed record NamedScopes(IReadOnlyCollection<string> Units, IReadOnlyList<(object Identity, string Name)> Names);

/// <summary>One object a batch changed.</summary>
/// <param name="Address">
/// Its address in the output, or, for an object the batch removed, in the input; an object of a
/// sequence the engine does not keep by instance has the address its operation reported.
/// </param>
/// <param name="Identity">The instance the engine keeps the object as, or null for an object of a unit without one.</param>
/// <param name="InOutput">Whether the object is in the output.</param>
public sealed record ChangedObject(string Address, object? Identity, bool InOutput);

/// <summary>Why an operation changed nothing: the <c>details.reason</c>, message and hint of its <c>OP_NO_EFFECT</c> warning.</summary>
/// <param name="Reason">One of <see cref="NoEffectReasons"/>.</param>
/// <param name="Message">What the operation did not find or change; it never repeats a value the caller may keep private, such as find text.</param>
public sealed record NoEffect(string Reason, string Message)
{
    /// <summary>How to make the operation take effect, or null.</summary>
    public string? Hint { get; init; }
}

/// <summary>
/// Records what each applied operation should leave in the output so that, after the batch,
/// the product's read-back of the staged output can check it (<c>edit --verify</c>).
/// </summary>
/// <typeparam name="TOp">The product's operation vocabulary.</typeparam>
public interface IEditVerifier<in TOp>
    where TOp : BoundedOperation
{
    /// <summary>
    /// Records the effect of one operation that applied, against the document it left. An
    /// exception leaves the operation unchecked; it never fails the operation.
    /// </summary>
    void Record(TOp operation, AppliedOperation applied);
}

/// <summary>What the product's read-back of the staged output found.</summary>
/// <param name="Issues">Each expectation the output does not meet.</param>
/// <param name="Checked">The ids of the operations whose every recorded effect was read back.</param>
public sealed record VerificationFacts(IReadOnlyList<VerificationIssue> Issues, IReadOnlyCollection<string> Checked);

/// <summary>The core of an edit's <c>verification</c>, as <see cref="EditVerification"/> reports it.</summary>
/// <param name="Ok">Whether the verification found no issue.</param>
/// <param name="Issues">The problems found.</param>
/// <param name="CheckedOps">In batch order, the ids of the operations whose effect was read back.</param>
public sealed record VerifiedBatch(bool Ok, IReadOnlyList<VerificationIssue> Issues, IReadOnlyList<string> CheckedOps);

/// <summary>A batch the SDK applied through an <see cref="IOperationModel{TOp}"/>.</summary>
public sealed class AppliedBatch
{
    private readonly IReadOnlyList<string> _recorded;
    private readonly IReadOnlySet<string> _unrecorded;

    internal AppliedBatch(
        IReadOnlyList<BoundedOperationOutcome> outcomes,
        IReadOnlyList<Warning> warnings,
        IReadOnlyList<int> pages,
        IReadOnlyList<ChangedObject> changed,
        IReadOnlyList<string> recorded,
        IReadOnlySet<string> unrecorded)
    {
        Outcomes = outcomes;
        Warnings = warnings;
        Pages = pages;
        Changed = changed;
        _recorded = recorded;
        _unrecorded = unrecorded;
    }

    /// <summary>One outcome per operation, in batch order.</summary>
    public IReadOnlyList<BoundedOperationOutcome> Outcomes { get; }

    /// <summary>An <c>OP_NO_EFFECT</c> warning for each operation that applied but changed nothing, in batch order.</summary>
    public IReadOnlyList<Warning> Warnings { get; }

    /// <summary>The numbers of the pages in the output that the batch changed, ascending; empty without a page unit.</summary>
    public IReadOnlyList<int> Pages { get; }

    /// <summary>Every object the batch changed, once, in the order of its addresses.</summary>
    public IReadOnlyList<ChangedObject> Changed { get; }

    /// <summary>
    /// The verification core from the product's read-back of the staged output: it passes
    /// without issues, and an operation counts as checked when the verifier recorded it and the
    /// read-back checked every effect it recorded.
    /// </summary>
    public VerifiedBatch Verified(VerificationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new VerifiedBatch(
            facts.Issues.Count == 0,
            facts.Issues,
            [.. _recorded.Where(id => facts.Checked.Contains(id) && !_unrecorded.Contains(id)).Distinct(StringComparer.Ordinal)]);
    }
}
