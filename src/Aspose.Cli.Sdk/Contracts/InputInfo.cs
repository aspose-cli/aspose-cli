namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Describes a file a command read.</summary>
/// <remarks>
/// Products build it only with <c>FileObjects.Input</c>, which makes the path absolute and reads
/// the size and fingerprint from the file itself.
/// </remarks>
[SchemaId("input")]
public sealed record InputInfo
{
    /// <summary>Absolute path of the file.</summary>
    public required string Path { get; init; }

    /// <summary>Detected format id, e.g. <c>xlsx</c>.</summary>
    public required string Format { get; init; }

    /// <summary>File size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 identity of the file as it was read.</summary>
    public required FileFingerprint Fingerprint { get; init; }

    /// <summary><c>true</c> when the file is encrypted and opens only with its password.</summary>
    public required bool Encrypted { get; init; }
}
