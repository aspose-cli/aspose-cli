namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Describes the input file an operation ran against.</summary>
public sealed record SourceInfo
{
    /// <summary>Absolute path of the file.</summary>
    public required string Path { get; init; }

    /// <summary>Detected format id, e.g. <c>xlsx</c>.</summary>
    public required string Format { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 identity when the owning workflow captured one.</summary>
    public FileFingerprint? Fingerprint { get; init; }

    /// <summary>
    /// <c>true</c> when the file is encrypted and opens only with its password, <c>false</c>
    /// when it is not; omitted when the owning workflow does not report it.
    /// </summary>
    public bool? Encrypted { get; init; }
}
