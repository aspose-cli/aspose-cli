namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Content identity of one admitted input or published output file.</summary>
public sealed record FileFingerprint
{
    /// <summary>Lower-case SHA-256 digest of the complete file.</summary>
    public required string Sha256 { get; init; }
}
