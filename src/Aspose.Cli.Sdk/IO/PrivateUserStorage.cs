using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Creates and validates storage that is accessible only to the current OS
/// user (and, on Windows, LocalSystem). Security is established on the parent
/// directory before a file is created, so a permissive umask or inherited ACL
/// never creates a readable secret window.
/// </summary>
public static class PrivateUserStorage
{
    private const int MaximumCleanupEntries = 4096;
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>
    /// Creates a private per-user temporary directory below the shared
    /// Aspose CLI temporary root. Every application-owned path component is
    /// hardened before the next component is created.
    /// </summary>
    public static string CreateTemporaryDirectory(
        string category,
        string? namePrefix = null)
    {
        if (string.IsNullOrWhiteSpace(category)
            || category.Any(static character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Temporary storage categories use letters, digits, '-' or '_'.",
                nameof(category));
        }

        string root = TemporaryRoot();
        string categoryRoot = EnsureDirectory(Path.Combine(root, category));
        string prefix = string.IsNullOrWhiteSpace(namePrefix)
            ? string.Empty
            : namePrefix + "-";
        return EnsureDirectory(Path.Combine(
            categoryRoot,
            prefix + Guid.NewGuid().ToString("N")));
    }

    /// <summary>
    /// Returns the private temporary root for the current OS user. The
    /// identity suffix prevents one local user from claiming or hardening the
    /// path needed by another user.
    /// </summary>
    public static string TemporaryRoot()
    {
        string identity = OperatingSystem.IsWindows()
            ? CurrentUserSid().Value
            : GetEffectiveUserId()
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        string suffix = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(identity))
                    .AsSpan(0, 8))
            .ToLowerInvariant();
        return EnsureDirectory(Path.Combine(
            Path.GetTempPath(),
            DistributionInfo.Id + "-" + suffix));
    }

    /// <summary>
    /// Ensures one directory exists with a private ACL/mode and rejects any
    /// symbolic-link or reparse-point component.
    /// </summary>

    /// <summary>Neutral per-user namespace for cross-application document publication locks.</summary>
    internal static string PublicationLockRoot()
    {
        string identity = OperatingSystem.IsWindows()
            ? CurrentUserSid().Value
            : GetEffectiveUserId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 8)).ToLowerInvariant();
        return EnsureDirectory(Path.Combine(Path.GetTempPath(), "aspose-document-publication-" + suffix));
    }

    public static string EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        RejectLinkedComponents(full, includeLeaf: true);

        if (!Directory.Exists(full))
        {
            string? parent = Path.GetDirectoryName(full);
            if (parent is not null && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (OperatingSystem.IsWindows())
            {
                CreateWindowsDirectory(full);
            }
            else
            {
                Directory.CreateDirectory(full, PrivateDirectoryMode);
            }
        }

        Harden(full, isDirectory: true);
        ValidateDirectory(full);
        return full;
    }

    /// <summary>
    /// Creates a new private file exclusively. The parent is validated before
    /// creation and an existing path (including a symlink) is never followed.
    /// </summary>
    public static FileStream CreateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        string directory = EnsureDirectory(
            Path.GetDirectoryName(full)
                ?? throw new ArgumentException("A private file needs a parent directory.", nameof(path)));
        RejectLinkedComponents(directory, includeLeaf: true);
        if (File.Exists(full) || Directory.Exists(full))
        {
            throw new IOException($"Private file already exists: {full}");
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }

        FileStream stream = new(full, options);
        try
        {
            Harden(full, isDirectory: false);
            ValidateFile(full);
            return stream;
        }
        catch
        {
            stream.Dispose();
            TryDelete(full);
            throw;
        }
    }

    /// <summary>Atomically replaces one private UTF-8 text file.</summary>
    public static void WriteAllText(
        string path,
        string contents,
        bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(contents);
        string full = Path.GetFullPath(path);
        string directory = EnsureDirectory(Path.GetDirectoryName(full)!);
        string temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.private");
        try
        {
            using (FileStream stream = CreateFile(temporary))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096,
                       leaveOpen: false))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            // Creation established the private ACL. Verify it before the sole commit point.
            ValidateFile(temporary);
            RejectLinkedComponents(full, includeLeaf: true);
            File.Move(temporary, full, overwrite);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>Reads a private UTF-8 file after validating its path and permissions.</summary>
    public static string ReadAllText(string path)
    {
        string full = Path.GetFullPath(path);
        ValidateFile(full);
        return File.ReadAllText(full, Encoding.UTF8);
    }

    /// <summary>
    /// Appends one line to a private bounded log. When the current file is
    /// over the limit it is atomically replaced before the append.
    /// </summary>
    public static void AppendLine(string path, string line, long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        string full = Path.GetFullPath(path);
        EnsureDirectory(Path.GetDirectoryName(full)!);
        if (!File.Exists(full))
        {
            using FileStream created = CreateFile(full);
        }
        else
        {
            ValidateFile(full);
        }

        if (new FileInfo(full).Length > maximumBytes)
        {
            WriteAllText(full, string.Empty);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
        using var stream = new FileStream(
            full,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        ValidateFile(full);
    }

    /// <summary>Validates that a directory is private and not a link.</summary>
    public static void ValidateDirectory(string path) =>
        Validate(path, isDirectory: true);

    /// <summary>Validates that a file is private and not a link.</summary>
    public static void ValidateFile(string path) =>
        Validate(path, isDirectory: false);

    /// <summary>
    /// Verifies that a Unix filesystem entry is owned by the effective user.
    /// The lookup does not follow the leaf symbolic link.
    /// </summary>
    public static void ValidateUnixOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Unix owner validation is not available on Windows.");
        }

        string full = Path.GetFullPath(path);
        uint actual = ReadUnixOwner(full);
        uint expected = GetEffectiveUserId();
        if (actual != expected)
        {
            throw new UnauthorizedAccessException(
                $"Private storage is not owned by the current user: {full}");
        }
    }

    /// <summary>
    /// Applies the private file ACL/mode and verifies it. The caller must have
    /// created the file inside an already validated private directory.
    /// </summary>
    public static void ProtectFile(string path)
    {
        string full = Path.GetFullPath(path);
        PrivateUserStorage.ValidateDirectory(Path.GetDirectoryName(full)!);
        RejectLinkedComponents(full, includeLeaf: true);
        Harden(full, isDirectory: false);
        ValidateFile(full);
    }

    /// <summary>
    /// Hardens and validates every existing directory and file below a
    /// private root. Linked entries are rejected rather than traversed.
    /// </summary>
    public static void ProtectTree(string root)
    {
        string full = Path.GetFullPath(root);
        Harden(full, isDirectory: true);
        ValidateDirectory(full);
        foreach (string entry in Directory.EnumerateFileSystemEntries(full))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"Private storage refuses linked content: {entry}");
            }

            if (Directory.Exists(entry))
            {
                ProtectTree(entry);
            }
            else
            {
                ProtectFile(entry);
            }
        }
    }

    /// <summary>
    /// Deletes a bounded private tree without following links. Every file is
    /// removed only while it still matches the physical object that was
    /// admitted for cleanup; unknown or concurrently changed content is kept.
    /// </summary>
    public static bool TryDeleteTree(string root)
    {
        string full = Path.GetFullPath(root);
        FileAttributes? rootAttributes =
            FilePublicationOwnedDelete.TryGetAttributesNoFollow(full);
        if (rootAttributes is null)
        {
            return true;
        }
        if (!rootAttributes.Value.HasFlag(FileAttributes.Directory)
            || rootAttributes.Value.HasFlag(FileAttributes.ReparsePoint)
            || !Directory.Exists(full))
        {
            return false;
        }

        try
        {
            var pending = new Stack<string>();
            var directories = new List<(
                string Path,
                FilePhysicalIdentity? Identity)>();
            pending.Push(full);
            int entries = 0;
            while (pending.TryPop(out string? directory))
            {
                FilePhysicalIdentity? identity =
                    FilePublicationOwnedDelete.TryGetDirectoryIdentity(
                        directory);
                ValidateDirectory(directory);
                if (OperatingSystem.IsWindows()
                    && (identity is null
                        || FilePublicationOwnedDelete.TryGetDirectoryIdentity(
                            directory) != identity))
                {
                    return false;
                }
                directories.Add((directory, identity));
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > MaximumCleanupEntries)
                    {
                        return false;
                    }
                    if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }
                    if (Directory.Exists(entry))
                    {
                        pending.Push(entry);
                        continue;
                    }

                    ValidateFile(entry);
                    FilePublicationSnapshot snapshot =
                        FilePublicationSnapshot.Capture(entry);
                    if (!FilePublicationOwnedDelete.TryDelete(entry, snapshot))
                    {
                        return false;
                    }
                }
            }

            foreach ((string directory, FilePhysicalIdentity? identity)
                     in directories.OrderByDescending(
                         static item => item.Path.Length))
            {
                if (!FilePublicationOwnedDelete.TryDeleteDirectory(
                        directory,
                        identity))
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
            return FilePublicationOwnedDelete.TryGetAttributesNoFollow(full)
                is null;
        }
    }

    private static void Validate(string path, bool isDirectory)
    {
        string full = Path.GetFullPath(path);
        bool exists = isDirectory
            ? Directory.Exists(full)
            : File.Exists(full);
        if (!exists)
        {
            if (isDirectory)
            {
                throw new DirectoryNotFoundException(full);
            }
            throw new FileNotFoundException(
                "Private file not found.",
                full);
        }

        RejectLinkedComponents(full, includeLeaf: true);
        if (OperatingSystem.IsWindows())
        {
            ValidateWindowsAcl(full, isDirectory);
            return;
        }

        UnixFileMode expected = isDirectory
            ? PrivateDirectoryMode
            : PrivateFileMode;
        UnixFileMode actual = File.GetUnixFileMode(full);
        if ((actual & ~expected) != 0
            || (actual & expected) != expected)
        {
            string mode = isDirectory ? "0700" : "0600";
            throw new UnauthorizedAccessException(
                $"Private storage permissions are not {mode}: {full}");
        }
        ValidateUnixOwner(full);
    }

    private static void Harden(string path, bool isDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            if (isDirectory)
            {
                SetWindowsDirectoryAcl(path);
            }
            else
            {
                SetWindowsFileAcl(path);
            }
        }
        else
        {
            File.SetUnixFileMode(
                path,
                isDirectory
                    ? PrivateDirectoryMode
                    : PrivateFileMode);
        }
    }

    internal static void RejectLinkedComponents(string path, bool includeLeaf)
    {
        string full = Path.GetFullPath(path);
        string? current = includeLeaf ? full : Path.GetDirectoryName(full);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"Private storage refuses symbolic links and reparse points: {current}");
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CreateWindowsDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        directory.Create(BuildWindowsDirectorySecurity());
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsDirectoryAcl(string path) =>
        new DirectoryInfo(path).SetAccessControl(BuildWindowsDirectorySecurity());

    [SupportedOSPlatform("windows")]
    private static void SetWindowsFileAcl(string path)
    {
        FileSecurity security = BuildWindowsFileSecurity();
        new FileInfo(path).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static DirectorySecurity BuildWindowsDirectorySecurity()
    {
        SecurityIdentifier current = CurrentUserSid();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(current);
        security.AddAccessRule(new FileSystemAccessRule(
            current,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        return security;
    }

    [SupportedOSPlatform("windows")]
    private static FileSecurity BuildWindowsFileSecurity()
    {
        SecurityIdentifier current = CurrentUserSid();
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(current);
        security.AddAccessRule(new FileSystemAccessRule(
            current,
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        return security;
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsAcl(string path, bool isDirectory)
    {
        FileSystemSecurity security = isDirectory
            ? new DirectoryInfo(path).GetAccessControl(
                AccessControlSections.Access | AccessControlSections.Owner)
            : new FileInfo(path).GetAccessControl(
                AccessControlSections.Access | AccessControlSections.Owner);
        SecurityIdentifier current = CurrentUserSid();
        SecurityIdentifier system =
            new(WellKnownSidType.LocalSystemSid, null);
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner
            || !owner.Equals(current))
        {
            throw new UnauthorizedAccessException(
                $"Private storage is not owned by the current user: {path}");
        }

        AuthorizationRuleCollection rules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: true,
            targetType: typeof(SecurityIdentifier));
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType == AccessControlType.Allow
                && rule.IdentityReference is SecurityIdentifier sid
                && !sid.Equals(current)
                && !sid.Equals(system))
            {
                throw new UnauthorizedAccessException(
                    $"Private storage grants access to another principal: {path}");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier CurrentUserSid() =>
        WindowsIdentity.GetCurrent().User
        ?? throw new UnauthorizedAccessException(
            "The current Windows user SID is unavailable.");

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();

    private static uint ReadUnixOwner(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            const int AtFileDescriptorCurrentWorkingDirectory = -100;
            const int AtSymbolicLinkNoFollow = 0x100;
            const uint StatxUserId = 0x0000_0008;
            int result = LinuxStatx(
                AtFileDescriptorCurrentWorkingDirectory,
                path,
                AtSymbolicLinkNoFollow,
                StatxUserId,
                out LinuxStatxBuffer buffer);
            if (result != 0 || (buffer.Mask & StatxUserId) == 0)
            {
                throw new UnauthorizedAccessException(
                    $"Private storage owner could not be verified: {path}");
            }

            return buffer.UserId;
        }

        if (OperatingSystem.IsMacOS())
        {
            if (MacLStat(path, out MacStatBuffer buffer) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"Private storage owner could not be verified: {path}");
            }

            return buffer.UserId;
        }

        throw new PlatformNotSupportedException(
            "Current-user private storage requires an owner-verification adapter.");
    }

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int LinuxStatx(
        int directoryFileDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        uint mask,
        out LinuxStatxBuffer buffer);

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(20)]
        public uint UserId;
    }

    [DllImport("libc", EntryPoint = "lstat$INODE64", SetLastError = true)]
    private static extern int MacLStat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStatBuffer buffer);

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct MacStatBuffer
    {
        [FieldOffset(16)]
        public uint UserId;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning(
                "Private temporary file cleanup failed: {0}.", exception.GetType().Name);
        }
    }
}
