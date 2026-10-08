using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

internal enum PublicationTransactionState
{
    Created,
    Staging,
    Prepared,
    Publishing,
    Committed,
    RollingBack,
    RolledBack,
    Partial,
}

internal enum PublicationEntryState
{
    Staged,
    Prepared,
    Publishing,
    Published,
    Restored,
    Unchanged,
    Unknown,
}

internal enum PublicationFaultKind
{
    JournalWrite,
    JournalRead,
    JournalLockWait,
    Backup,
    Publish,
    Rollback,
    Metadata,
    Cleanup,
}

internal readonly record struct PublicationFaultPoint(
    PublicationFaultKind Kind,
    int EntryIndex,
    string Path);

internal interface IPublicationFaultInjector
{
    void Hit(PublicationFaultPoint point);
}

internal sealed class NoPublicationFaultInjector : IPublicationFaultInjector
{
    public static NoPublicationFaultInjector Instance { get; } = new();

    private NoPublicationFaultInjector()
    {
    }

    public void Hit(PublicationFaultPoint point)
    {
    }
}

/// <summary>
/// The file attributes a publication restores with a file's content. Access control is the
/// file system's: a replaced file keeps its own DACL and a new file inherits from its folder.
/// </summary>
internal sealed record FilePublicationMetadata(FileAttributes Attributes)
{
    public static FilePublicationMetadata Capture(SafeFileHandle handle) =>
        new(File.GetAttributes(handle));

    public void Apply(string path) =>
        File.SetAttributes(path, Attributes & ~FileAttributes.Directory & ~FileAttributes.ReparsePoint);

    public bool Matches(string path)
    {
        try
        {
            return Attributes == File.GetAttributes(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>
/// Creates a publication transaction directory that only the current user and LocalSystem can
/// open, with the current user as its owner. Recovery restores and deletes files on the word of
/// the journal inside, so nobody else may write there even when the output folder itself is
/// shared. The access rules are set once at creation and never re-validated; a volume or server
/// that cannot store them keeps the folder's own rules.
/// </summary>
internal static class PublicationTransactionDirectory
{
    public static string Create(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return CreateWindows(path).FullName;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or InvalidOperationException or NotSupportedException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "A transaction directory kept its folder's access rules ({0}).", exception.GetType().Name);
            }
        }
        return Directory.CreateDirectory(path).FullName;
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryInfo CreateWindows(string path)
    {
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        SecurityIdentifier user = current.User
            ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        foreach (SecurityIdentifier principal in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                principal,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }
        var directory = new DirectoryInfo(path);
        directory.Create(security);
        return directory;
    }
}

/// <summary>
/// Lets a newly published file or directory inherit access from the folder it was published
/// into. A rename keeps the DACL the entry inherited under its staging directory, so its
/// explicit rules are removed and its DACL unprotected, which makes Windows recompute the
/// inherited rules from the real parent (and propagate them below a directory). This is not a
/// check: a volume without access control lists, or an entry whose DACL the user cannot
/// rewrite, keeps the rules it was renamed with and the publication still succeeds.
/// </summary>
internal static class FilePublicationInheritance
{
    public static void TryReset(string path)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        try
        {
            if (Directory.Exists(path))
            {
                var directory = new DirectoryInfo(path);
                DirectorySecurity security = directory.GetAccessControl(AccessControlSections.Access);
                Unprotect(security);
                directory.SetAccessControl(security);
            }
            else
            {
                var file = new FileInfo(path);
                FileSecurity security = file.GetAccessControl(AccessControlSections.Access);
                Unprotect(security);
                file.SetAccessControl(security);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning(
                "A published entry kept its staging access rules ({0}).", exception.GetType().Name);
        }
    }

    /// <summary>
    /// Reads the access rules of a file about to be replaced, so <see cref="TryApplyAccess"/> can
    /// give the published replacement the permissions its owner had set instead of those of the
    /// private transaction directory it was staged in. Best effort, like <see cref="TryReset"/>.
    /// </summary>
    public static byte[]? TryCaptureAccess(string existingTarget)
    {
        if (!OperatingSystem.IsWindows()) { return null; }
        try
        {
            return new FileInfo(existingTarget).GetAccessControl(AccessControlSections.Access)
                .GetSecurityDescriptorBinaryForm();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Applies rules captured by <see cref="TryCaptureAccess"/> to the published file. Applied
    /// in place, so Windows recomputes the inherited rules from the real folder and keeps the
    /// explicit ones.
    /// </summary>
    public static void TryApplyAccess(string published, byte[]? access)
    {
        if (!OperatingSystem.IsWindows() || access is null) { return; }
        try
        {
            var security = new FileSecurity();
            security.SetSecurityDescriptorBinaryForm(access, AccessControlSections.Access);
            new FileInfo(published).SetAccessControl(security);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning(
                "A published replacement kept its staging access rules ({0}).", exception.GetType().Name);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Unprotect(FileSystemSecurity security)
    {
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: false,
                     typeof(SecurityIdentifier)))
        {
            security.RemoveAccessRuleSpecific(rule);
        }
        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
    }
}

internal sealed record FilePublicationSnapshot(
    bool Exists,
    long Length,
    long LastWriteUtcTicks,
    string? Sha256,
    FilePublicationMetadata? Metadata,
    FilePhysicalIdentity? PhysicalIdentity)
{
    public static FilePublicationSnapshot Missing { get; } =
        new(false, 0, 0, null, null, null);

    public static FilePublicationSnapshot Capture(string path)
    {
        if (!File.Exists(path))
        {
            return Missing;
        }

        try
        {
            // Everything is read from this one handle, so another process may still replace
            // or delete the path meanwhile, as a concurrent in-place edit does.
            using SafeFileHandle handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                FileOptions.SequentialScan);
            FilePhysicalIdentity? identity =
                FilePublicationOwnedDelete.TryGetIdentity(handle);
            if (OperatingSystem.IsWindows() && identity is null)
            {
                throw new IOException(
                    $"Could not establish the physical identity of '{path}'.");
            }
            long length = RandomAccess.GetLength(handle);
            long lastWriteUtcTicks = File.GetLastWriteTimeUtc(handle).Ticks;
            string hash;
            using (var stream = new FileStream(handle, FileAccess.Read))
            {
                hash = Convert.ToHexString(SHA256.HashData(stream));
                long verifiedLength = stream.Length;
                long verifiedLastWriteUtcTicks =
                    File.GetLastWriteTimeUtc(handle).Ticks;
                if (length != verifiedLength
                    || lastWriteUtcTicks != verifiedLastWriteUtcTicks)
                {
                    throw new IOException(
                        $"File '{path}' changed while its publication snapshot was captured.");
                }
                return new FilePublicationSnapshot(
                    true,
                    length,
                    verifiedLastWriteUtcTicks,
                    hash,
                    FilePublicationMetadata.Capture(handle),
                    identity);
            }
        }
        catch (FileNotFoundException)
        {
            return Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return Missing;
        }
    }

    public bool ContentMatches(string path)
    {
        FilePublicationSnapshot current = Capture(path);
        return ContentEquals(current);
    }

    public bool ContentEquals(FilePublicationSnapshot other) =>
        Exists == other.Exists
        && (!Exists
            || (Length == other.Length
                && string.Equals(Sha256, other.Sha256, StringComparison.Ordinal)));

    public bool VersionEquals(FilePublicationSnapshot other) =>
        ContentEquals(other)
        && (!Exists
            || (OperatingSystem.IsWindows()
                ? PhysicalIdentity is not null
                    && other.PhysicalIdentity is not null
                    && PhysicalIdentity.Value == other.PhysicalIdentity.Value
                : PhysicalIdentity is null
                    || other.PhysicalIdentity is null
                    || PhysicalIdentity.Value == other.PhysicalIdentity.Value));

    public bool IsStructurallyValid() =>
        Exists
            ? Length >= 0
                && LastWriteUtcTicks > 0
                && Sha256 is { Length: 64 }
                && Sha256.All(Uri.IsHexDigit)
                && Metadata is not null
                && (!OperatingSystem.IsWindows() || PhysicalIdentity is not null)
            : Length == 0
                && LastWriteUtcTicks == 0
                && Sha256 is null
                && Metadata is null
                && PhysicalIdentity is null;
}

/// <summary>Flushes a written file's data to disk before the CLI relies on it.</summary>
internal static class DurableFile
{
    public static void Flush(string path)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete);
        stream.Flush(flushToDisk: true);
    }
}

internal sealed class PublicationJournal
{
    internal const int MaximumBytes = PublicationLimits.MaximumMetadataBytes;
    internal const int MaximumEntries = PublicationLimits.MaximumEntries;
    internal static bool IsTemporaryPath(string path)
    {
        string name = Path.GetFileName(path);
        string prefix = "." + AtomicPublicationPlan.JournalName + ".";
        const string suffix = ".tmp";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }
        string id = name[prefix.Length..^suffix.Length];
        return id.Length == 32 && id.All(Uri.IsHexDigit);
    }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public int Version { get; init; } = 1;

    public required string Operation { get; init; }

    public int OwnerProcessId { get; init; } = Environment.ProcessId;

    internal static long CurrentProcessStartUtcTicks { get; } = ProcessStartTicks();
    public long OwnerProcessStartUtcTicks { get; init; } = CurrentProcessStartUtcTicks;

    private static long ProcessStartTicks()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime().Ticks;
    }

    public PublicationTransactionState State { get; set; }

    public List<PublicationJournalEntry> Entries { get; init; } = [];

    public NewDirectoryOutput? DirectoryOutput { get; set; }

    public static PublicationJournal Read(string path, IPublicationFaultInjector? faults = null, OperationDeadline? deadline = null)
    {
        using PublicationJournalLock gate = PublicationJournalLock.Acquire(path, deadline, faults);
        return ReadLocked(path, faults, deadline);
    }

    /// <summary>
    /// Reads the journal unless a live process holds its lock, which proves the transaction is in
    /// use; returns null then instead of waiting.
    /// </summary>
    internal static PublicationJournal? ReadUnlessBusy(string path, OperationDeadline? deadline = null)
    {
        using PublicationJournalLock? gate = PublicationJournalLock.TryAcquire(path);
        return gate is null ? null : ReadLocked(path, faults: null, deadline);
    }

    private static PublicationJournal ReadLocked(string path, IPublicationFaultInjector? faults, OperationDeadline? deadline)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        faults?.Hit(new PublicationFaultPoint(PublicationFaultKind.JournalRead, -1, path));
        if (stream.Length < 1 || stream.Length > MaximumBytes)
        {
            throw new InvalidDataException(
                $"Publication journal '{path}' has an invalid bounded size.");
        }
        PublicationJournal journal =
            JsonSerializer.Deserialize<PublicationJournal>(
                stream,
                JsonOptions)
            ?? throw new InvalidDataException(
                $"Publication journal '{path}' is empty.");
        if (journal.Version != 1
            || string.IsNullOrWhiteSpace(journal.Operation)
            || journal.Operation.Length > 128
            || journal.OwnerProcessId <= 0
            || journal.Entries is null
            || journal.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException(
                $"Publication journal '{path}' violates its bounded contract.");
        }
        deadline?.ThrowIfExpired("publication-journal-read");
        return journal;
    }

    /// <summary>Checks a fully populated recovery record before any target can be changed.</summary>
    internal int EnsureLifecycleCapacity(string path)
    {
        var complete = new PublicationJournal
        {
            Operation = Operation, OwnerProcessId = OwnerProcessId,
            OwnerProcessStartUtcTicks = OwnerProcessStartUtcTicks,
            State = PublicationTransactionState.RollingBack,
            DirectoryOutput = DirectoryOutput,
            Entries = Entries.Select(entry => new PublicationJournalEntry
            {
                Index = entry.Index, Target = entry.Target, Staged = entry.Staged,
                Overwrite = entry.Overwrite, DeleteTarget = entry.DeleteTarget, Size = entry.Size,
                Original = entry.Original, StagedSnapshot = entry.StagedSnapshot,
                InputPath = entry.InputPath, InputSnapshot = entry.InputSnapshot,
                RequestedBackup = entry.RequestedBackup, RequestedBackupOriginal = entry.RequestedBackupOriginal,
                TargetParentIdentity = entry.TargetParentIdentity,
                RequestedBackupParentIdentity = entry.RequestedBackupParentIdentity,
                Backup = entry.Original.Exists ? Path.Combine(Path.GetDirectoryName(path)!, "backups", $"{entry.Index + 1:000000}.backup") : null,
                BackupSnapshot = entry.Original.Exists ? LargestSnapshot(entry) : null,
                Displaced = entry.Original.Exists ? Path.Combine(Path.GetDirectoryName(path)!, "backups", $"{entry.Index + 1:000000}.displaced") : null,
                DisplacedSnapshot = entry.Original.Exists ? LargestSnapshot(entry) : null,
                PublishedSnapshot = LargestSnapshot(entry), State = PublicationEntryState.Unknown,
            }).ToList(),
        };
        return Encoding.UTF8.GetByteCount(complete.SerializeBounded(path));
    }

    private static FilePublicationSnapshot LargestSnapshot(PublicationJournalEntry entry)
    {
        FilePublicationSnapshot largest = JsonSerializer.SerializeToUtf8Bytes(entry.Original, JsonOptions).Length
            > JsonSerializer.SerializeToUtf8Bytes(entry.StagedSnapshot, JsonOptions).Length
            ? entry.Original : entry.StagedSnapshot;
        return largest with
        {
            Length = long.MaxValue, LastWriteUtcTicks = long.MaxValue,
            PhysicalIdentity = OperatingSystem.IsWindows() ? new FilePhysicalIdentity(uint.MaxValue, ulong.MaxValue) : null,
        };
    }

    private string SerializeBounded(string path)
    {
        if (Entries.Count > MaximumEntries)
        {
            throw CliErrors.OutputUnwritable(
                path,
                "the publication journal entry budget was exceeded",
                phase: "journal");
        }
        string contents = JsonSerializer.Serialize(this, JsonOptions);
        if (Encoding.UTF8.GetByteCount(contents) > MaximumBytes)
        {
            throw CliErrors.OutputUnwritable(
                path,
                "the publication journal byte budget was exceeded",
                phase: "journal");
        }
        return contents;
    }

    /// <summary>Coordinates deletion with bounded readers and atomic writers of this journal.</summary>
    internal static void Delete(string path, IPublicationFaultInjector? faults = null)
    {
        using PublicationJournalLock gate = PublicationJournalLock.Acquire(path, faults: faults);
        if (!File.Exists(path)) { return; }
        if (!FilePublicationOwnedDelete.TryDelete(path, FilePublicationSnapshot.Capture(path)))
        { throw new IOException("A changed publication journal was preserved."); }
    }

    public void Write(string path, IPublicationFaultInjector? faults = null, OperationDeadline? deadline = null)
    {
        using PublicationJournalLock gate = PublicationJournalLock.Acquire(path, deadline, faults);
        string contents = SerializeBounded(path);
        string directory = Path.GetDirectoryName(path)
            ?? throw new IOException(
                $"Publication journal '{path}' has no parent directory.");
        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        FilePublicationSnapshot expected =
            FilePublicationSnapshot.Capture(path);
        using var temporary = OwnedTemporaryFile.Create(tempPath);
        File.WriteAllText(tempPath, contents);
        temporary.BindProducedFile();
        DurableFile.Flush(tempPath);
        FilePublicationSnapshot staged =
            FilePublicationSnapshot.Capture(tempPath);
        FilePublicationSnapshot current =
            FilePublicationSnapshot.Capture(path);
        if (!expected.VersionEquals(current))
        {
            throw PublicationErrors.OutputConflict(path, expected, current);
        }
        deadline?.ThrowIfExpired("publication-journal-write");
        AtomicFileRename.Move(tempPath, path, overwrite: expected.Exists, deadline);
        temporary.MarkPublished();
        if (!staged.VersionEquals(FilePublicationSnapshot.Capture(path)))
        {
            throw new IOException(
                $"Publication journal '{path}' changed during atomic replacement.");
        }
        DurableFile.Flush(path);
    }
}

internal sealed class PublicationJournalEntry
{
    public required int Index { get; init; }

    public required string Target { get; init; }

    public required string Staged { get; init; }

    public required bool Overwrite { get; init; }

    public string? Backup { get; set; }

    public FilePublicationSnapshot? BackupSnapshot { get; set; }

    public string? Displaced { get; init; }

    public FilePublicationSnapshot? DisplacedSnapshot { get; set; }

    public FilePublicationSnapshot? PublishedSnapshot { get; set; }

    public string? RequestedBackup { get; init; }

    public FilePublicationSnapshot? RequestedBackupOriginal { get; init; }

    public string? InputPath { get; init; }

    public FilePublicationSnapshot? InputSnapshot { get; init; }

    public required FilePublicationSnapshot Original { get; init; }

    public required FilePublicationSnapshot StagedSnapshot { get; init; }

    public FilePhysicalIdentity? TargetParentIdentity { get; init; }

    public FilePhysicalIdentity? RequestedBackupParentIdentity { get; init; }

    public PublicationEntryState State { get; set; } = PublicationEntryState.Staged;

    public bool DeleteTarget { get; init; }

    public long Size { get; set; }
}

internal sealed record PublicationRecoveryItem(
    string Target,
    bool OriginalExisted,
    bool Published,
    string Status,
    bool ContentVerified,
    bool MetadataVerified,
    string? Failure);

internal sealed record PublicationRecoveryReport(
    bool RecoveryComplete,
    IReadOnlyList<PublicationRecoveryItem> Items);
