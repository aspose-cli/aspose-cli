using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>What one successfully applied operation changed.</summary>
/// <param name="ItemsAffected">Number of product-owned items changed.</param>
/// <param name="Targets">Stable product-owned addresses of the changed items.</param>
public readonly record struct AppliedOperation(long ItemsAffected, IReadOnlyList<string> Targets);

/// <summary>
/// Applies a validated batch in order and reports one outcome per operation.
/// </summary>
/// <remarks>
/// Handlers resolve and check their targets before they change the document, so an
/// <see cref="OperationInvalidException"/> or a domain <see cref="CliException"/> means the
/// operation changed nothing: an atomic batch stops, and a best-effort batch records the
/// failure and continues. An <see cref="EngineOpException"/> can occur mid-change, so it
/// stops the batch in both modes and nothing is published.
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
    public static IReadOnlyList<BoundedOperationOutcome> Run<TOp>(
        OperationCatalog<TOp> catalog,
        IReadOnlyList<TOp> operations,
        bool bestEffort,
        OperationDeadline? deadline,
        Func<TOp, int, AppliedOperation> apply,
        Func<TOp, int, IReadOnlyList<string>> attemptedTargets)
        where TOp : BoundedOperation
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(attemptedTargets);

        var outcomes = new List<BoundedOperationOutcome>(operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            TOp operation = operations[index];
            string name = catalog.NameOf(operation);
            deadline?.ThrowIfExpired("operations");
            CliException rejection;
            try
            {
                AppliedOperation applied = apply(operation, index);
                outcomes.Add(new BoundedOperationOutcome
                {
                    Id = operation.Id!,
                    Index = index,
                    Op = name,
                    Status = OpStatuses.Ok,
                    ItemsAffected = applied.ItemsAffected,
                    Targets = applied.Targets,
                });
                continue;
            }
            catch (OperationInvalidException invalid)
            {
                rejection = OperationErrors.InvalidAt(index, name, invalid.Message, invalid.Hint ?? catalog.DefaultHint);
            }
            catch (CliException failure) when (!failure.IsInvocationFailure)
            {
                rejection = OperationErrors.FailedAt(index, name, failure);
            }
            catch (EngineOpException failure)
            {
                throw OperationErrors.EngineFailedAt(index, name, failure);
            }

            if (!bestEffort)
            {
                throw rejection;
            }
            outcomes.Add(new BoundedOperationOutcome
            {
                Id = operation.Id!,
                Index = index,
                Op = name,
                Status = OpStatuses.Failed,
                ItemsAffected = 0,
                Targets = attemptedTargets(operation, index),
                Error = new OpError
                {
                    Code = rejection.Code.Name,
                    Message = rejection.Message,
                    Hint = rejection.Hint ?? BestEffortHint,
                    Details = rejection.Details,
                },
            });
        }

        return outcomes;
    }
}
