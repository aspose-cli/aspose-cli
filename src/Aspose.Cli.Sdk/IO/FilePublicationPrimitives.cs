using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Errors;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

internal enum FilePublicationDurability
{
    None,
    File,
    FileAndDirectory,
}

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

internal sealed record FilePublicationMetadata(
    FileAttributes Attributes,
    int? UnixMode,
    byte[]? WindowsSecurityDescriptor)
{
    public static FilePublicationMetadata Capture(string path)
    {
        // On Unix, Hidden is derived from a leading dot in the path rather
        // than stored metadata. Transaction backups deliberately use dot
        // names, so comparing that synthetic flag would make an otherwise
        // verified backup impossible to restore. Unix mode is the durable
        // permission contract on those platforms.
        FileAttributes attributes = OperatingSystem.IsWindows()
            ? File.GetAttributes(path)
            : FileAttributes.Normal;
        int? unixMode = OperatingSystem.IsWindows()
            ? null
            : (int)File.GetUnixFileMode(path);
        byte[]? securityDescriptor = null;
        if (OperatingSystem.IsWindows())
        {
            FileSecurity security = FileSystemAclExtensions.GetAccessControl(
                new FileInfo(path),
                AccessControlSections.Access | AccessControlSections.Owner);
            securityDescriptor = security.GetSecurityDescriptorBinaryForm();
        }

        return new FilePublicationMetadata(attributes, unixMode, securityDescriptor);
    }

    public void Apply(string path)
    {
        if (!OperatingSystem.IsWindows() && UnixMode is { } unixMode)
        {
            ApplyUnixMode(path, unixMode);
        }

        if (OperatingSystem.IsWindows()
            && WindowsSecurityDescriptor is { Length: > 0 } descriptor)
        {
            ApplyWindowsSecurity(path, descriptor);
        }

        FileAttributes portable = Attributes
            & ~FileAttributes.Directory
            & ~FileAttributes.ReparsePoint;
        File.SetAttributes(path, portable);
    }

    public void ApplyContentAttributes(string path)
    {
        if (!OperatingSystem.IsWindows() && UnixMode is { } unixMode)
        {
            ApplyUnixMode(path, unixMode);
        }

        FileAttributes portable = Attributes
            & ~FileAttributes.Directory
            & ~FileAttributes.ReparsePoint;
        File.SetAttributes(path, portable);
    }

    [UnsupportedOSPlatform("windows")]
    private static void ApplyUnixMode(string path, int unixMode) =>
        File.SetUnixFileMode(path, (UnixFileMode)unixMode);

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsSecurity(string path, byte[] descriptor)
    {
        var security = new FileSecurity();
        security.SetSecurityDescriptorBinaryForm(
            descriptor,
            AccessControlSections.Access | AccessControlSections.Owner);
        FileSystemAclExtensions.SetAccessControl(new FileInfo(path), security);
    }

    public bool Matches(string path)
    {
        try
        {
            FilePublicationMetadata current = Capture(path);
            return Attributes == current.Attributes
                && UnixMode == current.UnixMode
                && EqualBytes(WindowsSecurityDescriptor, current.WindowsSecurityDescriptor);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static void ResetAccessToInherited(string path)
    {
        var file = new FileInfo(path);
        FileSecurity security = file.GetAccessControl(
            AccessControlSections.Access);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: false,
                     typeof(System.Security.Principal.SecurityIdentifier)))
        {
            security.RemoveAccessRuleSpecific(rule);
        }
        security.SetAccessRuleProtection(
            isProtected: false,
            preserveInheritance: false);
        file.SetAccessControl(security);
    }

    private static bool EqualBytes(byte[]? left, byte[]? right) =>
        left is null
            ? right is null
            : right is not null && left.AsSpan().SequenceEqual(right);
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
            FileShare share = OperatingSystem.IsWindows()
                ? FileShare.Read
                : FileShare.Read | FileShare.Delete;
            using SafeFileHandle handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                share,
                FileOptions.SequentialScan);
            FilePhysicalIdentity? identity =
                FilePublicationOwnedDelete.TryGetIdentity(handle);
            if (OperatingSystem.IsWindows() && identity is null)
            {
                throw new IOException(
                    $"Could not establish the physical identity of '{path}'.");
            }
            long length = RandomAccess.GetLength(handle);
            long lastWriteUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;
            string hash;
            using (var stream = new FileStream(handle, FileAccess.Read))
            {
                hash = Convert.ToHexString(SHA256.HashData(stream));
                long verifiedLength = stream.Length;
                long verifiedLastWriteUtcTicks =
                    File.GetLastWriteTimeUtc(path).Ticks;
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
                    FilePublicationMetadata.Capture(path),
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

    public bool FullyMatches(string path)
    {
        FilePublicationSnapshot current = Capture(path);
        return ContentEquals(current)
            && (!Exists || Metadata is null || Metadata.Matches(path));
    }

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

internal static class FilePublicationDurabilityAdapter
{
    public static void FlushFile(string path)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete);
        stream.Flush(flushToDisk: true);
    }

    public static void FlushDirectory(string directory)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(
                directory,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                FileOptions.None);
            RandomAccess.FlushToDisk(handle);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            throw new PlatformNotSupportedException(
                $"Directory durability is not available for '{directory}' on this file system.",
                exception);
        }
    }

    public static void Flush(string file, FilePublicationDurability durability)
    {
        if (durability is FilePublicationDurability.File or FilePublicationDurability.FileAndDirectory)
        {
            FlushFile(file);
        }

        if (durability == FilePublicationDurability.FileAndDirectory)
        {
            FlushDirectory(Path.GetDirectoryName(file)!);
        }
    }
}

internal sealed class PublicationJournal
{
    internal const int MaximumBytes = 1024 * 1024;
    internal const int MaximumEntries = 256;
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

    public long OwnerProcessStartUtcTicks { get; init; } =
        Environment.ProcessPath is null
            ? 0
            : System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;

    public PublicationTransactionState State { get; set; }

    public List<PublicationJournalEntry> Entries { get; init; } = [];

    public static PublicationJournal Read(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
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
        return journal;
    }

    public void Write(string path)
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
        FilePublicationDurabilityAdapter.FlushFile(tempPath);
        FilePublicationSnapshot staged =
            FilePublicationSnapshot.Capture(tempPath);
        FilePublicationSnapshot current =
            FilePublicationSnapshot.Capture(path);
        if (!expected.VersionEquals(current))
        {
            throw CliErrors.OutputConflict(path, expected, current);
        }
        File.Move(tempPath, path, overwrite: expected.Exists);
        temporary.MarkPublished();
        if (!staged.VersionEquals(FilePublicationSnapshot.Capture(path)))
        {
            throw new IOException(
                $"Publication journal '{path}' changed during atomic replacement.");
        }
        FilePublicationDurabilityAdapter.FlushFile(path);
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
