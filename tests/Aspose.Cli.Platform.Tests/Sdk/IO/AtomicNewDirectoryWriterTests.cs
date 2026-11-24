using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
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
        var faults = new Faults(point => { if (point.Kind == PublicationFaultKind.Publish) { cancellation.Cancel(); } });
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(cancellation: cancellation.Token), target, "test", faults))
        {
            Write(output, "data.txt", "candidate");
            Assert.Throws<OperationCanceledException>(output.Commit);
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
    }

    [Fact]
    public void Commit_CancellationAndCleanupFailureAfterRenameDoNotReportFailure()
    {
        using var temp = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        var budgets = new ResourceBudgetLedger(cancellation: cancellation.Token);
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Cleanup)
            { cancellation.Cancel(); throw new IOException("cleanup fault after rename"); }
        });
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(budgets, target, "test", faults))
        { Write(output, "data.txt", "committed"); output.Commit(); }
        Assert.False(budgets.Cancellation.IsCancellationRequested);
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
    public void Commit_ReplaceSwapsTheExistingDirectoryForTheCandidateAndDeletesIt()
    {
        using var temp = new TempDirectory();
        string target = Existing(temp);
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true))
        {
            Write(output, "data.txt", "replacement");
            output.Commit();
        }
        Assert.Equal("replacement", File.ReadAllText(Path.Combine(target, "data.txt")));
        Assert.False(File.Exists(Path.Combine(target, "old.txt")));
        Assert.False(Directory.Exists(Path.Combine(target, "parts")));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Commit_ReplaceOfADirectoryWithAReadOnlyFileCleansUpItsTransaction()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string target = Existing(temp);
        string readOnly = Path.Combine(target, "parts", "page.txt");
        File.SetAttributes(readOnly, File.GetAttributes(readOnly) | FileAttributes.ReadOnly);
        try
        {
            using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true))
            {
                Write(output, "data.txt", "replacement");
                output.Commit();
            }
            // The replaced tree, read-only file included, goes with its transaction, so no
            // committed transaction is left for the next publication's recovery to finish.
            Assert.Equal(["result"], Directory.EnumerateFileSystemEntries(temp.Path).Select(Path.GetFileName));
            using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true))
            {
                Write(output, "data.txt", "again");
                output.Commit();
            }
            Assert.Equal("again", File.ReadAllText(Path.Combine(target, "data.txt")));
            Assert.Equal(["result"], Directory.EnumerateFileSystemEntries(temp.Path).Select(Path.GetFileName));
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(temp.Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }
        }
    }

    [Fact]
    public void Commit_ReplaceOfAnAbsentTargetPublishesANewDirectory()
    {
        using var temp = new TempDirectory();
        string target = temp.File("result");
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true))
        {
            Write(output, "data.txt", "new");
            output.Commit();
        }
        Assert.Equal("new", File.ReadAllText(Path.Combine(target, "data.txt")));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Constructor_WithoutReplaceRefusesAnExistingDirectory()
    {
        using var temp = new TempDirectory();
        string target = Existing(temp);
        CliException error = Assert.Throws<CliException>(() => new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test"));
        Assert.Equal(ErrorCodes.OutputExists, error.Code);
        AssertPrevious(target);
    }

    [Fact]
    public void Constructor_ReplaceRefusesAFileAtTheTarget()
    {
        using var temp = new TempDirectory();
        string target = temp.File("result");
        File.WriteAllText(target, "a file");
        Assert.Throws<CliException>(() => new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true));
        Assert.Equal("a file", File.ReadAllText(target));
    }

    [Fact]
    public void Commit_ReplaceThatFailsAfterMovingThePreviousDirectoryPutsItBack()
    {
        using var temp = new TempDirectory();
        string target = Existing(temp);
        var faults = new Faults(point =>
        {
            if (point is { Kind: PublicationFaultKind.Publish, EntryIndex: 1 })
            {
                Assert.False(Directory.Exists(target));
                throw new IOException("after the previous directory moved");
            }
        });
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", faults, replace: true))
        {
            Write(output, "data.txt", "replacement");
            Assert.Throws<IOException>(output.Commit);
        }
        AssertPrevious(target);
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Commit_ReplaceThatCannotMoveThePreviousDirectoryBack_ReportsWhereItIs()
    {
        using var temp = new TempDirectory();
        string target = Existing(temp);
        // Another owner takes the emptied target, so neither the candidate nor the previous
        // directory can move there and the publication stops with the previous one displaced.
        var faults = new Faults(point =>
        {
            if (point is { Kind: PublicationFaultKind.Publish, EntryIndex: 1 })
            {
                Directory.CreateDirectory(target);
            }
        });
        string transaction;
        CliException error;
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", faults, replace: true))
        {
            Write(output, "data.txt", "replacement");
            error = Assert.Throws<CliException>(output.Commit);
            transaction = Path.GetDirectoryName(output.StagingDirectory)!;
        }
        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        string previous = error.Details!["originalPath"]!.GetValue<string>();
        Assert.Equal(Path.Combine(transaction, "previous"), previous);
        AssertPrevious(previous);
    }

    [Fact]
    public void Commit_WithATransactionParentStagesNothingInTheTargetsParent()
    {
        using var temp = new TempDirectory();
        string root = temp.File("root");
        string target = Existing(Path.Combine(root, "result"));
        var faults = new Faults(point =>
        {
            if (point is { Kind: PublicationFaultKind.Publish, EntryIndex: 0 })
            {
                Assert.Equal(["result"], Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName));
                Assert.Single(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
            }
        });
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", faults, replace: true, transactionParent: temp.Path))
        {
            Write(output, "data.txt", "replacement");
            output.Commit();
        }
        Assert.Equal("replacement", File.ReadAllText(Path.Combine(target, "data.txt")));
        Assert.Equal(["result"], Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Commit_WhoseAdmittedTargetChangesBeforeTheReplace_IsRefusedAndKeepsTheChange()
    {
        using var temp = new TempDirectory();
        string target = Existing(temp);
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true))
        {
            Write(output, "data.txt", "replacement");
            // The admission runs after the replaced tree is captured, so a change it lets in is seen.
            Assert.ThrowsAny<IOException>(() => output.Commit(admitted =>
            {
                Assert.Equal(target, admitted);
                File.WriteAllText(Path.Combine(admitted, "added.txt"), "added");
            }));
        }
        Assert.Equal("added", File.ReadAllText(Path.Combine(target, "added.txt")));
        AssertPrevious(target);
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Commit_WhoseAdmissionRefusesTheTarget_KeepsIt()
    {
        using var temp = new TempDirectory();
        string target = Existing(temp);
        using (var output = new AtomicNewDirectoryWriter(TestBudgets.Create(), target, "test", replace: true))
        {
            Write(output, "data.txt", "replacement");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => output.Commit(_ => throw new InvalidOperationException("not ours")));
            Assert.Equal("not ours", error.Message);
        }
        AssertPrevious(target);
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Constructor_RefusesATransactionParentThatIsNotAnAncestorOfTheTarget()
    {
        using var temp = new TempDirectory();
        string target = temp.File(Path.Combine("root", "result"));
        Assert.Throws<ArgumentException>(() => new AtomicNewDirectoryWriter(
            TestBudgets.Create(), target, "test", transactionParent: temp.File("elsewhere")));
        Assert.Throws<ArgumentException>(() => new AtomicNewDirectoryWriter(
            TestBudgets.Create(), target, "test", transactionParent: target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
    }

    // A directory an earlier publication left at the target: a file and a nested one.
    private static string Existing(TempDirectory temp) => Existing(temp.File("result"));

    private static string Existing(string target)
    {
        Directory.CreateDirectory(Path.Combine(target, "parts"));
        File.WriteAllText(Path.Combine(target, "old.txt"), "old");
        File.WriteAllText(Path.Combine(target, "parts", "page.txt"), "old page");
        return target;
    }

    private static void AssertPrevious(string target)
    {
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "old.txt")));
        Assert.Equal("old page", File.ReadAllText(Path.Combine(target, "parts", "page.txt")));
        Assert.False(File.Exists(Path.Combine(target, "data.txt")));
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