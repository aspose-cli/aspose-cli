namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of <c>aspose-cli license status</c>.</summary>
public sealed record LicenseStatusResult() : ResultEnvelope(CommonSchemaIds.LicenseStatus, 2)
{
    /// <summary>Whether any compiled product uses Aspose licensing.</summary>
    public required bool Applicable { get; init; }

    /// <summary>One of the <see cref="LicenseModes"/> constants.</summary>
    public required string Mode { get; init; }

    /// <summary>
    /// Where the license was found: <c>flag</c>, <c>env:ASPOSE_LICENSE_B64</c>,
    /// <c>env:ASPOSE_LICENSE_PATH</c>, <c>project</c>
    /// or <c>user</c>. Omitted in evaluation mode.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>Resolved license file path, when the source is a file.</summary>
    public string? Path { get; init; }

    /// <summary>License application status for each compiled-in product.</summary>
    public IReadOnlyList<ProductLicenseStatus>? Products { get; init; }
}

/// <summary>One product's independently applied license status.</summary>
public sealed record ProductLicenseStatus
{
    /// <summary>Stable product identifier.</summary>
    public required string Product { get; init; }

    /// <summary>Whether Aspose licensing applies to this product.</summary>
    public required bool Applicable { get; init; }

    /// <summary>One of the <see cref="LicenseModes"/> constants.</summary>
    public required string Mode { get; init; }

    /// <summary>Resolved source for this product, when configured.</summary>
    public string? Source { get; init; }

    /// <summary>Resolved license path for this product, when the source is a file.</summary>
    public string? Path { get; init; }

    /// <summary>Safe validation problem for this product, when configuration is unusable.</summary>
    public string? Problem { get; init; }

    /// <summary>Recommended repair action when <see cref="Problem"/> is present.</summary>
    public string? Hint { get; init; }
}
