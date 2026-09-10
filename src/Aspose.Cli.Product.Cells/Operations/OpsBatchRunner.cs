using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Operations;

/// <summary>Applies one op to the engine's workbook; returns the touched cell
/// count where meaningful. The engine supplies this; the runner owns the loop.</summary>
internal delegate long? ApplyOp(Op op);

/// <summary>
/// Runs an ops batch: the loop, index bookkeeping, and the atomic-vs-best-effort
/// contract shared across the Cells command surfaces (edit/write/calc). The
/// engine passes an <see cref="ApplyOp"/> that does the SDK-specific work and
/// launders its own exceptions into <see cref="EngineOpException"/>; this class
/// never touches an engine SDK. Because the workbook is only saved after every
/// op succeeded, batches are atomic by construction.
/// </summary>
internal static class OpsBatchRunner
{
    public static IReadOnlyList<BoundedOperationOutcome> Run(OpsBatch batch, ApplyOp apply, bool continueOnError)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(apply);

        var applied = new List<BoundedOperationOutcome>(batch.Ops.Count);
        for (int index = 0; index < batch.Ops.Count; index++)
        {
            Op op = batch.Ops[index];
            try
            {
                applied.Add(new BoundedOperationOutcome
                {
                    Id = op.Id!,
                    Index = index,
                    Op = op.OpName,
                    Status = OpStatuses.Ok,
                    ItemsAffected = apply(op) ?? 0,
                    Targets = OpsFootprint.OutcomeTargets(op),
                });
            }
            catch (Exception ex) when (ex is CliException or EngineOpException)
            {
                if (!continueOnError)
                {
                    throw Normalize(index, op, ex);
                }

                // Best-effort mode: record the op's own failure and keep going.
                // The per-op record keeps the specific code (e.g. SHEET_NOT_FOUND)
                // rather than the batch-level wrapper atomic mode throws. A
                // cleanly-failing op leaves no trace; a mid-apply failure may
                // leave its partial effects.
                applied.Add(new BoundedOperationOutcome
                {
                    Id = op.Id!,
                    Index = index,
                    Op = op.OpName,
                    Status = OpStatuses.Failed,
                    ItemsAffected = 0,
                    Targets = OpsFootprint.OutcomeTargets(op),
                    Error = ex is CliException cli
                        ? new OpError
                        {
                            Code = cli.Code.Name,
                            Message = cli.Message,
                            Hint = cli.Hint ?? "Fix or remove this operation, then retry the bounded batch.",
                        }
                        : new OpError
                        {
                            Code = ErrorCodes.OpsInvalid.Name,
                            Message = ex.Message,
                            Hint = "Fix or remove this operation, then retry the bounded batch.",
                        },
                });
            }
        }

        return applied;
    }

    /// <summary>
    /// Turns the exception a failing op threw into the canonical, index-bearing
    /// <see cref="CliException"/> — the one atomic mode throws and best-effort
    /// mode records, so both report failures identically.
    /// </summary>
    private static CliException Normalize(int index, Op op, Exception exception) => exception switch
    {
        // A mapper reported a domain problem during apply (e.g. a missing image
        // or pivot); attach the op index the mapper cannot see.
        CliException ex when ex.Code == ErrorCodes.OpsInvalid && ex.Details?["index"] is null =>
            CellsErrors.OpsInvalidAt(
                index, op.OpName, ex.Details?["reason"]?.GetValue<string>() ?? ex.Message, ex.Hint),
        CliException ex when ex.Code == ErrorCodes.OpsInvalid => ex,
        CliException ex => CellsErrors.OpsFailedAt(index, op.OpName, ex),
        EngineOpException ex => CellsErrors.OpsInvalidAt(index, op.OpName, ex.Message),
        _ => throw new InvalidOperationException($"Unexpected exception type: {exception.GetType().Name}"),
    };
}
