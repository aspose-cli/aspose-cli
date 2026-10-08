using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Stable, never-overwritten safety backup created before an in-place mutation.</summary>
public sealed record BackupInfo
{
    /// <summary>Absolute backup path.</summary>
    public required string Path { get; init; }

    /// <summary>True when this invocation created the backup; false when it reused an existing one.</summary>
    public required bool Created { get; init; }

    /// <summary>Backup file size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>When the content the backup holds was last written, in UTC with the Z designator.</summary>
    [JsonConverter(typeof(UtcTimestampJsonConverter))]
    public required DateTimeOffset LastWriteUtc { get; init; }

    /// <summary>
    /// True when the backup holds exactly the version this invocation replaced; false when a
    /// reused backup holds an earlier version, which a <c>BACKUP_PREDATES_EDIT</c> warning discloses.
    /// </summary>
    public required bool HoldsReplacedVersion { get; init; }
}
