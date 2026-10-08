namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Describes a file an operation produced.</summary>
public sealed record OutputInfo
{
    /// <summary>Absolute path of the produced file.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// Format id of the produced file, e.g. <c>pdf</c>; null for a companion file the engine
    /// writes beside it, such as a script of an HTML5 deck.
    /// </summary>
    public required string? Format { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 identity when the owning workflow captured one.</summary>
    public FileFingerprint? Fingerprint { get; init; }

    /// <summary>
    /// <c>true</c> when the produced file is encrypted and opens only with its password, <c>false</c>
    /// when it is not; omitted when the owning workflow does not report it.
    /// </summary>
    public bool? Encrypted { get; init; }
}
