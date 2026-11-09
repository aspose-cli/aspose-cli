using System.Security.AccessControl;
using System.Security.Principal;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Installer.Tests;

/// <summary>Who may change the directory a custom installation lands in.</summary>
public sealed partial class CustomerInstallerPowerShellTests
{
    private const string OtherWritersWarning = "Other accounts can change";

    private static readonly SecurityIdentifier AuthenticatedUsers = new(WellKnownSidType.AuthenticatedUserSid, null);

    private static readonly SecurityIdentifier BuiltinUsers = new(WellKnownSidType.BuiltinUsersSid, null);

    private const FileSystemRights ReadOnlyRights = FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize;

    private static readonly string[] PrivateIdentities =
    [
        WindowsIdentity.GetCurrent().User!.Value,
        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value,
    ];

    // The access rules are read and written through different APIs on the two PowerShell runtimes.
    [Theory]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void NewCustomDirectory_IsOpenOnlyToTheUserSystemAndAdministrators(string shell)
    {
        Requires.Windows();
        string root = DirectoryOutsideLocalApplicationData();
        try
        {
            string grandparent = Path.Combine(root, "new-parent");
            string parent = Path.Combine(grandparent, "nested");
            string install = Path.Combine(parent, "cli");
            string[] existingAncestorRules = RuleList(root);

            PowerShellResult installed = RunInstaller(_package.Path, install, shell: ShellExecutable(shell));

            Assert.True(installed.ExitCode == 0, installed.StdErr + installed.StdOut);
            AssertInstall(install);
            Assert.True(new DirectoryInfo(install).GetAccessControl(AccessControlSections.Access).AreAccessRulesProtected);
            foreach (string path in new[] { install, Path.Combine(install, "aspose-cli.exe") })
            {
                AssertOpenOnlyToTheUserSystemAndAdministrators(path);
            }
            string[] expectedParentRules = NewParentRules();
            Assert.Equal(expectedParentRules, RuleList(parent));
            Assert.Equal(expectedParentRules, RuleList(grandparent));
            Assert.Equal(existingAncestorRules, RuleList(root));
            Assert.DoesNotContain(OtherWritersWarning, Flat(installed), StringComparison.Ordinal);

            // An update publishes a new tree, which keeps these rules without a second change.
            string[] rules = RuleList(install);
            PowerShellResult updated = RunInstaller(_package.Path, install, arguments: ["-Update"],
                skipPath: false, skipSkills: false, shell: ShellExecutable(shell));

            Assert.True(updated.ExitCode == 0, updated.StdErr + updated.StdOut);
            Assert.Equal(rules, RuleList(install));
            AssertOpenOnlyToTheUserSystemAndAdministrators(Path.Combine(install, "aspose-cli.exe"));
            Assert.Equal(expectedParentRules, RuleList(parent));
            Assert.Equal(expectedParentRules, RuleList(grandparent));
            Assert.Equal(existingAncestorRules, RuleList(root));
            Assert.DoesNotContain(OtherWritersWarning, Flat(updated), StringComparison.Ordinal);
            Assert.Empty(Directory.GetDirectories(parent, ".aspose-cli-*"));

            // What is put in a created parent later is readable by other users, as it inherits.
            string later = Path.Combine(parent, "added-later.txt");
            File.WriteAllText(later, "shared");
            FileSystemAccessRule usersRule = Assert.Single(
                Rules(new FileInfo(later).GetAccessControl(AccessControlSections.Access)),
                static rule => rule.IdentityReference.Value == BuiltinUsers.Value);
            Assert.Equal(ReadOnlyRights, usersRule.FileSystemRights);
            Assert.True(usersRule.IsInherited);
        }
        finally { DeleteDirectoryWithRetry(root); }
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void NewInstallUnderAnExistingParent_LeavesTheParentAsItIs(string shell)
    {
        Requires.Windows();
        string root = DirectoryOutsideLocalApplicationData();
        try
        {
            // A folder with a rule of its own, which any rewrite of its rules would lose.
            string parent = Directory.CreateDirectory(Path.Combine(root, "existing-parent")).FullName;
            AllowAuthenticatedUsersToModify(parent);
            string[] rules = RuleList(parent);
            string install = Path.Combine(parent, "cli");

            PowerShellResult installed = RunInstaller(_package.Path, install, shell: ShellExecutable(shell));

            Assert.True(installed.ExitCode == 0, installed.StdErr + installed.StdOut);
            Assert.Equal(rules, RuleList(parent));
            AssertOpenOnlyToTheUserSystemAndAdministrators(install);

            PowerShellResult updated = RunInstaller(_package.Path, install, arguments: ["-Update"],
                skipPath: false, skipSkills: false, shell: ShellExecutable(shell));

            Assert.True(updated.ExitCode == 0, updated.StdErr + updated.StdOut);
            Assert.Equal(rules, RuleList(parent));
        }
        finally { DeleteDirectoryWithRetry(root); }
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void ExistingDirectoryOthersCanWrite_WarnsAndKeepsItsRules(string shell)
    {
        Requires.Windows();
        string root = DirectoryOutsideLocalApplicationData();
        try
        {
            string install = Path.Combine(root, "shared");
            Directory.CreateDirectory(install);
            AllowAuthenticatedUsersToModify(install);
            string[] rules = RuleList(install);
            string account = AuthenticatedUsers.Translate(typeof(NTAccount)).Value;

            PowerShellResult installed = RunInstaller(_package.Path, install, shell: ShellExecutable(shell));

            Assert.True(installed.ExitCode == 0, installed.StdErr + installed.StdOut);
            AssertInstall(install);
            Assert.Contains(OtherWritersWarning, Flat(installed), StringComparison.Ordinal);
            Assert.Contains(account, Flat(installed), StringComparison.Ordinal);
            Assert.Contains("replace aspose-cli.exe", Flat(installed), StringComparison.Ordinal);
            Assert.Contains("Remove their write access", Flat(installed), StringComparison.Ordinal);
            Assert.Equal(rules, RuleList(install));

            // An earlier installation in such a directory updates without an error or a change.
            PowerShellResult updated = RunInstaller(_package.Path, install, arguments: ["-Update"],
                skipPath: false, skipSkills: false, shell: ShellExecutable(shell));

            Assert.True(updated.ExitCode == 0, updated.StdErr + updated.StdOut);
            Assert.Contains(OtherWritersWarning, Flat(updated), StringComparison.Ordinal);
            Assert.Equal(rules, RuleList(install));
        }
        finally { DeleteDirectoryWithRetry(root); }
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void ExistingPrivateDirectory_KeepsItsRulesWithoutAWarning(string shell)
    {
        Requires.Windows();
        string root = DirectoryOutsideLocalApplicationData();
        try
        {
            string install = Path.Combine(root, "already-private");
            Directory.CreateDirectory(install);
            MakePrivate(install);
            string[] rules = RuleList(install);

            PowerShellResult installed = RunInstaller(_package.Path, install, shell: ShellExecutable(shell));

            Assert.True(installed.ExitCode == 0, installed.StdErr + installed.StdOut);
            Assert.Equal(rules, RuleList(install));
            Assert.DoesNotContain(OtherWritersWarning, Flat(installed), StringComparison.Ordinal);
        }
        finally { DeleteDirectoryWithRetry(root); }
    }

    [Fact]
    public void DefaultLocation_KeepsTheRulesOfItsFolderWithoutAWarning()
    {
        Requires.Windows();
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "aspose-installer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string install = Path.Combine(root, "cli");
            string sibling = Directory.CreateDirectory(Path.Combine(root, "sibling")).FullName;

            PowerShellResult installed = RunInstaller(_package.Path, install);

            Assert.True(installed.ExitCode == 0, installed.StdErr + installed.StdOut);
            Assert.Equal(RuleList(sibling), RuleList(install));
            Assert.DoesNotContain(OtherWritersWarning, Flat(installed), StringComparison.Ordinal);
        }
        finally { DeleteDirectoryWithRetry(root); }
    }

    private static string ShellExecutable(string shell) =>
        shell == "pwsh" ? ToolPath.Require("pwsh") : "powershell.exe";

    /// <summary>A new directory the installer treats as custom: anything below the local application data folder is its default location's.</summary>
    private static string DirectoryOutsideLocalApplicationData() =>
        Directory.CreateDirectory(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "aspose-installer-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    private static void AllowAuthenticatedUsersToModify(string directory)
    {
        var info = new DirectoryInfo(directory);
        DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);
        security.AddAccessRule(new FileSystemAccessRule(
            AuthenticatedUsers,
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
    }

    private static void MakePrivate(string directory)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (string identity in PrivateIdentities)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(identity),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }
        new DirectoryInfo(directory).SetAccessControl(security);
    }

    /// <summary>The rules of a parent folder the installer creates: other users read and nobody else writes.</summary>
    private static string[] NewParentRules() =>
        PrivateIdentities.Select(static identity => RuleText(identity, FileSystemRights.FullControl))
            .Append(RuleText(BuiltinUsers.Value, ReadOnlyRights))
            .Order(StringComparer.Ordinal)
            .Prepend("protected=True")
            .ToArray();

    private static string RuleText(string identity, FileSystemRights rights) =>
        $"{AccessControlType.Allow} {identity} {(int)rights:X8} {InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit} {PropagationFlags.None} inherited=False";

    private static string[] RuleList(string directory)
    {
        DirectorySecurity security = new DirectoryInfo(directory).GetAccessControl(AccessControlSections.Access);
        return Rules(security)
            .Select(static rule => $"{rule.AccessControlType} {rule.IdentityReference.Value} {(int)rule.FileSystemRights:X8} {rule.InheritanceFlags} {rule.PropagationFlags} inherited={rule.IsInherited}")
            .Order(StringComparer.Ordinal)
            .Prepend($"protected={security.AreAccessRulesProtected}")
            .ToArray();
    }

    private static void AssertOpenOnlyToTheUserSystemAndAdministrators(string path)
    {
        FileSystemSecurity security = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
            : new FileInfo(path).GetAccessControl(AccessControlSections.Access);
        Assert.Equal(
            PrivateIdentities.Order(StringComparer.Ordinal),
            Rules(security).Select(static rule => rule.IdentityReference.Value).Distinct().Order(StringComparer.Ordinal));
    }

    private static IEnumerable<FileSystemAccessRule> Rules(FileSystemSecurity security) =>
        security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>();
}
