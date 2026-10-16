namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Describes a file a command read as the value of one of its options, such as the template or
/// the content a file is created from: the facts of an <see cref="InputInfo"/> and the option.
/// </summary>
/// <remarks>
/// Products build it only with <c>FileObjects.Sources</c>, which makes the path absolute, reads
/// the size and fingerprint from the file itself and keeps the order of the command line.
/// </remarks>
[SchemaId("source-input")]
public sealed record SourceInput
{
    /// <summary>Absolute path of the file.</summary>
    public required string Path { get; init; }

    /// <summary>Format id the file was read as, e.g. <c>md</c>.</summary>
    public required string Format { get; init; }

    /// <summary>File size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 identity of the file as it was read.</summary>
    public required FileFingerprint Fingerprint { get; init; }

    /// <summary><c>true</c> when the file is encrypted and opens only with its password.</summary>
    public required bool Encrypted { get; init; }

    /// <summary>The option that named the file, e.g. <c>--template</c>.</summary>
    [MinLength(1)]
    public required string Option { get; init; }
}
