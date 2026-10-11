namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Describes a file a command produced.</summary>
/// <remarks>
/// Products build it only with <c>FileObjects.Output</c>, which makes the path absolute and takes
/// the size and fingerprint from the file its publication wrote.
/// </remarks>
[SchemaId("output")]
public sealed record OutputInfo
{
    /// <summary>Absolute path of the produced file.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// Format id of the produced file, e.g. <c>pdf</c>; for a companion file the engine writes
    /// beside it, such as a script of an HTML5 deck, the companion's file extension.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>File size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 identity of the published file.</summary>
    public required FileFingerprint Fingerprint { get; init; }

    /// <summary>
    /// <c>true</c> when the produced file is encrypted and opens only with its password;
    /// omitted when its format cannot carry a password.
    /// </summary>
    public bool? Encrypted { get; init; }
}
