using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Windows access rules a test gives a folder or expects of a file.</summary>
public static class AccessRules
{
    private static readonly SecurityIdentifier Everyone = new(WellKnownSidType.WorldSid, null);
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>Lets everyone read the directory and everything later created inside it.</summary>
    public static void ShareForReading(string directory)
    {
        DirectoryInfo info = Directory.CreateDirectory(directory);
        DirectorySecurity security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            Everyone,
            FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
    }

    /// <summary>
    /// The file inherits nothing from its folder and allows only the current user and
    /// LocalSystem, the current user included.
    /// </summary>
    public static void AssertCurrentUserOnly(string file)
    {
        FileSecurity security = new FileInfo(file).GetAccessControl();
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        FileSystemAccessRule[] rules = [.. security
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()];
        string listed = string.Join("; ", rules.Select(static rule =>
            $"{rule.AccessControlType} {rule.IdentityReference} {rule.FileSystemRights} inherited={rule.IsInherited}"));

        Assert.True(security.AreAccessRulesProtected, $"'{file}' inherits its folder's rules: {listed}");
        Assert.All(rules, rule => Assert.True(
            rule.AccessControlType == AccessControlType.Allow
                && (rule.IdentityReference.Equals(current.User) || rule.IdentityReference.Equals(LocalSystem)),
            $"'{file}' has a rule for someone else: {listed}"));
        Assert.Contains(rules, rule => rule.IdentityReference.Equals(current.User));
    }
}
