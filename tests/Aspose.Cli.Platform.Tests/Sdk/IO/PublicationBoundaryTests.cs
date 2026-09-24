using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

[Collection("Publication timing")]
public sealed class PublicationBoundaryTests
{
    [Fact]
    public void InterruptedUnknownRollbackRestoresTheOriginalInsteadOfDeletingItsEvidence()
    {
        using var temp = new TempDirectory();
        string target = temp.File("document.txt");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        string transaction = PrivateUserStorage.EnsureDirectory(temp.File(".aspose-publication-interrupted-rollback"));
        string backups = PrivateUserStorage.EnsureDirectory(Path.Combine(transaction, "backups"));
        string staged = AtomicPublicationPlan.StagedPath(transaction, target, 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, "uncommitted");
        FilePublicationSnapshot candidate = FilePublicationSnapshot.Capture(staged);
        string backup = Path.Combine(backups, "000001.backup");
        string displaced = Path.Combine(backups, "000001.displaced");
        File.Copy(target, backup);
        File.Replace(staged, target, displaced);
        new PublicationJournal
        {
            Operation = "interrupted-rollback", OwnerProcessId = int.MaxValue, OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.RollingBack,
            Entries = [new PublicationJournalEntry
            {
                Index = 0, Target = target, Staged = staged, Overwrite = true,
                Original = original, StagedSnapshot = candidate, Size = candidate.Length,
                Backup = backup, BackupSnapshot = FilePublicationSnapshot.Capture(backup),
                Displaced = displaced, DisplacedSnapshot = FilePublicationSnapshot.Capture(displaced),
                PublishedSnapshot = FilePublicationSnapshot.Capture(target),
                TargetParentIdentity = OutputPathValidator.CaptureParentIdentity(target),
                State = PublicationEntryState.Unknown,
            }],
        }.Write(Path.Combine(transaction, AtomicPublicationPlan.JournalName));

        Assert.Equal(1, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
        Assert.Equal(0, AtomicOutputSetWriter.RecoverPending(temp.Path));
    }

    [Fact]
    public void RecoveryOfAPublicationIntentRestoresSwappedTargetsAndLeavesUntouchedOnes()
    {
        using var temp = new TempDirectory();
        string transaction = PrivateUserStorage.EnsureDirectory(temp.File(".aspose-publication-interrupted-intent"));
        string backups = PrivateUserStorage.EnsureDirectory(Path.Combine(transaction, "backups"));
        PublicationJournalEntry Entry(int index, string name, bool swap)
        {
            string target = temp.File(name);
            File.WriteAllText(target, $"original {index}");
            FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
            string staged = AtomicPublicationPlan.StagedPath(transaction, target, index);
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            File.WriteAllText(staged, $"candidate {index}");
            FilePublicationSnapshot candidate = FilePublicationSnapshot.Capture(staged);
            string displaced = Path.Combine(backups, $"{index + 1:000000}.displaced");
            if (swap) { File.Replace(staged, target, displaced); }
            return new PublicationJournalEntry
            {
                Index = index, Target = target, Staged = staged, Overwrite = true,
                Original = original, StagedSnapshot = candidate, Size = candidate.Length,
                Backup = Path.Combine(backups, $"{index + 1:000000}.backup"), Displaced = displaced,
                TargetParentIdentity = OutputPathValidator.CaptureParentIdentity(target),
                State = PublicationEntryState.Publishing,
            };
        }
        new PublicationJournal
        {
            Operation = "interrupted-intent", OwnerProcessId = int.MaxValue, OwnerProcessStartUtcTicks = 1,
            State = PublicationTransactionState.Publishing,
            Entries = [Entry(0, "swapped.txt", swap: true), Entry(1, "untouched.txt", swap: false)],
        }.Write(Path.Combine(transaction, AtomicPublicationPlan.JournalName));
        File.WriteAllText(temp.File("untouched.txt"), "edited after the crash");

        Assert.Equal(1, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal("original 0", File.ReadAllText(temp.File("swapped.txt")));
        Assert.Equal("edited after the crash", File.ReadAllText(temp.File("untouched.txt")));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void UnfinishedWorkerCannotPublishEvenAfterAnOutputSetWasStaged()
    {
        using var temp = new TempDirectory();
        string root = PrivateUserStorage.CreateTemporaryDirectory("worker");
        try
        {
            string manifest = Path.Combine(root, WorkerOutputSession.ManifestName);
            var worker = new WorkerOutputSession(root, manifest);
            using var deadline = OperationDeadline.Start(null);
            var writer = new SafeFileWriter(new ResourceBudgetLedger(deadline, outputSession: worker));
            writer.Write(temp.File("output.txt"), false, file => File.WriteAllText(file, "candidate"));
            Assert.Throws<CliException>(() => WorkerOutputSession.Publish(manifest, TestBudgets.Create()));
            Assert.False(File.Exists(temp.File("output.txt")));
        }
        finally { PrivateUserStorage.TryDeleteTree(root); }
    }

    [Fact]
    public void FailedWorkerBatchDoesNotRetainAnEarlierEntry()
    {
        using var temp = new TempDirectory();
        string root = PrivateUserStorage.CreateTemporaryDirectory("worker");
        try
        {
            string manifest = Path.Combine(root, WorkerOutputSession.ManifestName);
            var worker = new WorkerOutputSession(root, manifest);
            using var deadline = OperationDeadline.Start(null);
            var writer = new SafeFileWriter(new ResourceBudgetLedger(deadline, outputSession: worker));
            using var outputs = new AtomicOutputSetWriter(writer, temp.Path, "failed-batch");
            outputs.Stage(temp.File("first.txt"), false, file => File.WriteAllText(file, "first"));
            StagedOutput changed = outputs.Stage(temp.File("second.txt"), false, file => File.WriteAllText(file, "second"));
            File.WriteAllText(changed.Path, "external replacement");
            Assert.Throws<CliException>(() => outputs.Commit());
            worker.SealForPublication();
            Assert.Empty(WorkerOutputSession.Publish(manifest, TestBudgets.Create()));
            Assert.False(File.Exists(temp.File("first.txt")));
            Assert.False(File.Exists(temp.File("second.txt")));
        }
        finally { PrivateUserStorage.TryDeleteTree(root); }
    }

    [Fact]
    public void OutputRootIncludesSiblingEvidenceDirectoriesWithoutCreatingThem()
    {
        using var temp = new TempDirectory();
        string documents = temp.File("documents");
        string evidence = temp.File("evidence");
        Assert.Equal(temp.Path, OutputSetPaths.CommonDirectory([documents, evidence]));
        Assert.False(Directory.Exists(documents));
        Assert.False(Directory.Exists(evidence));
        if (OperatingSystem.IsWindows())
        { Assert.Throws<CliException>(() => OutputSetPaths.CommonDirectory([@"C:\documents", @"D:\evidence"])); }
    }

    [Fact]
    public void MetadataAdmissionReservesSpaceForRecoverySnapshots()
    {
        using var temp = new TempDirectory();
        string original = temp.File("original.txt");
        File.WriteAllText(original, "original");
        FilePublicationSnapshot snapshot = FilePublicationSnapshot.Capture(original);
        FilePublicationSnapshot large = snapshot with
        { Metadata = snapshot.Metadata! with { WindowsSecurityDescriptor = new byte[32 * 1024] } };
        var journal = new PublicationJournal
        {
            Operation = "capacity", State = PublicationTransactionState.Staging,
            Entries = Enumerable.Range(0, 80).Select(index => new PublicationJournalEntry
            {
                Index = index, Target = temp.File($"target-{index}.txt"), Staged = temp.File($"stage-{index}.txt"),
                Overwrite = true, Original = large, StagedSnapshot = snapshot,
            }).ToList(),
        };
        string path = temp.File("journal.json");
        journal.Write(path);
        Assert.Throws<CliException>(() => journal.EnsureLifecycleCapacity(path));
        Assert.Equal("original", File.ReadAllText(original));
    }

    [Fact]
    public void ExistingDirectoryDoesNotConsumeTheNewDirectoryBudget()
    {
        using var temp = new TempDirectory();
        var directories = new OwnedOutputDirectories(deferred: true);
        directories.Ensure(temp.Path);
        for (int index = 0; index < PublicationLimits.MaximumDirectories; index++)
        { directories.Ensure(temp.File($"directory-{index}")); }
        Assert.Equal(PublicationLimits.MaximumDirectories, directories.Declared.Count());
        Assert.Throws<CliException>(() => directories.Ensure(temp.File("overflow")));
        Assert.Empty(Directory.GetDirectories(temp.Path));
    }

    [Fact]
    public void OwnedDirectoriesSupportLongLocalPathsAndRemainSafelyRemovable()
    {
        using var temp = new TempDirectory();
        string first = temp.File(new string('a', 90));
        string target = Path.Combine(first, new string('b', 90), new string('c', 90));
        Assert.True(target.Length > 260);
        var directories = new OwnedOutputDirectories(deferred: false);
        directories.Ensure(target);
        Assert.True(Directory.Exists(target));
        directories.CleanUp();
        Assert.False(Directory.Exists(first));
        Assert.True(Directory.Exists(temp.Path));
    }

    [Fact]
    public void DirectoryCleanupPreservesExistingAndPopulatedDirectories()
    {
        using var temp = new TempDirectory();
        string existing = temp.File("existing");
        Directory.CreateDirectory(existing);
        var directories = new OwnedOutputDirectories(deferred: false);
        directories.Ensure(existing);
        directories.Ensure(temp.File(Path.Combine("created", "nested")));
        string populated = temp.File("populated");
        directories.Ensure(populated);
        File.WriteAllText(Path.Combine(populated, "external.txt"), "preserve");
        directories.CleanUp();
        Assert.True(Directory.Exists(existing));
        Assert.False(Directory.Exists(temp.File("created")));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(populated, "external.txt")));
    }

    [Fact]
    public void FailedTerminalRollbackWritePreservesEvidenceForAnIdempotentRetry()
    {
        using var temp = new TempDirectory();
        string target = temp.File("first.txt");
        File.WriteAllText(target, "original");
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "terminal-failure",
            new TerminalRollbackFault()))
        {
            outputs.Stage(target, true, path => File.WriteAllText(path, "changed"));
            outputs.Stage(temp.File("second.txt"), false, path => File.WriteAllText(path, "second"));
            CliException error = Assert.Throws<CliException>(() => outputs.Commit());
            Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        }
        Assert.Equal("original", File.ReadAllText(target));
        string transaction = Assert.Single(Directory.GetDirectories(temp.Path, ".aspose-publication-*"));
        Assert.True(File.Exists(Path.Combine(transaction, "backups", "000001.backup")));
        string journalPath = Path.Combine(transaction, AtomicPublicationPlan.JournalName);
        var journal = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath))!;
        journal["ownerProcessId"] = int.MaxValue;
        journal["ownerProcessStartUtcTicks"] = 1;
        File.WriteAllText(journalPath, journal.ToJsonString());
        Assert.Equal(1, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal(0, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.Equal("original", File.ReadAllText(target));
    }

    /// <summary>Fails the rollback's terminal record: its first write records RollingBack, its second the outcome.</summary>
    private sealed class TerminalRollbackFault : IPublicationFaultInjector
    {
        private int _rollbackWrites = -1;

        public void Hit(PublicationFaultPoint point)
        {
            if (point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1)
            {
                _rollbackWrites = 0;
                throw new IOException("Injected publication failure.");
            }
            if (point.Kind == PublicationFaultKind.JournalWrite && _rollbackWrites >= 0 && ++_rollbackWrites == 2)
            { throw new IOException("Injected terminal journal failure."); }
        }
    }

    [Fact]
    public void TransientJournalLockIsWaitedOutWithoutRollingBack()
    {
        using var temp = new TempDirectory();
        string target = temp.File("target.txt");
        File.WriteAllText(target, "original");
        var locker = new TransientJournalLock(TimeSpan.FromMilliseconds(300));
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "transient-lock", locker))
        {
            outputs.Stage(target, true, path => File.WriteAllText(path, "replacement"));
            outputs.Stage(temp.File("new.txt"), false, path => File.WriteAllText(path, "new"));
            outputs.Commit();
        }
        Assert.True(locker.Locked);
        Assert.Equal("replacement", File.ReadAllText(target));
        Assert.Equal("new", File.ReadAllText(temp.File("new.txt")));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    /// <summary>Holds the existing journal open without delete sharing, as a scanner does, then releases it.</summary>
    private sealed class TransientJournalLock(TimeSpan duration) : IPublicationFaultInjector
    {
        public bool Locked { get; private set; }

        public void Hit(PublicationFaultPoint point)
        {
            if (Locked || point.Kind != PublicationFaultKind.JournalWrite || !File.Exists(point.Path)) { return; }
            var handle = new FileStream(point.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Locked = true;
            _ = Task.Delay(duration).ContinueWith(_ => handle.Dispose(), TaskScheduler.Default);
        }
    }

    [Fact]
    public void StagingDoesNotPublishARecoveryJournal()
    {
        using var temp = new TempDirectory();
        using var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "candidate");
        outputs.Stage(temp.File("report.txt"), false, file => File.WriteAllText(file, "candidate"));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, AtomicPublicationPlan.JournalName, SearchOption.AllDirectories));
        Assert.False(File.Exists(temp.File("report.txt")));
    }

    [Fact]
    public void UnknownProducerArtifactsDoNotBlockTheNextPublication()
    {
        using var temp = new TempDirectory();
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "producer"))
        {
            outputs.Stage(temp.File("bad.txt"), false, file =>
            {
                File.WriteAllText(file, "candidate");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(file)!, "unknown.txt"), "preserve");
            });
            Assert.Throws<CliException>(() => outputs.Commit());
        }
        TestBudgets.Writer().Write(temp.File("next.txt"), false, file => File.WriteAllText(file, "next"));
        Assert.Equal("next", File.ReadAllText(temp.File("next.txt")));
        Assert.Single(Directory.EnumerateFiles(temp.Path, "unknown.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public void InterruptedInitialSealPreservesPrivateCandidatesWithoutBlockingPublication()
    {
        using var temp = new TempDirectory();
        for (int index = 0; index < 34; index++)
        {
            string transaction = PrivateUserStorage.EnsureDirectory(temp.File($".aspose-publication-unsealed-{index}"));
            string candidate = Path.Combine(transaction, "output-000001", "candidate.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
            File.WriteAllText(candidate, "private candidate");
            PrivateUserStorage.WriteAllText(
                Path.Combine(transaction, $".publication-journal.v1.json.{Guid.NewGuid():N}.tmp"), "interrupted journal");
        }
        Assert.Equal(0, AtomicOutputSetWriter.RecoverPending(temp.Path));
        TestBudgets.Writer().Write(temp.File("next.txt"), false, path => File.WriteAllText(path, "next"));
        Assert.Equal("next", File.ReadAllText(temp.File("next.txt")));
        Assert.Equal(34, Directory.EnumerateFiles(temp.Path, "candidate.txt", SearchOption.AllDirectories).Count());
        Assert.Equal(34, Directory.EnumerateFiles(temp.Path, "*.tmp", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public void ExtractionSupportsOneThousandFiles()
    {
        using var temp = new TempDirectory();
        using var extraction = new ExtractionGuard(TestBudgets.Create(), temp.Path);
        for (int i = 0; i < 1000; i++) { extraction.WriteAllBytes($"item-{i}.bin", [1]); }
        extraction.Commit();
        Assert.Equal(1000, Directory.EnumerateFiles(temp.Path, "*.bin").Count());
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void RejectedExtraOutputDoesNotRunItsProducerOrPoisonRecovery()
    {
        using var temp = new TempDirectory();
        bool produced = false;
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "capacity"))
        {
            for (int i = 0; i < 1000; i++) { outputs.Stage(temp.File($"item-{i}.txt"), false, file => File.WriteAllText(file, "x")); }
            Assert.Throws<CliException>(() => outputs.Stage(temp.File("overflow.txt"), false, _ => produced = true));
        }
        Assert.False(produced);
        TestBudgets.Writer().Write(temp.File("next.txt"), false, file => File.WriteAllText(file, "next"));
        Assert.Equal("next", File.ReadAllText(temp.File("next.txt")));
    }
}
