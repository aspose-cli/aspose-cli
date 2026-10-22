using System.Security.AccessControl;
using System.Security.Principal;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>
/// The one owner of current-user-only access: a protected access list, owned by the current user,
/// that allows only the current user and LocalSystem and inherits nothing from its folder. Every
/// file that holds a secret (a license, a local service's control secret) is created with it,
/// so the secret is never readable through its folder's rules, not even before its bytes are
/// written. A staged output created with it keeps it when it is published
/// (<see cref="IsCurrentUserOnly"/>), whatever rules the folder or a replaced file had.
/// </summary>
public static class CurrentUserFileAccess
{
    /// <summary>Creates the new file <paramref name="path"/> with current-user-only access.</summary>
    /// <param name="path">The file to create.</param>
    /// <param name="options">The file options.</param>
    /// <exception cref="IOException">The file already exists.</exception>
    public static FileStream CreateNew(string path, FileOptions options = FileOptions.None) =>
        new FileInfo(path).Create(
            FileMode.CreateNew,
            FileSystemRights.Read | FileSystemRights.Write,
            FileShare.None,
            bufferSize: 4096,
            options,
            Rules<FileSecurity>(InheritanceFlags.None));

    /// <summary>
    /// Writes <paramref name="contents"/> to <paramref name="path"/> with current-user-only
    /// access. A file that does not exist is created with it; an existing one, such as the empty
    /// file a publication stages, is given it before any byte is written.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="contents">The bytes to write.</param>
    public static void Write(string path, ReadOnlySpan<byte> contents)
    {
        FileStream stream;
        var file = new FileInfo(path);
        if (file.Exists)
        {
            Restrict(path);
            stream = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None);
        }
        else
        {
            stream = CreateNew(path);
        }

        using (stream)
        {
            stream.Write(contents);
            stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>Gives an existing file current-user-only access.</summary>
    internal static void Restrict(string path) =>
        new FileInfo(path).SetAccessControl(Rules<FileSecurity>(InheritanceFlags.None));

    /// <summary>
    /// Gives <paramref name="copy"/>, an existing file about to receive the bytes of
    /// <paramref name="original"/>, current-user-only access when the original has it, so a
    /// secret copied from one staging area to another keeps it.
    /// </summary>
    internal static void Mirror(string original, string copy)
    {
        if (IsCurrentUserOnly(original))
        {
            Restrict(copy);
        }
    }

    /// <summary>Whether the file has exactly the rules <see cref="CreateNew"/> gives.</summary>
    internal static bool IsCurrentUserOnly(string path)
    {
        try
        {
            FileSecurity security = new FileInfo(path).GetAccessControl(AccessControlSections.Access);
            using WindowsIdentity current = WindowsIdentity.GetCurrent();
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            return security.AreAccessRulesProtected
                && security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>()
                    .All(rule => rule.AccessControlType == AccessControlType.Allow
                        && (rule.IdentityReference.Equals(current.User) || rule.IdentityReference.Equals(system)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>The same rules for a directory, inherited by everything created inside it.</summary>
    internal static DirectorySecurity DirectoryRules() =>
        Rules<DirectorySecurity>(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit);

    private static T Rules<T>(InheritanceFlags inheritance)
        where T : FileSystemSecurity, new()
    {
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        SecurityIdentifier user = current.User
            ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
        var security = new T();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        foreach (SecurityIdentifier principal in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                principal,
                FileSystemRights.FullControl,
                inheritance,
                PropagationFlags.None,
                AccessControlType.Allow));
        }
        return security;
    }
}
