using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// Builders for the envelope fields shared by every result. Engine adapters
/// use these so license reporting and evaluation warnings stay identical
/// across all commands and products.
/// </summary>
public static class EnvelopeParts
{
    /// <summary>
    /// The evaluation-mode warning attached to every output-producing
    /// operation that ran without a license. The hint deliberately instructs
    /// the agent to inform the user instead of silently delivering
    /// watermarked files.
    /// </summary>
    public static Warning EvaluationWatermark { get; } = new()
    {
        Code = WarningCodes.EvalMode,
        Message = "Evaluation mode: the produced file contains an Aspose evaluation watermark and some operations are limited.",
        Hint = "Tell the user about the watermark. A license removes it: set ASPOSE_LICENSE_PATH or pass --license.",
        Docs = "licensing",
    };

    /// <summary>Maps the license state to the contract representation.</summary>
    public static LicenseInfo License(LicenseState state) => new()
    {
        Mode = state.ToContractName(),
    };

    /// <summary>
    /// Warnings for an operation that produced a file: the evaluation warning
    /// in evaluation mode, otherwise none.
    /// </summary>
    public static IReadOnlyList<Warning>? OutputWarnings(LicenseState state) =>
        state == LicenseState.Evaluation ? [EvaluationWatermark] : null;

    /// <summary>Combines optional warning collections without emitting an empty list.</summary>
    public static IReadOnlyList<Warning>? CombineWarnings(
        params IReadOnlyList<Warning>?[] collections)
    {
        ArgumentNullException.ThrowIfNull(collections);
        Warning[] warnings = collections
            .Where(static collection => collection is not null)
            .SelectMany(static collection => collection!)
            .ToArray();
        return warnings.Length == 0 ? null : warnings;
    }
}
