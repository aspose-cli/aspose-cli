using System.Text.Json.Nodes;
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
    /// operation that ran without a license.
    /// </summary>
    public static Warning EvaluationWatermark { get; } = new(WarningCodes.EvaluationMode, "Evaluation mode: the produced file contains an Aspose evaluation watermark and some operations are limited.");

    /// <summary>
    /// The evaluation-mode warning of a command that asked for evaluation mode
    /// (<c>--license-mode evaluation</c>): the same disclosure, naming the request as its cause.
    /// </summary>
    public static Warning RequestedEvaluationWatermark { get; } = new(WarningCodes.EvaluationMode, "Evaluation mode, requested with --license-mode evaluation: the produced file contains an Aspose "
            + "evaluation watermark and some operations are limited.");

    /// <summary>
    /// The license block of a result: the mode when the engine ran without a license, and none
    /// when it ran licensed, since a licensed result omits it.
    /// </summary>
    public static LicenseInfo? License(LicenseState state) => state == LicenseState.Licensed
        ? null
        : new LicenseInfo { Mode = state.ToContractName() };

    /// <summary>
    /// Discloses a list inside a result that holds only its first <paramref name="returned"/>
    /// of <paramref name="total"/> entries, so no result is cut short silently. <c>details.list</c>
    /// names the list, <c>details.limit</c> the entries it holds and <c>details.actual</c> those that exist.
    /// </summary>
    /// <param name="list">The result field that was capped, e.g. <c>outline</c>.</param>
    /// <param name="returned">How many entries the list holds.</param>
    /// <param name="total">How many entries exist.</param>
    public static Warning ListTruncated(string list, int returned, int total)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(list);
        return new Warning(WarningCodes.ListTruncated, $"'{list}' lists the first {returned} of {total} entries.")
        {
            Details = new JsonObject
            {
                ["list"] = list,
                ["limit"] = returned,
                ["actual"] = total,
                ["unit"] = "entries",
            },
        };
    }

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
