using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
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
        Assert.Equal(ExitCode.PartialFailure, error.ExitCode);
        Assert.False(error.Details!["recoveryComplete"]!.GetValue<bool>());
        JsonArray targets = Assert.IsType<JsonArray>(error.Details["targets"]);
        JsonObject firstState = Assert.IsType<JsonObject>(targets[0]);
        Assert.Equal("unknown", firstState["status"]!.GetValue<string>());
        Assert.False(firstState["contentVerified"]!.GetValue<bool>());
        Assert.Equal("replacement-one", File.ReadAllText(first));
        Assert.Equal("original-two", File.ReadAllText(second));
        Assert.Single(Directory.EnumerateDirectories(temp.Path, ".aspose-*"));
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
        Assert.True(File.Exists(Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName)));
    }

    [Fact]
    public void BackupFailureOccursBeforeAnyTargetIsPublished()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.txt");
        string second = temp.File("second.txt");
        File.WriteAllText(first, "original-one");
        File.WriteAllText(second, "original-two");
        var faults = new SelectiveFaultInjector(
            point => point.Kind == PublicationFaultKind.Backup && point.EntryIndex == 1
                ? new IOException("backup fault")
                : null);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(first, overwrite: true, staged => File.WriteAllText(staged, "replacement-one"));
        set.Stage(second, overwrite: true, staged => File.WriteAllText(staged, "replacement-two"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original-one", File.ReadAllText(first));
        Assert.Equal("original-two", File.ReadAllText(second));
    }

    [Fact]
    public void JournalFailureAfterPublicationStillTriggersVerifiedRecovery()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        bool publicationStarted = false;
        bool injected = false;
        var faults = new SelectiveFaultInjector(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                publicationStarted = true;
                return null;
            }

            if (publicationStarted
                && !injected
                && point.Kind == PublicationFaultKind.JournalWrite)
            {
                injected = true;
                return new IOException("journal fault");
            }

            return null;
        });
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "test",
            faults);
        set.Stage(target, overwrite: true, staged => File.WriteAllText(staged, "replacement"));

        CliException error = Assert.Throws<CliException>(() => set.Commit());

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("original", File.ReadAllText(target));
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
    public void AbandonedPublishingJournalIsRecoveredAndRemoved()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = Path.Combine(
            temp.Path,
            ".aspose-publication-crashed-0001");
        string backups = Path.Combine(transaction, "backups");
        Directory.CreateDirectory(transaction);
        Directory.CreateDirectory(backups);
        string staged = AtomicPublicationPlan.StagedPath(
            transaction,
            target,
            index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        string backup = Path.Combine(backups, "000001.backup");
        string displaced = Path.Combine(backups, "000001.displaced");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original =
            FilePublicationSnapshot.Capture(target);
        File.WriteAllText(staged, "replacement");
        FilePublicationSnapshot stagedSnapshot =
            FilePublicationSnapshot.Capture(staged);
        File.WriteAllText(backup, "original");
        File.Replace(staged, target, displaced);
        var journal = new PublicationJournal
        {
            Operation = "crashed",
            OwnerProcessId = int.MaxValue,
            OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.Publishing,
            Entries =
            [
                new PublicationJournalEntry
                {
                    Index = 0,
                    Target = target,
                    Staged = staged,
                    Backup = backup,
                    Displaced = displaced,
                    DisplacedSnapshot =
                        FilePublicationSnapshot.Capture(displaced),
                    Overwrite = true,
                    Original = original,
                    StagedSnapshot = stagedSnapshot,
                    TargetParentIdentity =
                        OutputPathValidator.CaptureParentIdentity(target),
                    PublishedSnapshot = FilePublicationSnapshot.Capture(target),
                    State = PublicationEntryState.Published,
                    Size = stagedSnapshot.Length,
                },
            ],
        };
        journal.Write(Path.Combine(transaction, AtomicPublicationPlan.JournalName));

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void InterruptedDeletionIsRecoveredWithoutAPublishedSnapshot()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = Path.Combine(
            temp.Path,
            ".aspose-publication-crashed-delete");
        string backups = Path.Combine(transaction, "backups");
        Directory.CreateDirectory(transaction);
        Directory.CreateDirectory(backups);
        string staged = AtomicPublicationPlan.StagedPath(
            transaction,
            target,
            index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        string backup = Path.Combine(backups, "000001.backup");
        string displaced = Path.Combine(backups, "000001.displaced");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original =
            FilePublicationSnapshot.Capture(target);
        File.WriteAllBytes(staged, []);
        File.WriteAllText(backup, "original");
        File.Move(target, displaced);
        var journal = new PublicationJournal
        {
            Operation = "crashed-delete",
            OwnerProcessId = int.MaxValue,
            OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.Publishing,
            Entries =
            [
                new PublicationJournalEntry
                {
                    Index = 0,
                    Target = target,
                    Staged = staged,
                    Backup = backup,
                    BackupSnapshot = FilePublicationSnapshot.Capture(backup),
                    Displaced = displaced,
                    Overwrite = true,
                    Original = original,
                    StagedSnapshot = FilePublicationSnapshot.Capture(staged),
                    TargetParentIdentity =
                        OutputPathValidator.CaptureParentIdentity(target),
                    State = PublicationEntryState.Publishing,
                    DeleteTarget = true,
                    Size = 0,
                },
            ],
        };
        journal.Write(Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName));

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void InterruptedReplacementIsRecoveredWithoutAPublishedSnapshot()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        (string transaction, string staged, _) =
            CreateInterruptedReplacementTransaction(temp.Path, target);

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void RecoveryRetryAcceptsTheOriginalPhysicalIdentity()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        (string transaction, string staged, string displaced) =
            CreateInterruptedReplacementTransaction(temp.Path, target);
        File.Replace(displaced, target, staged);

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void InterruptedNewOutputIsRemovedWithoutAPublishedSnapshot()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = Path.Combine(
            temp.Path,
            ".aspose-publication-crashed-create");
        Directory.CreateDirectory(transaction);
        string staged = AtomicPublicationPlan.StagedPath(
            transaction,
            target,
            index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, "created");
        FilePublicationSnapshot stagedSnapshot =
            FilePublicationSnapshot.Capture(staged);
        File.Move(staged, target);
        var journal = new PublicationJournal
        {
            Operation = "crashed-create",
            OwnerProcessId = int.MaxValue,
            OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.Publishing,
            Entries =
            [
                new PublicationJournalEntry
                {
                    Index = 0,
                    Target = target,
                    Staged = staged,
                    Overwrite = false,
                    Original = FilePublicationSnapshot.Missing,
                    StagedSnapshot = stagedSnapshot,
                    TargetParentIdentity =
                        OutputPathValidator.CaptureParentIdentity(target),
                    State = PublicationEntryState.Publishing,
                    Size = stagedSnapshot.Length,
                },
            ],
        };
        journal.Write(Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName));

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.False(File.Exists(target));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void OrphanJournalTemporaryIsBoundedlyRemoved()
    {
        using var temp = new TempDirectory();
        string transaction = Directory.CreateDirectory(Path.Combine(
            temp.Path,
            $".aspose-publication-crashed-{Guid.NewGuid():N}")).FullName;
        string temporary = Path.Combine(
            transaction,
            $".{AtomicPublicationPlan.JournalName}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, "partial journal");

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void NewPublicationAutomaticallyRecoversAnAbandonedTransaction()
    {
        using var temp = new TempDirectory();
        string abandonedTarget = temp.File("abandoned.txt");
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            abandonedTarget,
            original: "original",
            published: "published");
        string newTarget = temp.File("new.txt");

        using (var set = new AtomicOutputSetWriter(
                   TestBudgets.Writer(),
                   temp.Path,
                   "next"))
        {
            set.Stage(
                newTarget,
                overwrite: false,
                staged => File.WriteAllText(staged, "new-output"));
            set.Commit();
        }

        Assert.Equal("original", File.ReadAllText(abandonedTarget));
        Assert.Equal("new-output", File.ReadAllText(newTarget));
        Assert.False(Directory.Exists(transaction));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-*"));
    }

    [Fact]
    public void RecoveryRejectsAReplacedLeasedDirectoryBeforeScanning()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        string output = temp.File("output");
        string displaced = temp.File("displaced");
        Directory.CreateDirectory(output);
        using PublicationDirectoryLease lease =
            PublicationDirectoryLease.Acquire(output);
        Directory.Move(output, displaced);
        Directory.CreateDirectory(output);

        CliException error = Assert.Throws<CliException>(() =>
            AtomicPublicationRecovery.RecoverPendingUnderLease(
                output,
                lease));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("path", error.Details!["phase"]!.GetValue<string>());
    }

    [Fact]
    public void SingleFileWriteRecoversBeforeCapturingItsTargetSnapshot()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            target,
            original: "original",
            published: "published");

        TestBudgets.Writer().Write(
            target,
            overwrite: true,
            staged => File.WriteAllText(staged, "next"));

        Assert.Equal("next", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void SingleFileWriteRecoversAnAncestorTransactionTargetingItsSubtreeFirst()
    {
        using var temp = new TempDirectory();
        string abandonedTarget = temp.File(Path.Combine("a", "nested", "abandoned.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(abandonedTarget)!);
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            abandonedTarget,
            original: "original",
            published: "published");
        string childTarget = temp.File(Path.Combine("a", "nested", "new.txt"));

        TestBudgets.Writer().Write(
            childTarget,
            overwrite: false,
            staged => File.WriteAllText(staged, "new"));

        Assert.Equal("original", File.ReadAllText(abandonedTarget));
        Assert.Equal("new", File.ReadAllText(childTarget));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void AbandonedRecoveryPreservesAnExternalTargetChange()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            target,
            original: "original",
            published: "published");
        File.WriteAllText(target, "external-change");

        CliException error = Assert.Throws<CliException>(
            () => AtomicOutputSetWriter.RecoverPending(temp.Path));

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.Equal("external-change", File.ReadAllText(target));
        Assert.True(Directory.Exists(transaction));
    }

    [Fact]
    public void AbandonedRecoveryRejectsSameContentExternalReplacement()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            target,
            original: "original",
            published: "published");
        string external = temp.File("external.txt");
        File.WriteAllText(external, "published");
        FilePublicationSnapshot expectedExternal =
            FilePublicationSnapshot.Capture(external);
        File.Move(external, target, overwrite: true);

        CliException error = Assert.Throws<CliException>(
            () => AtomicOutputSetWriter.RecoverPending(temp.Path));

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.True(expectedExternal.VersionEquals(
            FilePublicationSnapshot.Capture(target)));
        Assert.True(Directory.Exists(transaction));
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
        Assert.False(File.Exists(Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName)));
    }

    [Fact]
    public void PartialJournalIsNeverDeletedByLaterAutomaticRecovery()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            target,
            original: "original",
            published: "published");
        string journalPath = Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName);
        PublicationJournal journal = PublicationJournal.Read(journalPath);
        journal.State = PublicationTransactionState.Partial;
        journal.Write(journalPath);
        string backup = Assert.Single(journal.Entries).Backup!;

        CliException error = Assert.Throws<CliException>(() =>
            AtomicOutputSetWriter.RecoverPending(temp.Path));

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.True(File.Exists(journalPath));
        Assert.True(File.Exists(backup));
        Assert.True(Directory.Exists(transaction));
    }

    [Fact]
    public void OversizedRecoveryJournalIsRejectedWithoutDeletion()
    {
        using var temp = new TempDirectory();
        string transaction = Path.Combine(
            temp.Path,
            ".aspose-publication-invalid-large");
        Directory.CreateDirectory(transaction);
        string journal = Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName);
        File.WriteAllBytes(
            journal,
            new byte[PublicationJournal.MaximumBytes + 1]);

        CliException error = Assert.Throws<CliException>(
            () => AtomicOutputSetWriter.RecoverPending(temp.Path));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("recovery", error.Details!["phase"]!.GetValue<string>());
        Assert.True(File.Exists(journal));
    }

    [Fact]
    public void DuplicateRecoveryJournalPropertiesAreRejected()
    {
        using var temp = new TempDirectory();
        string transaction = Directory.CreateDirectory(Path.Combine(
            temp.Path,
            ".aspose-publication-invalid-duplicate")).FullName;
        string journal = Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName);
        File.WriteAllText(
            journal,
            """
            {"version":1,"operation":"first","operation":"second","state":0,"entries":[]}
            """);

        CliException error = Assert.Throws<CliException>(
            () => AtomicOutputSetWriter.RecoverPending(temp.Path));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("recovery", error.Details!["phase"]!.GetValue<string>());
        Assert.True(File.Exists(journal));
    }

    [Fact]
    public void NullRecoveryJournalEntriesAreRejected()
    {
        using var temp = new TempDirectory();
        string transaction = Directory.CreateDirectory(Path.Combine(
            temp.Path,
            ".aspose-publication-invalid-null")).FullName;
        string journal = Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName);
        File.WriteAllText(
            journal,
            """
            {"version":1,"operation":"invalid","ownerProcessId":1,"state":0,"entries":null}
            """);

        CliException error = Assert.Throws<CliException>(
            () => AtomicOutputSetWriter.RecoverPending(temp.Path));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("recovery", error.Details!["phase"]!.GetValue<string>());
        Assert.True(File.Exists(journal));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void ForeignTransactionDirectoriesNeitherBlockNorConsumeRecoveryBudget()
    {
        // Assigning another principal as owner needs the restore privilege of an elevated token.
        Requires.ElevatedWindows();
        using var temp = new TempDirectory();
        string nested = Directory.CreateDirectory(temp.File("nested")).FullName;
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var planted = new List<string>();
        foreach (string parent in new[] { temp.Path, nested })
        {
            for (int index = 0; index < 40; index++)
            {
                string foreign = Directory.CreateDirectory(
                    Path.Combine(parent, $".aspose-publication-foreign-{index:000}")).FullName;
                string journal = Path.Combine(foreign, AtomicPublicationPlan.JournalName);
                File.WriteAllText(journal, "{\"state\":\"Partial\"}");
                planted.Add(journal);
            }
            SetOwner(Path.Combine(parent, ".aspose-publication-foreign-*"), system);
        }
        Assert.All(planted, journal => Assert.Equal(system, OwnerOf(Path.GetDirectoryName(journal)!)));

        string target = Path.Combine(nested, "target.txt");
        using (var set = new AtomicOutputSetWriter(TestBudgets.Writer(), nested, "foreign-state"))
        {
            set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "content"));
            set.Commit();
        }

        Assert.Equal("content", File.ReadAllText(target));
        Assert.All(planted, journal => Assert.True(File.Exists(journal)));
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
    public void TransactionOwnedByTheElevatedTokensDefaultOwnerIsRecovered()
    {
        Requires.ElevatedWindows();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        Assert.SkipUnless(administrators == WindowsIdentity.GetCurrent().Owner,
            "Requires a token whose default owner is the Administrators group.");
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string transaction = CreateAbandonedPublishingTransaction(
            temp.Path,
            target,
            original: "original",
            published: "published");
        Assert.Equal(administrators, OwnerOf(transaction));
        Assert.NotEqual(WindowsIdentity.GetCurrent().User, OwnerOf(transaction));

        int recovered = AtomicOutputSetWriter.RecoverPending(temp.Path);

        Assert.Equal(1, recovered);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
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

    [SupportedOSPlatform("windows")]
    private static IdentityReference? OwnerOf(string directory) =>
        new DirectoryInfo(directory).GetAccessControl(AccessControlSections.Owner)
            .GetOwner(typeof(SecurityIdentifier));

    /// <summary>Uses icacls, which enables the restore privilege that assigning another owner needs.</summary>
    [SupportedOSPlatform("windows")]
    private static void SetOwner(string pattern, SecurityIdentifier owner)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "icacls.exe"))
        {
            ArgumentList = { pattern, "/setowner", "*" + owner.Value, "/Q" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output);
    }

    [Fact]
    public void UnrelatedAncestorTransactionDoesNotBlockNestedPublication()
    {
        using var temp = new TempDirectory();
        string transaction = Directory.CreateDirectory(temp.File(".aspose-publication-invalid-ancestor")).FullName;
        string journal = Path.Combine(transaction, AtomicPublicationPlan.JournalName);
        File.WriteAllText(journal, """{"version":1,"operation":"invalid","ownerProcessId":1,"state":0,"entries":null}""");
        string nested = Directory.CreateDirectory(temp.File("nested")).FullName;
        string target = Path.Combine(nested, "target.txt");

        using (var set = new AtomicOutputSetWriter(TestBudgets.Writer(), nested, "nested"))
        {
            set.Stage(target, overwrite: false, staged => File.WriteAllText(staged, "content"));
            set.Commit();
        }

        Assert.Equal("content", File.ReadAllText(target));
        Assert.True(File.Exists(journal));
        Assert.Throws<CliException>(() => AtomicOutputSetWriter.RecoverPending(temp.Path));
    }

    [Fact]
    public void UnrelatedAsposeDirectoriesDoNotConsumeRecoveryBudget()
    {
        using var temp = new TempDirectory();
        for (int index = 0; index < 40; index++)
        {
            Directory.CreateDirectory(temp.File($".aspose-user-{index:000}"));
        }
        string target = temp.File("target.txt");

        using (var set = new AtomicOutputSetWriter(
                   TestBudgets.Writer(),
                   temp.Path,
                   "..\\..\\untrusted-operation"))
        {
            set.Stage(
                target,
                overwrite: false,
                staged => File.WriteAllText(staged, "content"));
            set.Commit();
        }

        Assert.Equal("content", File.ReadAllText(target));
        Assert.Empty(Directory.EnumerateDirectories(
            temp.Path,
            ".aspose-publication-*"));
    }

    [Fact]
    public async Task StagingDoesNotBlockAnIndependentDirectoryPublication()
    {
        using var temp = new TempDirectory();
        string first = temp.File(Path.Combine("a", "first.txt"));
        string second = temp.File(Path.Combine("b", "second.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        using var set = new AtomicOutputSetWriter(
            TestBudgets.Writer(),
            temp.Path,
            "ancestor");
        set.Stage(
            first,
            overwrite: false,
            staged => File.WriteAllText(staged, "first"));

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task secondWrite = Task.Factory.StartNew(() =>
        {
            started.SetResult();
            TestBudgets.Writer().Write(second, overwrite: false,
                staged => File.WriteAllText(staged, "second"));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await secondWrite.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(first));

        set.Commit();
        await secondWrite.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("first", File.ReadAllText(first));
        Assert.Equal("second", File.ReadAllText(second));
    }

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
    public void AtomicSwapRestoresSameContentExternalReplacementByIdentity()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        string staged = temp.File("staged.txt");
        string external = temp.File("external.txt");
        File.WriteAllText(target, "same-content");
        FilePublicationSnapshot expected = FilePublicationSnapshot.Capture(target);
        File.WriteAllText(external, "same-content");
        File.Move(external, target, overwrite: true);
        FilePublicationSnapshot externalSnapshot = FilePublicationSnapshot.Capture(target);
        File.WriteAllText(staged, "published-content");

        CliException error = Assert.Throws<CliException>(() =>
            FilePublicationAtomicSwap.Publish(
                staged,
                target,
                overwrite: true,
                expected,
                expected));

        Assert.Equal(ErrorCodes.OutputConflict, error.Code);
        Assert.True(externalSnapshot.VersionEquals(
            FilePublicationSnapshot.Capture(target)));
        Assert.Equal("same-content", File.ReadAllText(target));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(temp.Path),
            path => path.EndsWith(".displaced", StringComparison.Ordinal)
                || path.EndsWith(".conflict", StringComparison.Ordinal));
    }

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

    private static (string Transaction, string Staged, string Displaced)
        CreateInterruptedReplacementTransaction(
            string root,
            string target)
    {
        string transaction = Path.Combine(
            root,
            $".aspose-publication-crashed-{Guid.NewGuid():N}");
        string backups = Path.Combine(transaction, "backups");
        Directory.CreateDirectory(transaction);
        Directory.CreateDirectory(backups);
        string staged = AtomicPublicationPlan.StagedPath(
            transaction,
            target,
            index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        string backup = Path.Combine(backups, "000001.backup");
        string displaced = Path.Combine(backups, "000001.displaced");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original =
            FilePublicationSnapshot.Capture(target);
        File.WriteAllText(staged, "replacement");
        FilePublicationSnapshot stagedSnapshot =
            FilePublicationSnapshot.Capture(staged);
        File.WriteAllText(backup, "original");
        File.Replace(staged, target, displaced);
        var journal = new PublicationJournal
        {
            Operation = "crashed-replace",
            OwnerProcessId = int.MaxValue,
            OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.Publishing,
            Entries =
            [
                new PublicationJournalEntry
                {
                    Index = 0,
                    Target = target,
                    Staged = staged,
                    Backup = backup,
                    BackupSnapshot = FilePublicationSnapshot.Capture(backup),
                    Displaced = displaced,
                    Overwrite = true,
                    Original = original,
                    StagedSnapshot = stagedSnapshot,
                    TargetParentIdentity =
                        OutputPathValidator.CaptureParentIdentity(target),
                    State = PublicationEntryState.Publishing,
                    Size = stagedSnapshot.Length,
                },
            ],
        };
        journal.Write(Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName));
        return (transaction, staged, displaced);
    }

    private static string CreateAbandonedPublishingTransaction(
        string root,
        string target,
        string original,
        string published)
    {
        string transaction = Path.Combine(
            root,
            $".aspose-publication-crashed-{Guid.NewGuid():N}");
        string backups = Path.Combine(transaction, "backups");
        Directory.CreateDirectory(transaction);
        Directory.CreateDirectory(backups);
        string staged = AtomicPublicationPlan.StagedPath(
            transaction,
            target,
            index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        string backup = Path.Combine(backups, "000001.backup");
        string displaced = Path.Combine(backups, "000001.displaced");
        File.WriteAllText(target, original);
        FilePublicationSnapshot originalSnapshot =
            FilePublicationSnapshot.Capture(target);
        File.WriteAllText(staged, published);
        FilePublicationSnapshot stagedSnapshot =
            FilePublicationSnapshot.Capture(staged);
        File.WriteAllText(backup, original);
        File.Replace(staged, target, displaced);
        var journal = new PublicationJournal
        {
            Operation = "crashed",
            OwnerProcessId = int.MaxValue,
            OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.Publishing,
            Entries =
            [
                new PublicationJournalEntry
                {
                    Index = 0,
                    Target = target,
                    Staged = staged,
                    Backup = backup,
                    BackupSnapshot = FilePublicationSnapshot.Capture(backup),
                    Displaced = displaced,
                    DisplacedSnapshot =
                        FilePublicationSnapshot.Capture(displaced),
                    Overwrite = true,
                    Original = originalSnapshot,
                    StagedSnapshot = stagedSnapshot,
                    TargetParentIdentity =
                        OutputPathValidator.CaptureParentIdentity(target),
                    PublishedSnapshot = FilePublicationSnapshot.Capture(target),
                    State = PublicationEntryState.Published,
                    Size = stagedSnapshot.Length,
                },
            ],
        };
        journal.Write(Path.Combine(
            transaction,
            AtomicPublicationPlan.JournalName));
        return transaction;
    }
}
