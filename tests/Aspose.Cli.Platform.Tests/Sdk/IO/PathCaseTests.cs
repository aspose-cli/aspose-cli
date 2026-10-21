using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Windows file names ignore case, so every place that compares local paths treats two
/// spellings of one path as the same path: the worker layout, the publication journal and its
/// recovery, the output set, the publication locks and the directory boundaries. The tests
/// spell the second path in upper case, which every test directory under %TEMP% changes.
/// </summary>
public sealed class PathCaseTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _workerRoot = WorkerOutputSession.CreateRoot();

    public void Dispose()
    {
        _temp.Dispose();
        UserStorage.TryDeleteTree(_workerRoot);
    }

    private static string Upper(string path) => path.ToUpperInvariant();

    [Fact]
    public void WorkerManifest_ATargetInACaseVariantOfThePrivateRoot_IsRefused()
    {
        Assert.NotEqual(_workerRoot, Upper(_workerRoot));
        string manifestPath = Path.Combine(_workerRoot, WorkerOutputSession.ManifestName);
        PlannedOutput entry = WorkerEntry(_temp.File("report.txt"));
        WorkerManifestStore.Write(manifestPath, new WorkerOutputManifest
        {
            Entries = [entry with { Target = Upper(Path.Combine(_workerRoot, "report.txt")) }],
            Sealed = true,
        });

        CliException error = Assert.Throws<CliException>(() => WorkerManifestStore.ReadAndValidate(manifestPath));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        InvalidDataException cause = Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Contains("private transport storage", cause.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerManifest_TwoTargetsThatDifferOnlyInCase_Overlap()
    {
        string manifestPath = Path.Combine(_workerRoot, WorkerOutputSession.ManifestName);
        string target = _temp.File("report.txt");
        WorkerManifestStore.Write(manifestPath, new WorkerOutputManifest
        {
            Entries = [WorkerEntry(target), WorkerEntry(target) with { Target = Upper(target) }],
            Sealed = true,
        });

        CliException error = Assert.Throws<CliException>(() => WorkerManifestStore.ReadAndValidate(manifestPath));

        InvalidDataException cause = Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Contains("target paths overlap", cause.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicationPlan_TreatsACaseVariantOfItsDirectoryAndArtifactsAsItsOwn()
    {
        string transaction = Directory.CreateDirectory(_temp.File(".aspose-publication-case")).FullName;
        string target = _temp.File("report.txt");
        string staged = AtomicPublicationPlan.StagedPath(transaction, target, index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, "staged");
        var journal = new PublicationJournal
        {
            Operation = "case",
            State = PublicationTransactionState.Staging,
            Entries =
            [
                new PublicationJournalEntry
                {
                    Index = 0,
                    Target = Upper(target),
                    Staged = Upper(staged),
                    Overwrite = false,
                    Original = FilePublicationSnapshot.Capture(target),
                    StagedSnapshot = FilePublicationSnapshot.Capture(staged),
                },
            ],
        };
        AtomicPublicationPlan plan = AtomicPublicationPlan.Open(transaction, journal);

        plan.EnsureTarget(Upper(target));
        plan.EnsureNoUnknownArtifacts();

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(staged)!, "unknown.txt"), "unknown");
        Assert.Throws<IOException>(plan.EnsureNoUnknownArtifacts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recovery_OfAJournalThatSpellsItsPathsInAnotherCase_RestoresAsTheSameSpellingDoes(bool upper)
    {
        Func<string, string> spell = upper ? Upper : static path => path;
        string target = _temp.File("target.txt");
        string transaction = Path.Combine(_temp.Path, ".aspose-publication-crashed-0001");
        string backups = Path.Combine(transaction, "backups");
        Directory.CreateDirectory(backups);
        string staged = AtomicPublicationPlan.StagedPath(transaction, target, index: 0);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        string displaced = Path.Combine(backups, "000001.displaced");
        File.WriteAllText(target, "original");
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        File.WriteAllText(staged, "replacement");
        FilePublicationSnapshot stagedSnapshot = FilePublicationSnapshot.Capture(staged);
        File.Replace(staged, target, displaced);
        new PublicationJournal
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
                    Target = spell(target),
                    Staged = spell(staged),
                    Displaced = spell(displaced),
                    DisplacedSnapshot = FilePublicationSnapshot.Capture(displaced),
                    Overwrite = true,
                    Original = original,
                    StagedSnapshot = stagedSnapshot,
                    TargetParentIdentity = OutputPathValidator.CaptureParentIdentity(target),
                    PublishedSnapshot = FilePublicationSnapshot.Capture(target),
                    State = PublicationEntryState.Published,
                    Size = stagedSnapshot.Length,
                },
            ],
        }.Write(Path.Combine(transaction, AtomicPublicationPlan.JournalName));

        int recovered = AtomicPublicationRecovery.RecoverPendingUnderLease(_temp.Path, lease: null);

        Assert.Equal(1, recovered);
        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(Directory.Exists(transaction));
    }

    [Fact]
    public void OutputSet_CaseVariantsShareOneRootAndOneDeclaredDirectory()
    {
        string lower = _temp.File("out");
        Assert.Equal(lower, OutputSetPaths.CommonDirectory([Path.Combine(lower, "x"), Upper(Path.Combine(lower, "y"))]));

        var directories = new OwnedOutputDirectories(deferred: true);
        directories.Ensure(Path.Combine(lower, "new"));
        directories.Ensure(Upper(Path.Combine(lower, "new")));

        Assert.Equal(2, directories.Declared.Count());
        Assert.False(Directory.Exists(lower));
    }

    [Fact]
    public void PublicationLocks_HeldUnderOneSpelling_ExcludeAnother()
    {
        string directory = Directory.CreateDirectory(_temp.File("locked")).FullName;
        string sibling = Directory.CreateDirectory(_temp.File("free")).FullName;

        using (PublicationDirectoryLease.Acquire(directory))
        {
            using (PublicationDirectoryLease.Acquire(sibling, [sibling], OperationDeadline.Start(TimeSpan.FromSeconds(5)))) { }
            using OperationDeadline deadline = OperationDeadline.Start(TimeSpan.FromMilliseconds(300));
            CliException error = Assert.Throws<CliException>(
                () => PublicationDirectoryLease.Acquire(Upper(directory), [Upper(directory)], deadline).Dispose());
            Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
        }

        string journal = Path.Combine(directory, AtomicPublicationPlan.JournalName);
        using (PublicationJournalLock.Acquire(journal))
        {
            // A named mutex is reentrant on its owning thread, so another thread asks.
            bool siblingFree = false;
            bool variantFree = true;
            var other = new Thread(() =>
            {
                using (PublicationJournalLock? free = PublicationJournalLock.TryAcquire(Path.Combine(sibling, AtomicPublicationPlan.JournalName)))
                { siblingFree = free is not null; }
                using (PublicationJournalLock? variant = PublicationJournalLock.TryAcquire(Upper(journal)))
                { variantFree = variant is not null; }
            });
            other.Start();
            other.Join();
            Assert.True(siblingFree);
            Assert.False(variantFree);
        }
    }

    /// <summary>
    /// The boundary checks for a drive root, a trailing separator, a sibling that shares the
    /// prefix (<c>a</c> and <c>ab</c>), a case variant, <c>..</c>, a UNC path and a device path.
    /// </summary>
    [Theory]
    [InlineData("a", @"a\x", true)]
    [InlineData("a", @"A\x", true)]
    [InlineData("a", @"a\b\x", true)]
    [InlineData("a", @"ab\x", false)]
    [InlineData("a", @"x", false)]
    public void Boundaries_ForAPublicationTarget(string directory, string target, bool inside)
    {
        string transaction = Path.Combine(_temp.File(directory), ".aspose-publication-boundary");
        AtomicPublicationPlan plan = AtomicPublicationPlan.Open(transaction, new PublicationJournal { Operation = "boundary" });

        Exception? error = Record.Exception(() => plan.EnsureTarget(_temp.File(target)));

        Assert.Equal(inside, error is null);
    }

    [Fact]
    public void Boundaries_ForADriveRoot_HoldTheFilesBelowIt()
    {
        string drive = Path.GetPathRoot(_temp.Path)!;

        // A publication into the drive root holds its own files.
        AtomicPublicationPlan.Open(Path.Combine(drive, ".aspose-publication-boundary"), new PublicationJournal { Operation = "boundary" })
            .EnsureTarget(Path.Combine(drive, "x.txt"));
    }

    [Theory]
    [InlineData(@"C:\", @"C:\x", true)]
    [InlineData(@"C:\", @"C:\", true)]
    [InlineData(@"C:\a", @"C:\a", true)]
    [InlineData(@"C:\a\", @"C:\a", true)]
    [InlineData(@"C:\a", @"C:\a\", true)]
    [InlineData(@"C:\a", @"C:\a\b", true)]
    [InlineData(@"C:\a\", @"C:\a\b", true)]
    [InlineData(@"C:\a", @"c:\A\b", true)]
    [InlineData(@"C:\a", @"C:\ab", false)]
    [InlineData(@"C:\a", @"C:\", false)]
    public void Boundaries_ForARecoveryRoot(string root, string path, bool within) =>
        Assert.Equal(within, AtomicPublicationRecovery.IsWithinRoot(root, path));

    /// <summary>
    /// The worker's private root and the license snapshot hold every path below them, also when
    /// the root is a drive root or ends in a separator.
    /// </summary>
    [Theory]
    [InlineData(@"C:\a", @"C:\a", true)]
    [InlineData(@"C:\a", @"C:\a\b", true)]
    [InlineData(@"C:\a", @"C:\A\B", true)]
    [InlineData(@"C:\a", @"c:\a", true)]
    [InlineData(@"C:\a", @"C:\ab", false)]
    [InlineData(@"C:\a", @"C:\ab\x", false)]
    [InlineData(@"C:\a", @"C:\AB\x", false)]
    [InlineData(@"C:\a\", @"C:\a\b", true)]
    [InlineData(@"C:\", @"C:\x", true)]
    [InlineData(@"C:\", @"C:\", true)]
    [InlineData(@"C:\", @"D:\x", false)]
    [InlineData(@"\\server\share\a", @"\\server\share\a\x", true)]
    [InlineData(@"\\server\share\a", @"\\server\share\ab", false)]
    public void Boundaries_ForTheWorkerRootAndTheLicenseSnapshot(string root, string path, bool overlaps)
    {
        Assert.Equal(overlaps, Record.Exception(() => WorkerManifestStore.EnsureOutsideRoot(root, path)) is InvalidDataException);
        Assert.Equal(overlaps, Aspose.Cli.Sdk.Licensing.LicenseInstaller.IsInsideSnapshot(root, path));
    }

    [Fact]
    public void Boundaries_ForASiblingThatSharesThePrefix()
    {
        Assert.Equal(_temp.Path, OutputSetPaths.CommonDirectory([_temp.File(@"a\x"), _temp.File(@"ab\y")]));
    }

    [Theory]
    [InlineData(@"C:\", @"C:\x", false, true)]
    [InlineData(@"C:\", @"C:\", true, true)]
    [InlineData(@"C:\", @"C:\", false, false)]
    [InlineData(@"C:\a", @"C:\a\b", false, true)]
    [InlineData(@"C:\a\", @"C:\a\b", false, true)]
    [InlineData(@"C:\a", @"c:\A\b", false, true)]
    [InlineData(@"C:\a", @"C:\A", true, true)]
    [InlineData(@"C:\a", @"C:\a", false, false)]
    [InlineData(@"C:\a", @"C:\ab", true, false)]
    [InlineData(@"C:\a", @"C:\ab\x", true, false)]
    [InlineData(@"C:\a", @"C:\", true, false)]
    [InlineData(@"C:\a\", @"C:\a", true, false)]
    public void FilePaths_IsWithin_TreatsTheRootTheSeparatorAndCaseAsTheBoundariesDo(string root, string path, bool allowEqual, bool within) =>
        Assert.Equal(within, FilePaths.IsWithin(root, path, allowEqual));

    [Theory]
    [InlineData(@"\\server\share\a.txt", true)]
    [InlineData("//server/share/a.txt", true)]
    [InlineData(@"\\?\C:\a.txt", true)]
    [InlineData(@"\\.\PhysicalDrive0", true)]
    [InlineData(@"C:\a.txt", false)]
    [InlineData(@"\a.txt", false)]
    [InlineData("a.txt", false)]
    public void FilePaths_IsRemoteOrDevice_RecognisesUncAndDevicePaths(string path, bool remote) =>
        Assert.Equal(remote, FilePaths.IsRemoteOrDevice(path));

    [Fact]
    public void FilePaths_TwoSpellingsOfOnePath_AreTheSamePath()
    {
        Assert.True(FilePaths.Same(@"C:\Data\Book.xlsx", @"c:\data\BOOK.XLSX"));
        Assert.False(FilePaths.Same(@"C:\Data\Book.xlsx", @"C:\Data\Book.xls"));
        Assert.True(FilePaths.Comparer.Equals(@"C:\Data", @"c:\DATA"));
        Assert.Equal(StringComparison.OrdinalIgnoreCase, FilePaths.Comparison);
    }

    private PlannedOutput WorkerEntry(string target)
    {
        string fullTarget = Path.GetFullPath(target);
        string retained = Directory.CreateDirectory(Path.Combine(_workerRoot, Guid.NewGuid().ToString("N") + "-retained")).FullName;
        string staged = Path.Combine(retained, "output.stage");
        File.WriteAllText(staged, "published");
        return new PlannedOutput
        {
            Target = fullTarget,
            Staged = staged,
            Overwrite = false,
            TargetParentIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(Path.GetDirectoryName(fullTarget)!),
            Original = FilePublicationSnapshot.Capture(fullTarget),
            StagedSnapshot = FilePublicationSnapshot.Capture(staged),
        };
    }
}
