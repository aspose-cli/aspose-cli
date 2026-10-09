namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli license status</c>: the effective, independently verified license
/// status of every selected product; there is no first-product summary.
/// </summary>
public sealed record LicenseStatusResult() : ResultEnvelope("license-status", 2)
{
    /// <summary>Whether a license changes what any selected product produces.</summary>
    public required bool Applicable { get; init; }

    /// <summary>The license status of each selected product.</summary>
    [MinItems(1)]
    public required IReadOnlyList<ProductLicenseStatus> Products { get; init; }

    /// <summary>Whether the shared user license, which every product reads, is installed.</summary>
    public bool SharedUserLicenseInstalled { get; init; }

    /// <summary>Opaque identity of this validated snapshot; absent when any selected source is invalid.</summary>
    [Pattern("^[0-9a-f]{64}$")]
    public string? Identity { get; init; }
}

/// <summary>Effective license configuration and validation for one product.</summary>
public sealed record ProductLicenseStatus
{
    /// <summary>Product id, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Product { get; init; }

    /// <summary>Human-readable product name.</summary>
    [MinLength(1)]
    public required string Name { get; init; }

    /// <summary>Whether a license changes what the product produces.</summary>
    public required bool Applicable { get; init; }

    /// <summary>The license mode the product runs under.</summary>
    [AllowedValues(LicenseModes.Evaluation, LicenseModes.Licensed, LicenseModes.Invalid, LicenseModes.NotApplicable)]
    public required string Mode { get; init; }

    /// <summary>Where the license comes from, such as an environment variable or the user license; omitted when there is none.</summary>
    [MinLength(1)]
    public string? Source { get; init; }

    /// <summary>The license file's path, when the license is a file.</summary>
    [MinLength(1)]
    public string? Path { get; init; }

    /// <summary>Why the configured license was rejected; omitted when it is valid.</summary>
    [MinLength(1)]
    public string? Problem { get; init; }

    /// <summary>How to fix the license; omitted when nothing needs fixing.</summary>
    [MinLength(1)]
    public string? Hint { get; init; }

    /// <summary>Whether a user license for this product is installed.</summary>
    public bool UserLicenseInstalled { get; init; }
}
