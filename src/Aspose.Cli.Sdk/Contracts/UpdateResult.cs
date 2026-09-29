namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of an update check or install.</summary>
public sealed record UpdateResult() : ResultEnvelope(CommonSchemaIds.Update, 2)
{
    public required string Status { get; init; }

    public required string CurrentVersion { get; init; }

    public string? AvailableVersion { get; init; }

    public string? SourceRevision { get; init; }

    public string? Feed { get; init; }

    public string? ArchiveSha256 { get; init; }

    public int? ProcessId { get; init; }
}
