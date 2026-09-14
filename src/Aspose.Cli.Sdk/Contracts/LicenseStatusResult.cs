namespace Aspose.Cli.Sdk.Contracts;

/// <summary>One authoritative status for every requested product; no first-product summary.</summary>
public sealed record LicenseStatusResult() : ResultEnvelope(CommonSchemaIds.LicenseStatus, 2)
{
    public required bool Applicable { get; init; }
    public required IReadOnlyList<ProductLicenseStatus> Products { get; init; }
    public bool SharedUserLicenseInstalled { get; init; }
    /// <summary>Opaque identity of this validated snapshot; absent when any selected source is invalid.</summary>
    public string? Identity { get; init; }
}

/// <summary>Effective license configuration and validation for one product.</summary>
public sealed record ProductLicenseStatus
{
    public required string Product { get; init; }
    public required string Name { get; init; }
    public required bool Applicable { get; init; }
    public required string Mode { get; init; }
    public string? Source { get; init; }
    public string? Path { get; init; }
    public string? Problem { get; init; }
    public string? Hint { get; init; }
    public bool UserLicenseInstalled { get; init; }
}
