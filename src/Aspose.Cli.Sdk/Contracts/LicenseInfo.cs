namespace Aspose.Cli.Sdk.Contracts;

/// <summary>License mode attached to results produced by engine operations.</summary>
[SchemaId("license")]
public sealed record LicenseInfo
{
    /// <summary>The license mode: <c>licensed</c>, <c>evaluation</c> or <c>not-applicable</c>.</summary>
    [AllowedValues(LicenseModes.Evaluation, LicenseModes.Licensed, LicenseModes.NotApplicable)]
    public required string Mode { get; init; }
}

/// <summary>
/// Well-known values of <see cref="LicenseInfo.Mode"/>. Kept as strings in the
/// contract so the wire format is self-describing and stable.
/// </summary>
public static class LicenseModes
{
    /// <summary>A configured license was rejected; used only by license diagnostics.</summary>
    public const string Invalid = "invalid";

    /// <summary>The product engine does not use Aspose licensing.</summary>
    public const string NotApplicable = "not-applicable";

    /// <summary>No license configured; output carries evaluation limitations.</summary>
    public const string Evaluation = "evaluation";

    /// <summary>A valid Aspose license is applied; no evaluation limitations.</summary>
    public const string Licensed = "licensed";
}
