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
}
