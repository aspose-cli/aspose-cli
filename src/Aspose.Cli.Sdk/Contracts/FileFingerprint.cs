namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Content identity of a file, which the write boundary compares to refuse an input that changed
/// while it was read or before its output was published. No result reports it.
/// </summary>
public sealed record FileFingerprint
{
    /// <summary>Lower-case SHA-256 digest of the complete file.</summary>
    [Pattern("^[0-9a-f]{64}$")]
    public required string Sha256 { get; init; }
}
