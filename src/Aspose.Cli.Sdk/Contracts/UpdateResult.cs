namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of <c>aspose-cli update</c>: an update check, or an install handed to the installer.</summary>
public sealed record UpdateResult() : ResultEnvelope("update-result", 2)
{
    /// <summary>
    /// <c>up-to-date</c> when no newer release exists, <c>available</c> when one does, or
    /// <c>pending</c> when its install was handed to the installer process.
    /// </summary>
    [AllowedValues("up-to-date", "available", "pending")]
    public required string Status { get; init; }

    /// <summary>The running CLI's artifact version.</summary>
    [MinLength(1)]
    public required string CurrentVersion { get; init; }

    /// <summary>The newer release's artifact version; omitted when the CLI is up to date.</summary>
    [MinLength(1)]
    public string? AvailableVersion { get; init; }

    /// <summary>The source revision the feed's release was built from.</summary>
    [Pattern("^[0-9a-f]{40}$")]
    public string? SourceRevision { get; init; }

    /// <summary>The release feed that was checked.</summary>
    [MinLength(1)]
    public string? Feed { get; init; }

    /// <summary>The lowercase hex SHA-256 of the release archive.</summary>
    [Pattern("^[0-9a-f]{64}$")]
    public string? ArchiveSha256 { get; init; }

    /// <summary>The installer process id; present only when the status is pending.</summary>
    [Minimum(1)]
    public int? ProcessId { get; init; }
}
