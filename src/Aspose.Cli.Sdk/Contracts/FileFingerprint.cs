namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Content identity of one admitted input or published output file.</summary>
[SchemaId("file-fingerprint")]
public sealed record FileFingerprint
{
    /// <summary>Lower-case SHA-256 digest of the complete file.</summary>
    [Pattern("^[0-9a-f]{64}$")]
    public required string Sha256 { get; init; }
}
