using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.IO;

/// <summary>The exact staged file produced for one final output, before publication.</summary>
public sealed class StagedOutput
{
    private readonly PublicationJournalEntry _entry;

    internal StagedOutput(PublicationJournalEntry entry) => _entry = entry;

    public string Path => _entry.Staged;
    public string TargetPath => _entry.Target;
    public long SizeBytes => _entry.Size;
    public FileFingerprint Fingerprint => new() { Sha256 = _entry.StagedSnapshot.Sha256!.ToLowerInvariant() };

    internal FilePublicationSnapshot Snapshot => _entry.StagedSnapshot;
    /// <summary>
    /// The requested safety backup: the one this publication creates, or the existing one it
    /// keeps, which may hold an earlier version than the file it replaces.
    /// </summary>
    public BackupInfo? Backup => _entry.RequestedBackup is { } path && _entry.Original.Exists
        ? Describe(path, _entry.RequestedBackupOriginal!.Exists ? _entry.RequestedBackupOriginal : null, _entry.Original)
        : null;

    private static BackupInfo Describe(string path, FilePublicationSnapshot? kept, FilePublicationSnapshot replaced)
    {
        // A created backup is a copy of the replaced file, which keeps its last-write time.
        FilePublicationSnapshot held = kept ?? replaced;
        return new BackupInfo
        {
            Path = path,
            Created = kept is null,
            SizeBytes = held.Length,
            LastWriteUtc = new DateTimeOffset(held.LastWriteUtcTicks, TimeSpan.Zero),
            HoldsReplacedVersion = kept is null
                || string.Equals(kept.Sha256, replaced.Sha256, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Inspects the bound candidate while preventing Windows writers from replacing it.</summary>
    public T Read<T>(Func<string, T> inspect)
    {
        ArgumentNullException.ThrowIfNull(inspect);
        using var lease = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        EnsureUnchanged();
        T result = inspect(Path);
        EnsureUnchanged();
        return result;
    }

    internal void EnsureUnchanged()
    {
        if (!_entry.StagedSnapshot.VersionEquals(FilePublicationSnapshot.Capture(Path)))
        {
            throw new IOException($"Staged output '{TargetPath}' changed after production.");
        }
    }
}
