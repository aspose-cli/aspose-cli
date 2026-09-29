using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class AtomicNewDirectoryWriterTests
{
    [Fact]
    public void Commit_PublishesTheCompleteDirectoryAtOneRename()
    {
        using var temp = new TempDirectory();
        string target = temp.File("result");
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish) { Assert.False(Directory.Exists(target)); }
        });
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", faults))
        {
            Write(output, "index.html", "index");
            Write(output, Path.Combine("parts", "page.txt"), "page");
            Assert.False(Directory.Exists(target));
            output.Commit();
        }
        Assert.Equal("index", File.ReadAllText(Path.Combine(target, "index.html")));
        Assert.Equal("page", File.ReadAllText(Path.Combine(target, "parts", "page.txt")));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Dispose_UncommittedCandidateReclaimsOnlyItsOwnedDirectories()
    {
        using var temp = new TempDirectory();
        string target = temp.File(Path.Combine("created", "result"));
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test"))
        { Write(output, "data.txt", "candidate"); }
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
    }

    [Fact]
    public void Commit_ConcurrentTargetCreationPreservesTheOtherOwnersTree()
    {
        using var temp = new TempDirectory();
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test"))
        {
            Write(output, "data.txt", "candidate");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "owned.txt"), "other owner");
            CliException error = Assert.Throws<CliException>(output.Commit);
            Assert.Equal(ErrorCodes.OutputExists, error.Code);
        }
        Assert.Equal("other owner", File.ReadAllText(Path.Combine(target, "owned.txt")));
        Assert.Single(Directory.EnumerateFileSystemEntries(target));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Commit_CancellationBeforeRenameLeavesNoTargetOrCandidate()
    {
        using var temp = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        using OperationDeadline deadline = OperationDeadline.Start(null, cancellation.Token);
        var faults = new Faults(point => { if (point.Kind == PublicationFaultKind.Publish) { cancellation.Cancel(); } });
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(deadline), target, "test", faults))
        {
            Write(output, "data.txt", "candidate");
            Assert.Throws<OperationCanceledException>(output.Commit);
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
    }

    [Fact]
    public void Commit_CancellationAndJournalFailureAfterRenameDoNotReportFailure()
    {
        using var temp = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        using OperationDeadline deadline = OperationDeadline.Start(null, cancellation.Token);
        var budgets = new ResourceBudgetLedger(deadline);
        int writes = 0;
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.JournalWrite && ++writes == 3)
            { cancellation.Cancel(); throw new IOException("journal fault after rename"); }
        });
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(budgets, target, "test", faults))
        { Write(output, "data.txt", "committed"); output.Commit(); }
        Assert.True(budgets.HasCommittedOutputs);
        Assert.Equal("committed", File.ReadAllText(Path.Combine(target, "data.txt")));
    }

    [Fact]
    public void Commit_OutputBudgetFailureCleansUnpublishedCandidate()
    {
        using var temp = new TempDirectory();
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(
            new Dictionary<string, long> { [ResourceBudgetKinds.OutputBytes] = 3 }), temp.File("result"), "test"))
        {
            Write(output, "data.txt", "too large");
            Assert.Throws<CliException>(output.Commit);
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
    }

    [Fact]
    public void Worker_RetainsCandidateUntilParentPublishesAndThenReclaimsItsStorage()
    {
        using var temp = new TempDirectory();
        string workerRoot = UserStorage.CreateTemporaryDirectory("worker");
        try
        {
            string manifest = Path.Combine(workerRoot, WorkerOutputSession.ManifestName);
            var worker = new WorkerOutputSession(workerRoot, manifest);
            using OperationDeadline deadline = OperationDeadline.Start(null);
            string target = temp.File(Path.Combine("new-parent", "result"));
            using (var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(deadline, outputSession: worker), target, "test"))
            {
                Assert.StartsWith(workerRoot + Path.DirectorySeparatorChar, output.StagingDirectory, StringComparison.OrdinalIgnoreCase);
                Write(output, Path.Combine("parts", "data.txt"), "complete");
                output.Commit();
            }
            worker.SealForPublication();
            Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
            WorkerOutputSession.Publish(manifest, TestBudgets.Create());
            Assert.Equal("complete", File.ReadAllText(Path.Combine(target, "parts", "data.txt")));
            Assert.True(UserStorage.TryDeleteTree(workerRoot));
        }
        finally { UserStorage.TryDeleteTree(workerRoot); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Worker_RejectsChangedOrExtraCandidateFiles(bool extra)
    {
        using var temp = new TempDirectory();
        string workerRoot = UserStorage.CreateTemporaryDirectory("worker");
        try
        {
            string manifest = Path.Combine(workerRoot, WorkerOutputSession.ManifestName);
            var worker = new WorkerOutputSession(workerRoot, manifest);
            using OperationDeadline deadline = OperationDeadline.Start(null);
            string target = temp.File("result");
            using var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(deadline, outputSession: worker), target, "test");
            Write(output, "data.txt", "original");
            output.Commit();
            worker.SealForPublication();
            Write(output, extra ? "unknown.txt" : "data.txt", "changed");
            Assert.Throws<CliException>(() => WorkerOutputSession.Publish(manifest, TestBudgets.Create()));
            Assert.False(Directory.Exists(target));
        }
        finally { UserStorage.TryDeleteTree(workerRoot); }
    }

    [Fact]
    public void Worker_RejectsMixedDirectoryAndFileOutputSets()
    {
        using var temp = new TempDirectory();
        string workerRoot = UserStorage.CreateTemporaryDirectory("worker");
        try
        {
            var worker = new WorkerOutputSession(workerRoot, Path.Combine(workerRoot, WorkerOutputSession.ManifestName));
            using OperationDeadline deadline = OperationDeadline.Start(null);
            var budgets = new ResourceBudgetLedger(deadline, outputSession: worker);
            new SafeFileWriter(budgets).Write(temp.File("file.txt"), false, path => File.WriteAllText(path, "file"));
            using var output = new AtomicNewDirectoryWriter(budgets, temp.File("directory"), "test");
            Write(output, "data.txt", "candidate");
            Assert.Throws<IOException>(output.Commit);
            Assert.False(File.Exists(temp.File("file.txt")));
            Assert.False(Directory.Exists(temp.File("directory")));
        }
        finally { UserStorage.TryDeleteTree(workerRoot); }
    }

    [Fact]
    public void Commit_UnknownFileAddedAfterSealingIsPreservedInsteadOfPublishedOrDeleted()
    {
        using var temp = new TempDirectory();
        string? staging = null;
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish) { File.WriteAllText(Path.Combine(staging!, "unknown.txt"), "preserve"); }
        });
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", faults))
        {
            staging = output.StagingDirectory;
            Write(output, "data.txt", "candidate");
            Assert.Throws<IOException>(output.Commit);
        }
        Assert.False(Directory.Exists(target));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(staging!, "unknown.txt")));
    }

    [Fact]
    public void RecoverPending_ReclaimsVerifiedAbandonedDirectoryCandidate()
    {
        using var temp = new TempDirectory();
        using var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), temp.File("result"), "test",
            new Faults(point => { if (point.Kind == PublicationFaultKind.Publish) { throw new IOException("before rename"); } }));
        Write(output, "data.txt", "candidate");
        Assert.Throws<IOException>(output.Commit);
        string transaction = Path.GetDirectoryName(output.StagingDirectory)!;
        string journalPath = Path.Combine(transaction, AtomicPublicationPlan.JournalName);
        PublicationJournal current = PublicationJournal.Read(journalPath);
        new PublicationJournal
        {
            Operation = current.Operation, State = current.State, OwnerProcessId = int.MaxValue,
            OwnerProcessStartUtcTicks = 1, DirectoryOutput = current.DirectoryOutput,
        }.Write(journalPath);
        Assert.Equal(1, AtomicOutputSetWriter.RecoverPending(temp.Path));
        Assert.False(Directory.Exists(transaction));
        Assert.False(Directory.Exists(output.TargetDirectory));
    }

    [Fact]
    public void Worker_RejectsAReplacedOutputParentAndPreservesTheNewOwnersFiles()
    {
        using var temp = new TempDirectory();
        string parent = temp.File("parent");
        Directory.CreateDirectory(parent);
        string workerRoot = UserStorage.CreateTemporaryDirectory("worker");
        try
        {
            string manifest = Path.Combine(workerRoot, WorkerOutputSession.ManifestName);
            var worker = new WorkerOutputSession(workerRoot, manifest);
            using OperationDeadline deadline = OperationDeadline.Start(null);
            using (var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(deadline, outputSession: worker), Path.Combine(parent, "result"), "test"))
            { Write(output, "data.txt", "candidate"); output.Commit(); }
            worker.SealForPublication();
            Directory.Move(parent, temp.File("original-parent"));
            Directory.CreateDirectory(parent);
            File.WriteAllText(Path.Combine(parent, "owner.txt"), "preserve");
            if (OperatingSystem.IsWindows())
            {
                Assert.Throws<CliException>(() => WorkerOutputSession.Publish(manifest, TestBudgets.Create()));
                Assert.False(Directory.Exists(Path.Combine(parent, "result")));
            }
            Assert.Equal("preserve", File.ReadAllText(Path.Combine(parent, "owner.txt")));
        }
        finally { UserStorage.TryDeleteTree(workerRoot); }
    }

    [Fact]
    public void Commit_RejectsALinkWithoutDeletingItsTarget()
    {
        using var temp = new TempDirectory();
        string source = temp.File("outside.txt");
        File.WriteAllText(source, "preserve");
        using var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), temp.File("result"), "test");
        string link = Path.Combine(output.StagingDirectory, "link.txt");
        FileSystemLinks.CreateFileSymbolicLink(link, source);
        Assert.Throws<InvalidDataException>(output.Commit);
        Assert.False(Directory.Exists(output.TargetDirectory));
        Assert.Equal("preserve", File.ReadAllText(source));
    }

    [Fact]
    public void WorkerCleanup_EntryCapCoversTheLargestDirectoryOutputTree() =>
        Assert.True(UserStorage.MaximumCleanupEntries
            >= NewDirectoryPublication.MaximumFiles + PublicationLimits.MaximumDirectories);

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Commit_NewWindowsDirectoryInheritsItsParentAcl()
    {
        Requires.Windows();

        VerifyNewWindowsDirectoryInheritsItsParentAcl();
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyNewWindowsDirectoryInheritsItsParentAcl()
    {
        using var temp = new TempDirectory();
        string parent = Directory.CreateDirectory(temp.File("shared")).FullName;
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var info = new DirectoryInfo(parent);
        DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);
        // Reaches only direct children, so the staging directory one level deeper lacks it.
        security.AddAccessRule(new FileSystemAccessRule(
            users,
            FileSystemRights.ReadData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.NoPropagateInherit,
            AccessControlType.Allow));
        info.SetAccessControl(security);
        string target = Path.Combine(parent, "result");

        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test"))
        {
            Write(output, "index.html", "index");
            Assert.DoesNotContain(Rules(output.StagingDirectory), rule => rule.IdentityReference.Equals(users));
            output.Commit();
        }

        FileSystemAccessRule[] rules = Rules(target);
        Assert.Contains(rules, rule => rule.IdentityReference.Equals(users)
            && rule.IsInherited
            && rule.AccessControlType == AccessControlType.Allow
            && rule.FileSystemRights.HasFlag(FileSystemRights.ReadData));
        Assert.DoesNotContain(rules, rule => !rule.IsInherited);
        Assert.Equal("index", File.ReadAllText(Path.Combine(target, "index.html")));
    }

    [SupportedOSPlatform("windows")]
    private static FileSystemAccessRule[] Rules(string directory) =>
        new DirectoryInfo(directory).GetAccessControl(AccessControlSections.Access)
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .ToArray();

    private static void Write(AtomicNewDirectoryWriter output, string relative, string contents)
    {
        string path = Path.Combine(output.StagingDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private sealed class Faults(Action<PublicationFaultPoint> action) : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point) => action(point);
    }
}