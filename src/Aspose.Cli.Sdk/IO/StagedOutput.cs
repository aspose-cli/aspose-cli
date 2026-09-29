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
    public SafeBackupResult? Backup => _entry.RequestedBackup is { } path && _entry.Original.Exists
        ? new SafeBackupResult(path, !_entry.RequestedBackupOriginal!.Exists,
            _entry.RequestedBackupOriginal.Exists ? _entry.RequestedBackupOriginal.Length : _entry.Original.Length)
        : null;

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
