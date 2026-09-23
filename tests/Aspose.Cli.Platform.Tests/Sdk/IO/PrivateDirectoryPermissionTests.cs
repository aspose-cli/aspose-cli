using System.Security.AccessControl;
using System.Security.Principal;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class PrivateDirectoryPermissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnsureDirectory_NormalizesPrivateButIncompleteWindowsPermissions(bool inherit)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var temp = new TempDirectory();
        string path = PrivateUserStorage.EnsureDirectory(temp.File("private"));
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        var security = new DirectorySecurity();
        security.SetOwner(user);
        security.SetAccessRuleProtection(true, false);
        // Keep permission-management rights so normalization is authorized while file creation is still denied.
        FileSystemRights userRights = inherit
            ? FileSystemRights.ReadAndExecute | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership
            : FileSystemRights.FullControl;
        security.AddAccessRule(new FileSystemAccessRule(user, userRights,
            inherit ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
        PrivateUserStorage.ValidateDirectory(path); // Private is weaker than normalized, inheritable full control.
        Assert.Equal(path, PrivateUserStorage.EnsureDirectory(path));
        DirectorySecurity actual = new DirectoryInfo(path).GetAccessControl();
        Assert.True(actual.AreAccessRulesProtected);
        foreach (FileSystemAccessRule rule in actual.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
            Assert.Equal(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, rule.InheritanceFlags);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        }
        using FileStream created = PrivateUserStorage.CreateFile(Path.Combine(path, "writable.txt"));
        created.WriteByte(1);
    }

    [Fact]
    public void EnsureDirectory_NormalizesRestrictiveUnixOwnerPermissions()
    {
        if (OperatingSystem.IsWindows()) { return; }
        using var temp = new TempDirectory();
        string path = PrivateUserStorage.EnsureDirectory(temp.File("private"));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        PrivateUserStorage.EnsureDirectory(path);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(path));
    }

    [Fact]
    public void EnsureDirectory_RejectsAnExistingDirectoryLink()
    {
        using var temp = new TempDirectory();
        string actual = PrivateUserStorage.EnsureDirectory(temp.File("actual"));
        string link = temp.File("link");
        try { Directory.CreateSymbolicLink(link, actual); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { return; }
        Assert.Throws<UnauthorizedAccessException>(() => PrivateUserStorage.EnsureDirectory(link));
        Assert.True(Directory.Exists(actual));
    }
}