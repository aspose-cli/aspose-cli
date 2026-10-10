using System.Reflection;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>What one successfully applied operation changed.</summary>
/// <param name="ItemsAffected">Number of product-owned items changed.</param>
/// <param name="Targets">Stable product-owned addresses of the changed items.</param>
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
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(attemptedTargets);

        // The CLI code an operation runs: this SDK and the product that applies it.
        var own = new HashSet<Assembly> { typeof(BoundedOperationRunner).Assembly, apply.Method.Module.Assembly };
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
                    Targets = Bounded(operation, name, applied.Targets, degenerateTargets),
                });
                continue;
            }
            catch (OperationInvalidException invalid)
            {
                rejection = OperationErrors.InvalidAt(index, name, invalid.Message, invalid.Hint ?? catalog.DefaultHint, value: invalid.Mistake);
            }
            catch (CliException failure) when (!failure.IsInvocationFailure)
            {
                rejection = OperationErrors.FailedAt(index, name, failure);
            }
            catch (EngineOpException failure)
            {
                throw OperationErrors.EngineFailedAt(index, name, failure);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // A file the operation declares that does not exist is that file's error, and the
                // operation changed nothing; any other I/O failure the engine raised can occur
                // mid-change. One the CLI raised propagates.
                if (failure is FileNotFoundException { FileName: { Length: > 0 } file }
                    && Declares(catalog.InputPaths(operation), file))
                {
                    rejection = OperationErrors.FailedAt(index, name, CliErrors.FileNotFound(file));
                }
                else if (ExceptionOrigin.IsThirdParty(failure, own))
                {
                    throw OperationErrors.EngineFailedAt(index, name, new EngineOpException(failure.Message, failure));
                }
                else
                {
                    throw;
                }
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
                Targets = Bounded(operation, name, attemptedTargets(operation, index), degenerateTargets),
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
