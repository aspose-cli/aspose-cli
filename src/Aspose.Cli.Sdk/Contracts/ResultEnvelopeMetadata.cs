namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Common result metadata transported across internal process boundaries.
/// Keeping the fields together prevents a parent process from accidentally
/// reconstructing or dropping license and warning disclosures produced by the
/// process that executed the operation.
/// </summary>
public sealed record ResultEnvelopeMetadata
{
    /// <summary>License mode reported by the process that executed the operation.</summary>
    public LicenseInfo? License { get; init; }

    /// <summary>Warnings reported by the process that executed the operation.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>Captures every mutable common metadata field from a result.</summary>
    public static ResultEnvelopeMetadata From(ResultEnvelope result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ResultEnvelopeMetadata
        {
            License = result.License,
            Warnings = result.Warnings,
        };
    }
}
