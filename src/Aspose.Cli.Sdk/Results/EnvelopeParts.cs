using System.Globalization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// Builders for the envelope fields shared by every result, so license reporting and
/// evaluation warnings stay identical across all commands and products.
/// </summary>
public static class EnvelopeParts
{
    /// <summary>
    /// The evaluation-mode warning attached to every output-producing
    /// operation that ran without a license. The hint deliberately instructs
    /// the agent to inform the user instead of silently delivering
    /// watermarked files.
    /// </summary>
    public static Warning EvaluationWatermark { get; } = new(WarningCodes.EvalMode, "Evaluation mode: the produced file contains an Aspose evaluation watermark and some operations are limited.")
    {
        Hint = "Tell the user about the watermark. A license removes it: set ASPOSE_LICENSE_PATH or pass --license.",
        Docs = "licensing",
    };

    /// <summary>
    /// The evaluation-mode warning of a command that asked for evaluation mode
    /// (<c>--license-mode evaluation</c>): the same disclosure, naming the request as its cause.
    /// </summary>
    public static Warning RequestedEvaluationWatermark { get; } = new(WarningCodes.EvalMode, "Evaluation mode, requested with --license-mode evaluation: the produced file contains an Aspose "
            + "evaluation watermark and some operations are limited.")
    {
        Hint = "No license was read, on request. Tell the user if this file is delivered; drop --license-mode evaluation "
            + "to apply the configured license.",
        Docs = "licensing",
    };

    /// <summary>Maps the license state to the contract representation.</summary>
    public static LicenseInfo License(LicenseState state) => new()
    {
        Mode = state.ToContractName(),
    };

    /// <summary>
    /// Discloses a list inside a result that holds only its first <paramref name="returned"/>
    /// of <paramref name="total"/> entries, so no result is cut short silently.
    /// </summary>
    /// <param name="list">The result field that was capped, e.g. <c>outline</c>.</param>
    /// <param name="returned">How many entries the list holds.</param>
    /// <param name="total">How many entries exist.</param>
    /// <param name="hint">How to read the rest, e.g. a narrower command.</param>
    public static Warning ListTruncated(string list, int returned, int total, string hint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(list);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        return new Warning(WarningCodes.ListTruncated, $"'{list}' lists the first {returned} of {total} entries.")
        {
            Hint = hint,
            Location = list,
        };
    }

    /// <summary>
    /// Discloses a kept backup that holds an earlier version than the file the edit replaced,
    /// so a caller never mistakes it for a copy of the version just overwritten; otherwise none.
    /// </summary>
    public static IReadOnlyList<Warning>? BackupWarnings(BackupInfo? backup) =>
        backup is { HoldsReplacedVersion: false }
            ? [new Warning(WarningCodes.BackupPredatesEdit, $"The existing backup '{backup.Path}' was kept, not replaced: it holds the file as last written at "
                    + $"{backup.LastWriteUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC, "
                    + "an earlier version than the one this edit replaced.")
            {
                Hint = "The kept backup stays the state before the first backed-up edit. To keep the version this edit "
                    + "replaced as well, copy the file before the next in-place edit, or move the old backup aside so "
                    + "--backup creates a new one. Tell the user which version the backup holds.",
                Location = "backup",
            }]
            : null;

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

    /// <summary>Collects the warnings that are present without emitting an empty list.</summary>
    public static IReadOnlyList<Warning>? CombineWarnings(params Warning?[] warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        Warning[] present = [.. warnings.OfType<Warning>()];
        return present.Length == 0 ? null : present;
    }
}
