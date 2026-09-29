namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli --version --output json</c>: the build identity alone, so a caller
/// can read the version without the full capabilities document.
/// </summary>
public sealed record VersionResult() : ResultEnvelope(CommonSchemaIds.Version, 2)
{
    /// <summary>CLI version without build metadata, e.g. <c>1.0.0</c>; the same value as capabilities <c>cliVersion</c>.</summary>
    public required string CliVersion { get; init; }

    /// <summary>
    /// Full artifact version, as plain <c>--version</c> prints it, when it carries build metadata
    /// beyond <see cref="CliVersion"/>; omitted when the two are equal.
    /// </summary>
    public string? ArtifactVersion { get; init; }

    /// <summary>Root source revision embedded by the distribution build.</summary>
    public required string SourceRevision { get; init; }

    /// <summary>Whether tracked source changes were present during the build.</summary>
    public required bool BuildDirty { get; init; }

    /// <summary>Exact engine version compiled for each product, as capabilities reports them.</summary>
    public required IReadOnlyList<EnginePinCapabilities> EnginePins { get; init; }
}
