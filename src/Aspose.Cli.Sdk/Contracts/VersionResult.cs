namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli --version --output-format json</c>: the build identity.
/// </summary>
public sealed record VersionResult() : ResultEnvelope("version")
{
    /// <summary>CLI version without build metadata, e.g. <c>1.0.0</c>; the same value as the <c>schema</c> catalog's <c>cliVersion</c>.</summary>
    [MinLength(1)]
    public required string CliVersion { get; init; }

    /// <summary>
    /// Full artifact version, as plain <c>--version</c> prints it, when it carries build metadata
    /// beyond <see cref="CliVersion"/>; omitted when the two are equal.
    /// </summary>
    [MinLength(1)]
    public string? ArtifactVersion { get; init; }

    /// <summary>Root source revision embedded by the distribution build.</summary>
    [MinLength(1)]
    public required string SourceRevision { get; init; }

    /// <summary>Whether tracked source changes were present during the build.</summary>
    public required bool BuildDirty { get; init; }

    /// <summary>The engine package and version compiled for each product.</summary>
    public required IReadOnlyList<EnginePin> EnginePins { get; init; }
}

/// <summary>The engine package one product is compiled with.</summary>
public sealed record EnginePin
{
    /// <summary>Product id, e.g. <c>cells</c>.</summary>
    [MinLength(1)]
    public required string Product { get; init; }

    /// <summary>Engine package compiled for the product, e.g. <c>Aspose.Cells</c>.</summary>
    [MinLength(1)]
    public required string Engine { get; init; }

    /// <summary>Exact engine package version.</summary>
    [MinLength(1)]
    public required string Version { get; init; }
}
