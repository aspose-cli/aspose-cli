using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class AtomicOutputSetWriterTests
{
    [Fact]
    public void DuplicateTargetsAreRejectedBeforePublication()
    {
        using var temp = new TempDirectory();
        string target = temp.File("same.txt");
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "first"));

        CliException exception = Assert.Throws<CliException>(() =>
            set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "second")));

        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void Commit_RejectsAStageReplacedAfterStaging()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "stage-identity");
        set.Stage(
            target,
            overwrite: true,
            staged => File.WriteAllText(staged, "verified"));
        string transaction = Assert.Single(Directory.GetDirectories(temp.Path));
        string staged = AtomicPublicationPlan.StagedPath(
            transaction,
            target,
            index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.Delete(staged);
        File.WriteAllText(staged, "replacement");

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.Equal("original", File.ReadAllText(target));
        // The swap refused the stage before it touched the target, so nothing was published.
        JsonObject state = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(error.Details!["targets"])));
        Assert.Equal("unchanged", state["status"]!.GetValue<string>());
        Assert.False(state["published"]!.GetValue<bool>());
    }

    [Fact]
    public void Commit_ReportsANewTargetWhoseStageWasReplacedAsNotPublished()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "stage-identity");
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "verified"));
        string transaction = Assert.Single(Directory.GetDirectories(temp.Path));
        string staged = AtomicPublicationPlan.StagedPath(transaction, target, index: 0);
        File.Delete(staged);
        File.WriteAllText(staged, "replacement");

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.False(File.Exists(target));
        JsonObject state = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(error.Details!["targets"])));
        Assert.Equal("unchanged", state["status"]!.GetValue<string>());
        Assert.False(state["published"]!.GetValue<bool>());
    }

    [Fact]
    public void CommitFailureRestoresEarlierOverwrittenTargets()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish
                && point.EntryIndex == 1
                    ? new IOException("publish fault")
                    : null);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.Equal(ExitCode.OutputError, error.ExitCode);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original", File.ReadAllText(first));
    }

    [Fact]
    public void CommitFailureRestoresAnEarlierDeletion()
    {
        using var temp = new TempDirectory();
        string deleted = temp.File("deleted.txt");
        string replaced = temp.File("replaced.txt");
        File.WriteAllText(deleted, "original-deleted");
        File.WriteAllText(replaced, "original-replaced");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish
                && point.EntryIndex == 1
                    ? new IOException("publish fault")
                    : null);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.StageDeletionPrepared(
            deleted,
            FilePublicationSnapshot.Capture(deleted));
        set.Stage(
            replaced,
            overwrite: true,
            staged => File.WriteAllText(staged, "replacement"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original-deleted", File.ReadAllText(deleted));
        Assert.Equal("original-replaced", File.ReadAllText(replaced));
    }

    [Fact]
    public void DeletionAndReplacementCommitAsOneOutputSet()
    {
        using var temp = new TempDirectory();
        string deleted = temp.File("deleted.txt");
        string replaced = temp.File("replaced.txt");
        File.WriteAllText(deleted, "obsolete");
        File.WriteAllText(replaced, "original");
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test");
        set.StageDeletionPrepared(
            deleted,
            FilePublicationSnapshot.Capture(deleted));
        set.Stage(
            replaced,
            overwrite: true,
            staged => File.WriteAllText(staged, "replacement"));

        _ = set.Commit();

        Assert.False(File.Exists(deleted));
        Assert.Equal("replacement", File.ReadAllText(replaced));
    }

    [Fact]
    public void OneJournalRecoversTargetsInDifferentDescendantDirectories()
    {
        using var temp = new TempDirectory();
        string first = temp.File(Path.Combine("a", "first.txt"));
        string second = temp.File(Path.Combine("b", "second.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish
                && point.EntryIndex == 1
                    ? new IOException("publish fault")
                    : null);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(first, overwrite: true, staged =>
            File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged =>
            File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original-one", File.ReadAllText(first));
        Assert.Equal("original-two", File.ReadAllText(second));
        Assert.Empty(Directory.EnumerateDirectories(
            temp.Path,
            ".aspose-publication-*"));
    }

    [Theory]
    [InlineData((int)PublicationFaultKind.Rollback)]
    [InlineData((int)PublicationFaultKind.Metadata)]
    public void RecoveryFailureIsReturnedAsPartialWithPerTargetEvidence(
        int recoveryFaultValue)
    {
        var recoveryFault = (PublicationFaultKind)recoveryFaultValue;
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1
                ? new IOException("publish fault")
                : point.Kind == recoveryFault && point.EntryIndex == 0
                    ? new IOException("rollback fault")
                    : null);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.Equal(ExitCode.OutputError, error.ExitCode);
        Assert.False(error.Details!["recoveryComplete"]!.GetValue<bool>());
        JsonArray targets = Assert.IsType<JsonArray>(error.Details["targets"]);
        JsonObject firstState = Assert.IsType<JsonObject>(targets[0]);
        Assert.Equal("unknown", firstState["status"]!.GetValue<string>());
        Assert.False(firstState["contentVerified"]!.GetValue<bool>());
        Assert.Equal("original-one", File.ReadAllText(firstState["originalPath"]!.GetValue<string>()));
        Assert.Null(Assert.IsType<JsonObject>(targets[1])["originalPath"]);
        Assert.Equal("replacement-one", File.ReadAllText(first));
        Assert.Equal("original-two", File.ReadAllText(second));
        Assert.Single(Directory.EnumerateDirectories(temp.Path, ".aspose-*"));
    }

    [Fact]
    public void PartialRecoveryKeepsTheDisplacedOriginalForManualRestore()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1
                ? new IOException("publish fault")
                : point.Kind == PublicationFaultKind.Rollback && point.EntryIndex == 0
                    ? new IOException("rollback fault")
                    : null);
        CliException error;
        using (var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test", faults))
        {
            set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement-one"));
            set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));
            error = Assert.Throws<CliException>(() => set.Commit());
            Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        }

        string transaction = Assert.Single(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
        string displaced = Path.Combine(transaction, "backups", "000001.displaced");
        Assert.Equal(displaced, error.Details!["targets"]![0]!["originalPath"]!.GetValue<string>());
        Assert.Equal("original-one", File.ReadAllText(displaced));
        Assert.Empty(Directory.EnumerateFiles(transaction, "*.backup", SearchOption.AllDirectories));
        File.Copy(displaced, first, overwrite: true);
        Assert.Equal("original-one", File.ReadAllText(first));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RollbackKeepsATargetChangedAfterItsSwap(bool existed)
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        if (existed) { File.WriteAllText(first, "original-one"); }
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind != PublicationFaultKind.Publish || point.EntryIndex != 1) { return null; }
            File.WriteAllText(first, "changed by someone else");
            return new IOException("publish fault");
        });
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test", faults);
        set.Stage(first, overwrite: existed, staged => File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.Equal("changed by someone else", File.ReadAllText(first));
        Assert.Equal("original-two", File.ReadAllText(second));
        JsonNode? originalPath = error.Details!["targets"]![0]!["originalPath"];
        if (existed) { Assert.Equal("original-one", File.ReadAllText(originalPath!.GetValue<string>())); }
        else { Assert.Null(originalPath); }
    }

    [Fact]
    public void RollbackKeepsAnotherFileOfTheSameContentThatTookAPublishedTargetsPlace()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        FilePublicationSnapshot? replacement = null;
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind != PublicationFaultKind.Publish || point.EntryIndex != 1) { return null; }
            string other = temp.File("other.txt");
            File.WriteAllText(other, "replacement-one");
            replacement = FilePublicationSnapshot.Capture(other);
            File.Move(other, first, overwrite: true);
            return new IOException("publish fault");
        });
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test", faults);
        set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.True(replacement!.VersionEquals(FilePublicationSnapshot.Capture(first)));
        Assert.Equal("original-one", File.ReadAllText(error.Details!["targets"]![0]!["originalPath"]!.GetValue<string>()));
        Assert.Equal("original-two", File.ReadAllText(second));
    }

    [Fact]
    public void InPlacePublicationKeepsNoInternalBackup()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        string[]? transactionFiles = null;
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                string transaction = Assert.Single(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
                transactionFiles = Directory.GetFiles(transaction, "*", SearchOption.AllDirectories);
            }
            return null;
        });
        using (var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test", faults))
        {
            set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "replacement"));
            set.Commit();
        }

        Assert.Equal("replacement", File.ReadAllText(target));
        Assert.NotNull(transactionFiles);
        Assert.DoesNotContain(transactionFiles, path => path.EndsWith(".backup", StringComparison.Ordinal));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void UnexpectedRecoveryExceptionStillUsesPublicationErrorContract()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(point =>
            point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1
                ? new IOException("publish fault")
                : point.Kind == PublicationFaultKind.Rollback
                    ? new InvalidOperationException("unexpected recovery fault")
                    : null);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(first, overwrite: true, staged =>
            File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged =>
            File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.False(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original-one", File.ReadAllText(error.Details["targets"]![0]!["originalPath"]!.GetValue<string>()));
    }

    [Fact]
    public void PartialRecoveryEvidenceSurvivesDisposal()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1
                ? new IOException("publish fault")
                : point.Kind == PublicationFaultKind.Rollback && point.EntryIndex == 0
                    ? new IOException("rollback fault")
                    : null);
        var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());
        set.Dispose();

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        string transaction = Assert.Single(
            Directory.EnumerateDirectories(
                temp.Path,
                ".aspose-publication-*"));
        string original = error.Details!["targets"]![0]!["originalPath"]!.GetValue<string>();
        Assert.StartsWith(transaction + Path.DirectorySeparatorChar, original, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("original-one", File.ReadAllText(original));
    }

    [Fact]
    public void TargetReplacedRightAfterItsSwapFailsThePublicationAndKeepsTheReplacement()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind == PublicationFaultKind.Access)
            {
                string external = temp.File("external.txt");
                File.WriteAllText(external, "external");
                File.Move(external, target, overwrite: true);
            }
            return null;
        });
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test", faults);
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "replacement"));

        Assert.Throws<CliException>(() => set.Commit());

        Assert.Single(Directory.EnumerateFiles(temp.Path, "*", SearchOption.AllDirectories),
            path => File.ReadAllText(path) == "external");
        Assert.NotEqual("replacement", File.ReadAllText(target));
    }

    [Fact]
    public void CleanupFailureDoesNotReverseACommittedPublication()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Cleanup
                ? new IOException("cleanup fault")
                : null);
        var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "content"));
        set.Commit();

        set.Dispose();

        Assert.Equal("content", File.ReadAllText(target));
        Assert.Single(Directory.EnumerateDirectories(temp.Path, ".aspose-*"));
    }

    [Fact]
    public void ExternalModificationBeforeCommitIsNotOverwritten()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "replacement"));
        File.WriteAllText(target, "external-update");

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputConflict, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("external-update", File.ReadAllText(target));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void TargetHeldOpenByAnotherApplicationIsUnwritableAndUntouched()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "replacement"));

        CliException error;
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            error = Assert.Throws<CliException>(() => set.Commit());
        }

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original", File.ReadAllText(target));
        Assert.Equal(["target.txt"], Directory.EnumerateFileSystemEntries(temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void SuccessfulOverwritePreservesPortableMetadata()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.Hidden);
        }
        else
        {
            File.SetUnixFileMode(
                target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "replacement"));
        set.Commit();

        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.GetAttributes(target).HasFlag(FileAttributes.Hidden));
        }
        else
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(target));
        }
    }

    [Fact]
    public void SealingRejectsAndPreservesUnknownTransactionArtifacts()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction;
        string unknown;
        var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test");
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "content"));
        transaction = Assert.Single(
            Directory.EnumerateDirectories(temp.Path, ".aspose-*"));
        unknown = Path.Combine(transaction, "external-sentinel.txt");
        File.WriteAllText(unknown, "external");
        Assert.Throws<CliException>(() => set.Commit());

        set.Dispose();

        Assert.False(File.Exists(target));
        Assert.Equal("external", File.ReadAllText(unknown));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void TransactionDirectoryAdmitsOnlyTheCurrentUserAndLocalSystem()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string shared = Path.Combine(temp.Path, "shared");
        Directory.CreateDirectory(shared);
        var sharedSecurity = new DirectoryInfo(shared).GetAccessControl();
        sharedSecurity.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(shared).SetAccessControl(sharedSecurity);

        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), shared, "test");
        set.Stage(Path.Combine(shared, "out.txt"), overwrite: false, staged => File.WriteAllText(staged, "x"));
        string transaction = Assert.Single(Directory.GetDirectories(shared, ".aspose-publication-*"));

        var security = new DirectoryInfo(transaction).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        Assert.Equal(user, security.GetOwner(typeof(SecurityIdentifier)));
        Assert.True(security.AreAccessRulesProtected);
        var principals = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Select(rule => (SecurityIdentifier)rule.IdentityReference)
            .ToHashSet();
        Assert.Equal(
            new HashSet<SecurityIdentifier> { user, new(WellKnownSidType.LocalSystemSid, null) },
            principals);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void RollbackRestoresContentAndKeepsATargetAclThatDiffersFromItsFolder()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original");
        File.WriteAllText(second, "original-two");
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        new FileInfo(first).SetAccessControl(security);
        string expected = AccessSddl(first);
        Assert.NotEqual(AccessSddl(second), expected);
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1
                ? new IOException("publish fault")
                : null);
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test", faults);
        set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original", File.ReadAllText(first));
        Assert.Equal(expected, AccessSddl(first));
    }

    [SupportedOSPlatform("windows")]
    private static string AccessSddl(string path) =>
        new FileInfo(path).GetAccessControl(AccessControlSections.Access)
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);

    [Fact]
    public async Task CommitAndDisposeMayRunOnDifferentThreads()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "cross-thread");
        set.Stage(first, overwrite: false, staged => File.WriteAllText(staged, "first"));

        await Task.Run(() => set.Commit());
        set.Dispose();
        using (var next = new AtomicOutputSetWriter(
                   TestBudgets.Writer(),
                   temp.Path,
                   "next"))
        {
            next.Stage(second, overwrite: false, staged => File.WriteAllText(staged, "second"));
            next.Commit();
        }

        Assert.Equal("first", File.ReadAllText(first));
        Assert.Equal("second", File.ReadAllText(second));
    }

    [Fact]
    public void AtomicPublish_RestoresTheOriginalWhenStageChangesImmediatelyBeforeSwap()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        string? transaction = null;
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                ReplaceFile(AtomicPublicationPlan.StagedPath(transaction!, target, index: 0), "replacement");
            }
            return null;
        });
        var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "stage-before-swap", faults);
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "verified"));
        transaction = Assert.Single(Directory.GetDirectories(temp.Path));

        CliException error = Assert.Throws<CliException>(() => set.Commit());
        set.Dispose();

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        AssertSwapNeverStarted(error);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.True(original.VersionEquals(FilePublicationSnapshot.Capture(target)));
        Assert.Equal("replacement", File.ReadAllText(AtomicPublicationPlan.StagedPath(transaction, target, index: 0)));
        AssertNoSwapResidue(temp.Path);
    }

    [Fact]
    public void AtomicPublish_RejectsAStageReplacedAfterVerification()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string? transaction = null;
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                ReplaceFile(AtomicPublicationPlan.StagedPath(transaction!, target, index: 0), "replacement");
            }
            return null;
        });
        var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "new-stage-before-swap", faults);
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "verified"));
        transaction = Assert.Single(Directory.GetDirectories(temp.Path));

        CliException error = Assert.Throws<CliException>(() => set.Commit());
        set.Dispose();

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        AssertSwapNeverStarted(error);
        Assert.False(File.Exists(target));
        Assert.Equal("replacement", File.ReadAllText(AtomicPublicationPlan.StagedPath(transaction, target, index: 0)));
        AssertNoSwapResidue(temp.Path);
    }

    [Fact]
    public void AtomicSwapRestoresSameContentExternalReplacementByIdentity()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "same-content");
        FilePublicationSnapshot? external = null;
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                ReplaceFile(target, "same-content");
                external = FilePublicationSnapshot.Capture(target);
            }
            return null;
        });
        var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "external-before-swap", faults);
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "published-content"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());
        set.Dispose();

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.True(external!.VersionEquals(FilePublicationSnapshot.Capture(target)));
        Assert.Equal("same-content", File.ReadAllText(target));
        AssertNoSwapResidue(temp.Path);
    }

    private static void ReplaceFile(string path, string content)
    {
        string replacement = path + ".external";
        File.WriteAllText(replacement, content);
        File.Move(replacement, path, overwrite: true);
    }

    private static void AssertSwapNeverStarted(CliException error)
    {
        IOException failure = Assert.IsType<IOException>(error.InnerException);
        Assert.Contains("changed after verification", failure.Message, StringComparison.Ordinal);
    }

    private static void AssertNoSwapResidue(string root) =>
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories),
            path => path.EndsWith(".displaced", StringComparison.Ordinal));

    [Fact]
    public void SuccessfulCommitPublishesEveryStagedArtifact()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(first, overwrite: false, staged => File.WriteAllText(staged, "one"));
        set.Stage(second, overwrite: false, staged => File.WriteAllText(staged, "two"));

        IReadOnlyList<long> sizes = set.Commit();

        Assert.Equal([3, 3], sizes);
        Assert.Equal("one", File.ReadAllText(first));
        Assert.Equal("two", File.ReadAllText(second));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void NewWindowsOutputsCarryTheAclTheirFolderGivesNewFiles()
    {
        Requires.Windows();

        VerifyNewWindowsOutputsCarryTheAclTheirFolderGivesNewFiles();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void ExistingWindowsTargetWithModifyOnlyAclCanBeSavedAndReopened()
    {
        Requires.Windows();

        VerifyExistingWindowsTargetWithModifyOnlyAclCanBeSavedAndReopened();
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyExistingWindowsTargetWithModifyOnlyAclCanBeSavedAndReopened()
    {
        using var temp = new TempDirectory();
        string directory = temp.File("modify-only");
        Directory.CreateDirectory(directory);
        SecurityIdentifier identity = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows identity has no SID.");
        var directorySecurity = new DirectorySecurity();
        directorySecurity.SetOwner(identity);
        directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        directorySecurity.AddAccessRule(new FileSystemAccessRule(
            identity,
            FileSystemRights.Modify | FileSystemRights.Synchronize,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(directorySecurity);

        string target = Path.Combine(directory, "published.txt");
        File.WriteAllText(target, "original");
        byte[] originalSecurity = new FileInfo(target)
            .GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner)
            .GetSecurityDescriptorBinaryForm();

        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            directory,
            "test");
        set.Stage(
            target,
            overwrite: true,
            staged => File.WriteAllText(staged, "replacement"));
        _ = set.Commit();

        Assert.Equal("replacement", File.ReadAllText(target));
        byte[] publishedSecurity = new FileInfo(target)
            .GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner)
            .GetSecurityDescriptorBinaryForm();
        Assert.Equal(originalSecurity, publishedSecurity);
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyNewWindowsOutputsCarryTheAclTheirFolderGivesNewFiles()
    {
        using var temp = new TempDirectory();
        string target = temp.File("published.txt");
        string reference = temp.File("reference.txt");
        File.WriteAllText(reference, "reference");
        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "content"));

        _ = set.Commit();

        string[] published = AccessRules(target);
        Assert.Equal(AccessRules(reference), published);
        Assert.DoesNotContain(published, rule => rule.EndsWith(":explicit", StringComparison.Ordinal));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void NewWindowsOutputInASubfolderInheritsTheSubfolderAcl()
    {
        Requires.Windows();

        VerifyNewWindowsOutputInASubfolderInheritsTheSubfolderAcl();
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyNewWindowsOutputInASubfolderInheritsTheSubfolderAcl()
    {
        using var temp = new TempDirectory();
        string folder = Directory.CreateDirectory(temp.File("shared")).FullName;
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var info = new DirectoryInfo(folder);
        DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);
        security.AddAccessRule(new FileSystemAccessRule(
            users,
            FileSystemRights.ReadData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
        string target = Path.Combine(folder, "published.txt");

        using var set = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "test");
        set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "content"));
        _ = set.Commit();

        Assert.Equal("content", File.ReadAllText(target));
        FileSystemAccessRule[] rules = new FileInfo(target).GetAccessControl(AccessControlSections.Access)
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .ToArray();
        Assert.Contains(rules, rule => rule.IdentityReference.Equals(users)
            && rule.IsInherited
            && rule.AccessControlType == AccessControlType.Allow
            && rule.FileSystemRights.HasFlag(FileSystemRights.ReadData));
        Assert.DoesNotContain(rules, rule => !rule.IsInherited);
    }

    [SupportedOSPlatform("windows")]
    private static string[] AccessRules(string path) =>
        new FileInfo(path).GetAccessControl(AccessControlSections.Access)
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Select(rule => $"{rule.IdentityReference}:{rule.AccessControlType}:{rule.FileSystemRights}:"
                + (rule.IsInherited ? "inherited" : "explicit"))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private sealed class SelectiveFaultInjector(
        Func<PublicationFaultPoint, Exception?> select) : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point)
        {
            if (select(point) is { } exception)
            {
                throw exception;
            }
        }
    }

}
