using System.Security.AccessControl;
using System.Security.Principal;
using Aspose.Cli.Sdk.IO.Files;

namespace Aspose.Cli.Sdk.IO.Publication;

internal enum PublicationTransactionState
{
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
    Publish,
    Access,
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
/// Creates a publication transaction directory that only the current user and LocalSystem can
/// open, with the current user as its owner. It holds the staged outputs and the originals they
/// replace, so nobody else may read or write there even when the output folder itself is
/// shared. The access rules are set once at creation and never re-validated; a volume or server
/// that cannot store them keeps the folder's own rules.
/// </summary>
internal static class PublicationTransactionDirectory
{
    public static string Create(string path)
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
        return Directory.CreateDirectory(path).FullName;
    }

    private static DirectoryInfo CreateWindows(string path)
    {
        var directory = new DirectoryInfo(path);
        directory.Create(CurrentUserFileAccess.DirectoryRules());
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
        try
        {
            return CaptureAccess(existingTarget);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads the access rules of a file, as <see cref="TryCaptureAccess"/> does, or throws.</summary>
    public static byte[] CaptureAccess(string existingFile) =>
        new FileInfo(existingFile).GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorBinaryForm();

    /// <summary>
    /// Applies rules captured by <see cref="TryCaptureAccess"/> to the published file. Applied
    /// in place, so Windows recomputes the inherited rules from the real folder and keeps the
    /// explicit ones.
    /// </summary>
    public static void TryApplyAccess(string published, byte[]? access)
    {
        if (access is null) { return; }
        try
        {
            ApplyAccess(published, access);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning(
                "A published replacement kept its staging access rules ({0}).", exception.GetType().Name);
        }
    }

    /// <summary>Gives a file exactly the captured rules, as <see cref="TryApplyAccess"/> does, or throws.</summary>
    public static void ApplyAccess(string path, byte[] access)
    {
        var security = new FileSecurity();
        security.SetSecurityDescriptorBinaryForm(access, AccessControlSections.Access);
        new FileInfo(path).SetAccessControl(security);
    }

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

/// <summary>One staged output, what its publication requires and how far it has gone.</summary>
internal sealed record PublicationEntry
{
    public required int Index { get; init; }

    public required string Target { get; init; }

    public required string Staged { get; init; }

    public required bool Overwrite { get; init; }

    public bool DeleteTarget { get; init; }

    public required FilePublicationSnapshot Original { get; init; }

    public required FilePublicationSnapshot StagedSnapshot { get; init; }

    public string? InputPath { get; init; }

    public FilePublicationSnapshot? InputSnapshot { get; init; }

    public FilePhysicalIdentity? TargetParentIdentity { get; init; }

    public string? Displaced { get; init; }

    public FilePublicationSnapshot? DisplacedSnapshot { get; set; }

    public FilePublicationSnapshot? PublishedSnapshot { get; set; }

    public PublicationEntryState State { get; set; } = PublicationEntryState.Staged;

    public long Size { get; set; }
}

/// <summary>
/// What a rollback did to one target. <see cref="OriginalPath"/> is where the original of a
/// target that was not put back now is, when the publication moved it aside.
/// </summary>
internal sealed record PublicationRecoveryItem(
    string Target,
    bool OriginalExisted,
    bool Published,
    string Status,
    bool ContentVerified,
    bool MetadataVerified,
    string? Failure,
    string? OriginalPath = null);

internal sealed record PublicationRecoveryReport(
    bool RecoveryComplete,
    IReadOnlyList<PublicationRecoveryItem> Items);
