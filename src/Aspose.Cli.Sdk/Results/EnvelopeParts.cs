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
    /// The license block of a result: the mode when the engine ran without a license, and none
    /// when it ran licensed, since a licensed result omits it.
    /// </summary>
    public static LicenseInfo? License(LicenseState state) => state == LicenseState.Licensed
        ? null
        : new LicenseInfo { Mode = state.ToContractName() };

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
