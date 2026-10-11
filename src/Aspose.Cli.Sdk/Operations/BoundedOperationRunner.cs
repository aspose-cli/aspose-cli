using System.Reflection;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>What one successfully applied operation changed.</summary>
/// <param name="ItemsAffected">Number of product-owned items changed.</param>
/// <param name="Targets">Stable product-owned addresses of the changed items.</param>
/// <remarks>
/// The SDK builds it from the snapshots an <see cref="IOperationModel{TOp}"/> takes. Until every
/// product applies its batches through one, a product that still counts for itself constructs it
/// too; once the last one has moved, the constructor becomes internal to the SDK.
/// </remarks>
public readonly record struct AppliedOperation(long ItemsAffected, IReadOnlyList<string> Targets);

/// <summary>
/// Applies a validated batch in order and reports one outcome per operation. An outcome lists
/// at most <see cref="BoundedOperationOutcome.MaximumTargets"/> targets: one that changed more parts lists the product's
/// degenerate form instead, never a prefix of the parts.
/// </summary>
/// <remarks>
/// Handlers resolve and check their targets before they change the document, so an
/// <see cref="OperationInvalidException"/> or a domain <see cref="CliException"/> means the
/// operation changed nothing: an atomic batch stops, and a best-effort batch records the
/// failure and continues, as does a missing file the operation declares. An
/// <see cref="EngineOpException"/>, or another I/O exception the engine raised, can occur
/// mid-change, so it stops the batch in both modes and nothing is published.
/// </remarks>
public static class BoundedOperationRunner
{
    private const string BestEffortHint = "Fix or remove this operation, then retry the batch.";

    /// <summary>Runs every operation of a validated batch.</summary>
    /// <param name="catalog">The vocabulary the batch was validated against.</param>
    /// <param name="operations">Validated operations with assigned ids.</param>
    /// <param name="bestEffort">Whether a rejected operation is recorded instead of stopping the batch.</param>
    /// <param name="deadline">Invocation deadline checked before every operation.</param>
    /// <param name="apply">Applies one operation; receives its zero-based index.</param>
    /// <param name="attemptedTargets">Addresses reported for an operation that failed; receives its index.</param>
    /// <param name="degenerateTargets">
    /// The product's degenerate form of an operation's targets when it lists more than
    /// <see cref="BoundedOperationOutcome.MaximumTargets"/>: the document root address, such as <c>document</c>, or range
    /// addresses that cover every changed part, never a category address with a meaning of its
    /// own; receives the operation and its full list. A product whose operations never list that
    /// many omits it.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// An operation lists more than <see cref="BoundedOperationOutcome.MaximumTargets"/> targets without a degenerate form,
    /// or the degenerate form is empty or as long.
    /// </exception>
    public static IReadOnlyList<BoundedOperationOutcome> Run<TOp>(
        OperationCatalog<TOp> catalog,
        IReadOnlyList<TOp> operations,
        bool bestEffort,
        OperationDeadline? deadline,
        Func<TOp, int, AppliedOperation> apply,
        Func<TOp, int, IReadOnlyList<string>> attemptedTargets,
        Func<TOp, IReadOnlyList<string>, IReadOnlyList<string>>? degenerateTargets = null)
        where TOp : BoundedOperation
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(attemptedTargets);
        return Execute(
            catalog, operations, bestEffort, deadline, apply.Method.Module.Assembly, apply,
            (operation, index, name) => Bounded(operation, name, attemptedTargets(operation, index), degenerateTargets),
            (operation, name, applied) => Bounded(operation, name, applied.Targets, degenerateTargets),
            applied: null);
    }

    /// <summary>
    /// Runs every operation of a validated batch through the product's object model: the SDK
    /// snapshots the objects each operation can change before and after it, and an operation's
    /// <c>itemsAffected</c> is the number of objects that changed and its <c>targets</c> their
    /// addresses, where they are in the output, or in the input for removed ones. More than
    /// <see cref="BoundedOperationOutcome.MaximumTargets"/> addresses shorten to one per unit
    /// that covers them all, such as <c>page/1-150</c>. An operation that applied but changed
    /// nothing warns <c>OP_NO_EFFECT</c>.
    /// When the snapshot after an operation cannot be read, the operation has applied but no
    /// outcome can say what it changed: the batch stops as after an engine failure.
    /// </summary>
    /// <param name="catalog">The vocabulary the batch was validated against.</param>
    /// <param name="operations">Validated operations with assigned ids.</param>
    /// <param name="bestEffort">Whether a rejected operation is recorded instead of stopping the batch.</param>
    /// <param name="deadline">Invocation deadline checked before every operation.</param>
    /// <param name="model">Snapshots and applies the operations.</param>
    /// <param name="verifier">With <c>--verify</c>, records the effect of each operation that applied.</param>
    /// <param name="pageUnit">
    /// The unit whose numbered addresses, such as <c>page/3</c>, are the pages of the output;
    /// the batch reports the ones it changed in <see cref="AppliedBatch.Pages"/>.
    /// </param>
    public static AppliedBatch Run<TOp>(
        OperationCatalog<TOp> catalog,
        IReadOnlyList<TOp> operations,
        bool bestEffort,
        OperationDeadline? deadline,
        IOperationModel<TOp> model,
        IEditVerifier<TOp>? verifier = null,
        string? pageUnit = null)
        where TOp : BoundedOperation
    {
        ArgumentNullException.ThrowIfNull(model);
        var warnings = new List<Warning>();
        var pages = new SortedSet<int>();
        var recorded = new List<string>();
        var unrecorded = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<BoundedOperationOutcome> outcomes = Execute(
            catalog,
            operations,
            bestEffort,
            deadline,
            model.GetType().Assembly,
            (operation, _) =>
            {
                OperationSnapshot before = model.Snapshot(operation);
                model.Apply(operation);
                OperationSnapshot after;
                try
                {
                    after = model.Snapshot(operation);
                }
                catch (Exception failure) when (failure is EngineOpException or CliException { IsInvocationFailure: false })
                {
                    // The operation changed the document, but what it changed cannot be read, so
                    // no outcome can say it truthfully, and reporting it failed would deny a change
                    // the output holds: the batch stops and nothing is written, as after an engine
                    // failure, in both modes. Any other exception is a defect and propagates.
                    throw new EngineOpException(
                        $"it applied, but reading what it changed failed: {failure.Message}",
                        failure is EngineOpException { InnerException: { } inner } ? inner : failure);
                }

                IReadOnlyList<ObjectChange> changes = OperationSnapshot.Compare(before, after);
                if (pageUnit is not null)
                {
                    pages.UnionWith(changes
                        .Where(change => change.InOutput && change.Address.StartsWith(pageUnit + "/", StringComparison.Ordinal))
                        .Select(static change => OperationSnapshot.FirstNumber(change.Address))
                        .OfType<int>());
                }

                return new AppliedOperation(changes.Count, [.. changes.Select(static change => change.Address).Distinct(StringComparer.Ordinal)]);
            },
            (operation, _, _) => Covered(model.AttemptedTargets(operation)),
            (_, _, applied) => Covered(applied.Targets),
            (operation, id, name, applied) =>
            {
                if (applied.ItemsAffected == 0)
                {
                    NoEffect effect = model.Explain(operation) ?? new NoEffect(
                        NoEffectReasons.NoChange,
                        $"Operation {id} ({name}) changed nothing: what it addresses already had the state it sets.");
                    warnings.Add(catalog.NoEffect(operation, effect.Reason, effect.Message) with { Hint = effect.Hint });
                }

                if (verifier is not null)
                {
                    recorded.Add(id);
                    try
                    {
                        verifier.Record(operation, applied);
                    }
                    catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
                    {
                        // Recording reads the document; what it cannot read stays unchecked.
                        unrecorded.Add(id);
                    }
                }
            });
        return new AppliedBatch(outcomes, warnings, [.. pages], recorded, unrecorded);
    }

    private static IReadOnlyList<string> Covered(IReadOnlyList<string> targets) =>
        targets.Count <= BoundedOperationOutcome.MaximumTargets ? targets : OperationSnapshot.Cover(targets);

    private static IReadOnlyList<BoundedOperationOutcome> Execute<TOp>(
        OperationCatalog<TOp> catalog,
        IReadOnlyList<TOp> operations,
        bool bestEffort,
        OperationDeadline? deadline,
        Assembly product,
        Func<TOp, int, AppliedOperation> apply,
        Func<TOp, int, string, IReadOnlyList<string>> attemptedTargets,
        Func<TOp, string, AppliedOperation, IReadOnlyList<string>> appliedTargets,
        Action<TOp, string, string, AppliedOperation>? applied)
        where TOp : BoundedOperation
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(operations);

        // The CLI code an operation runs: this SDK and the product that applies it.
        var own = new HashSet<Assembly> { typeof(BoundedOperationRunner).Assembly, product };
        var outcomes = new List<BoundedOperationOutcome>(operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            TOp operation = operations[index];
            string name = catalog.NameOf(operation);
            string id = operation.Id
                ?? throw new ArgumentException("Every operation of a validated batch has an id.", nameof(operations));
            deadline?.ThrowIfExpired("operations");
            CliException? rejection = null;
            AppliedOperation done = default;
            try
            {
                done = apply(operation, index);
            }
            catch (OperationInvalidException invalid)
            {
                rejection = OperationErrors.InvalidAt(id, name, invalid.Message, invalid.Hint ?? catalog.DefaultHint, value: invalid.Mistake);
            }
            catch (CliException failure) when (!failure.IsInvocationFailure)
            {
                rejection = OperationErrors.FailedAt(id, name, failure);
            }
            catch (EngineOpException failure)
            {
                throw OperationErrors.EngineFailedAt(id, name, failure);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // A file the operation declares that does not exist is that file's error, and the
                // operation changed nothing; any other I/O failure the engine raised can occur
                // mid-change. One the CLI raised propagates.
                if (failure is FileNotFoundException { FileName: { Length: > 0 } file }
                    && Declares(catalog.InputPaths(operation), file))
                {
                    rejection = OperationErrors.FailedAt(id, name, CliErrors.FileNotFound(file));
                }
                else if (ExceptionOrigin.IsThirdParty(failure, own))
                {
                    throw OperationErrors.EngineFailedAt(id, name, new EngineOpException(failure.Message, failure));
                }
                else
                {
                    throw;
                }
            }

            if (rejection is null)
            {
                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = id,
                    Op = name,
                    Status = OpStatuses.Ok,
                    ItemsAffected = done.ItemsAffected,
                    Targets = appliedTargets(operation, name, done),
                });
                applied?.Invoke(operation, id, name, done);
                continue;
            }

            if (!bestEffort)
            {
                throw rejection;
            }
            outcomes.Add(new BoundedOperationOutcome
            {
                Id = id,
                Op = name,
                Status = OpStatuses.Failed,
                ItemsAffected = 0,
                Targets = attemptedTargets(operation, index, name),
                Error = rejection.ToEnvelope().Error with { Hint = rejection.Hint ?? BestEffortHint },
            });
        }

        return outcomes;
    }

    private static IReadOnlyList<string> Bounded<TOp>(
        TOp operation,
        string name,
        IReadOnlyList<string> targets,
        Func<TOp, IReadOnlyList<string>, IReadOnlyList<string>>? degenerateTargets)
    {
        if (targets.Count <= BoundedOperationOutcome.MaximumTargets)
        {
            return targets;
        }

        IReadOnlyList<string> degenerate = degenerateTargets?.Invoke(operation, targets)
            ?? throw new InvalidOperationException(
                $"Operation '{name}' lists {targets.Count} targets, more than {BoundedOperationOutcome.MaximumTargets}, and its product declares no degenerate form.");
        return degenerate.Count is > 0 and <= BoundedOperationOutcome.MaximumTargets
            ? degenerate
            : throw new InvalidOperationException(
                $"The degenerate form of operation '{name}' lists {degenerate.Count} targets; it must list 1 to {BoundedOperationOutcome.MaximumTargets}.");
    }

    private static bool Declares(IReadOnlyList<string> inputs, string file)
    {
        string? missing = PathResolver.TryResolve(Environment.CurrentDirectory, file);
        return missing is not null && inputs.Any(input =>
            string.Equals(PathResolver.TryResolve(Environment.CurrentDirectory, input), missing, StringComparison.OrdinalIgnoreCase));
    }
}
