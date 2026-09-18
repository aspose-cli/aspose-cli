using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class WorkerOutputPublicationTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _workerRoot =
        PrivateUserStorage.CreateTemporaryDirectory("worker");

    private string ManifestPath =>
        Path.Combine(_workerRoot, WorkerOutputSession.ManifestName);

    public void Dispose()
    {
        _temp.Dispose();
        PrivateUserStorage.TryDeleteTree(_workerRoot);
    }

    [Fact]
    public void Publish_ChangesTargetsOnlyAfterValidatedParentPublication()
    {
        string target = _temp.File("report.txt");
        string backup = _temp.File("report.backup.txt");
        File.WriteAllText(target, "original");
        WorkerOutputManifest manifest = Manifest(
            Entry(target, "published", backup));
        WorkerManifestStore.Write(ManifestPath, manifest);

        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(File.Exists(backup));

        WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create());

        Assert.Equal("published", File.ReadAllText(target));
        Assert.Equal("original", File.ReadAllText(backup));
        Assert.DoesNotContain("\"state\"", PrivateUserStorage.ReadAllText(ManifestPath), StringComparison.Ordinal);

    }

    [Fact]
    public void Publish_CommitsDeletionAndReplacementThroughOneTransaction()
    {
        string deleted = _temp.File("obsolete.txt");
        string replaced = _temp.File("report.txt");
        File.WriteAllText(deleted, "obsolete");
        File.WriteAllText(replaced, "original");
        WorkerOutputEntry deletion = Entry(deleted, "unused") with { DeleteTarget = true };
        WorkerOutputManifest manifest = Manifest(
            deletion,
            Entry(replaced, "published"));
        WorkerManifestStore.Write(ManifestPath, manifest);

        WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create());

        Assert.False(File.Exists(deleted));
        Assert.Equal("published", File.ReadAllText(replaced));
        Assert.Equal(2, WorkerManifestStore.ReadAndValidate(ManifestPath).Entries.Count);

    }

    [Fact]
    public void PrivateCleanup_RemovesValidatedTree()
    {
        string root = PrivateUserStorage.CreateTemporaryDirectory("worker");
        string child = PrivateUserStorage.EnsureDirectory(
            Path.Combine(root, "child"));
        PrivateUserStorage.WriteAllText(
            Path.Combine(child, "owned.txt"),
            "owned");

        Assert.True(PrivateUserStorage.TryDeleteTree(root));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void PrivateCleanup_PreservesADanglingRootLink()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string link = _temp.File("dangling-private-root");
        try
        {
            Directory.CreateSymbolicLink(
                link,
                _temp.File("missing-private-root"));
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            Assert.False(PrivateUserStorage.TryDeleteTree(link));
            Assert.True(
                FilePublicationOwnedDelete.TryGetAttributesNoFollow(link)
                    ?.HasFlag(FileAttributes.ReparsePoint));
        }
        finally
        {
            Directory.Delete(link, recursive: false);
        }
    }

    [Fact]
    public void Publish_LaterDirectoryConflict_RestoresEarlierDirectory()
    {
        string first = _temp.File(Path.Combine("a", "first.txt"));
        string second = _temp.File(Path.Combine("b", "second.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        File.WriteAllText(first, "first-original");
        File.WriteAllText(second, "second-original");
        WorkerOutputManifest manifest = Manifest(
            Entry(first, "first-published"),
            Entry(second, "second-published"));
        WorkerManifestStore.Write(ManifestPath, manifest);
        File.WriteAllText(second, "external-change");

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));

        Assert.Equal(ErrorCodes.OutputConflict, error.Code);
        Assert.Equal("first-original", File.ReadAllText(first));
        Assert.Equal("external-change", File.ReadAllText(second));
    }

    [Fact]
    public void Publish_SameContentStageReplacement_IsRejected()
    {
        string target = _temp.File("report.txt");
        File.WriteAllText(target, "original");
        WorkerOutputManifest manifest = Manifest(Entry(target, "published"));
        WorkerManifestStore.Write(ManifestPath, manifest);
        string staged = manifest.Entries[0].Staged;
        string replacement = Path.Combine(
            Path.GetDirectoryName(staged)!,
            "replacement.tmp");
        string displaced = Path.Combine(
            Path.GetDirectoryName(staged)!,
            "displaced.tmp");
        File.WriteAllText(replacement, "published");
        PrivateUserStorage.ProtectFile(replacement);
        File.Replace(replacement, staged, displaced);

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Publish_UndeclaredParentDirectory_IsRejectedWithoutCreation()
    {
        string parent = _temp.File("undeclared");
        string target = Path.Combine(parent, "report.txt");
        WorkerManifestStore.Write(
            ManifestPath,
            Manifest(Entry(target, "published")));

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.False(Directory.Exists(parent));
    }

    [Fact]
    public void Publish_FailureRemovesOnlyTheCreatedDirectoryIdentity()
    {
        string createdParent = _temp.File("created");
        string first = Path.Combine(createdParent, "first.txt");
        string second = _temp.File("second.txt");
        WorkerOutputManifest manifest = Manifest(
            Entry(first, "first-published"),
            Entry(second, "second-published"));
        manifest = manifest with { Directories = [new WorkerDirectoryEntry(createdParent, false, null)] };
        WorkerManifestStore.Write(ManifestPath, manifest);
        File.WriteAllText(second, "external-change");

        Assert.Throws<CliException>(() =>
            WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));

        Assert.False(Directory.Exists(createdParent));
        Assert.Equal("external-change", File.ReadAllText(second));
    }

    [Fact]
    public void Publish_SameContentExternalReplacementIsPreserved()
    {
        string target = _temp.File("report.txt");
        File.WriteAllText(target, "original");
        WorkerOutputManifest manifest = Manifest(Entry(target, "published"));
        WorkerManifestStore.Write(ManifestPath, manifest);
        string external = _temp.File("external.txt");
        File.WriteAllText(external, "original");
        File.Replace(external, target, _temp.File("displaced.txt"));
        Assert.Throws<CliException>(() => WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Publish_InputChangeIsRejectedBeforeAnyTargetChanges()
    {
        string input = _temp.File("source.txt");
        File.WriteAllText(input, "source");
        string target = _temp.File("report.txt");
        WorkerOutputEntry entry = Entry(target, "published") with
        { InputPath = input, InputSnapshot = FilePublicationSnapshot.Capture(input) };
        WorkerManifestStore.Write(ManifestPath, Manifest(entry));
        File.WriteAllText(input, "external");
        CliException error = Assert.Throws<CliException>(() => WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));
        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Publish_DeadlineOrCancellationCannotBypassFinalPublication(bool expire)
    {
        string target = _temp.File("report.txt");
        WorkerManifestStore.Write(ManifestPath, Manifest(Entry(target, "published")));
        using var cancelled = new CancellationTokenSource();
        using OperationDeadline deadline = expire
            ? OperationDeadline.FromAbsoluteTick(TimeSpan.FromSeconds(1), Environment.TickCount64 - 1)
            : OperationDeadline.Start(null, cancelled.Token);
        if (!expire) { cancelled.Cancel(); }
        Exception? error = Record.Exception(() => WorkerOutputSession.Publish(ManifestPath, new ResourceBudgetLedger(deadline)));
        if (expire) { Assert.Equal(ErrorCodes.OperationTimeout, Assert.IsType<CliException>(error).Code); }
        else { Assert.IsAssignableFrom<OperationCanceledException>(error); }
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("oversized")]
    public void Publish_MalformedManifest_IsAStableOutputError(string corruption)
    {
        string target = _temp.File("report.txt");
        WorkerOutputManifest manifest = Manifest(Entry(target, "published"));
        WorkerManifestStore.Write(ManifestPath, manifest);
        string json = PrivateUserStorage.ReadAllText(ManifestPath);
        json = corruption switch
        {
            "duplicate" => json.Replace(
                "\"version\":3,",
                "\"version\":3,\r\n  \"version\":3,",
                StringComparison.Ordinal),
            "unknown" => json.Replace(
                "\"version\":3,",
                "\"version\":3,\r\n  \"unknown\": true,",
                StringComparison.Ordinal),
            _ => json + new string(' ', PublicationLimits.MaximumMetadataBytes),
        };
        PrivateUserStorage.WriteAllText(ManifestPath, json);

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath, TestBudgets.Create()));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal(
            "worker-manifest",
            error.Details!["phase"]!.GetValue<string>());
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentCommitFailureRestoresEarlierOutputsAndDeletion(bool cancel)
    {
        string first = _temp.File("first.txt");
        string second = _temp.File("second.txt");
        File.WriteAllText(first, "original");
        File.WriteAllText(second, "obsolete");
        WorkerManifestStore.Write(ManifestPath, Manifest(Entry(first, "published"),
            Entry(second, "unused") with { DeleteTarget = true }));
        using var cancellation = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancellation.Token);
        var faults = new Callback(point =>
        {
            if (point.Kind != PublicationFaultKind.Publish || point.EntryIndex != 1) { return; }
            if (cancel) { cancellation.Cancel(); }
            else { throw new IOException("Injected second output failure."); }
        });
        Exception? error = Record.Exception(() => WorkerOutputPublisher.Publish(
            WorkerManifestStore.ReadAndValidate(ManifestPath), new ResourceBudgetLedger(deadline), faults));
        if (cancel) { Assert.IsAssignableFrom<OperationCanceledException>(error); }
        else { Assert.IsType<CliException>(error); }
        Assert.Equal("original", File.ReadAllText(first));
        Assert.Equal("obsolete", File.ReadAllText(second));
    }

    [Fact]
    public void CancellationAfterDurableCommitDoesNotUndoPublishedData()
    {
        string target = _temp.File("report.txt");
        WorkerManifestStore.Write(ManifestPath, Manifest(Entry(target, "published")));
        using var cancellation = new CancellationTokenSource();
        using var deadline = OperationDeadline.Start(null, cancellation.Token);
        var budgets = new ResourceBudgetLedger(deadline);
        WorkerOutputPublisher.Publish(WorkerManifestStore.ReadAndValidate(ManifestPath), budgets,
            new Callback(point => { if (point.Kind == PublicationFaultKind.Cleanup) { cancellation.Cancel(); } }));
        Assert.True(budgets.HasCommittedOutputs);
        Assert.Equal("published", File.ReadAllText(target));
    }

    private sealed class Callback(Action<PublicationFaultPoint> action) : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point) => action(point);
    }

    private WorkerOutputManifest Manifest(params WorkerOutputEntry[] entries) =>
        new() { Entries = [.. entries], Sealed = true };

    private WorkerOutputEntry Entry(
        string target,
        string content,
        string? backupPath = null)
    {
        string fullTarget = Path.GetFullPath(target);
        FilePublicationSnapshot original =
            FilePublicationSnapshot.Capture(fullTarget);
        string retained = PrivateUserStorage.EnsureDirectory(Path.Combine(
            _workerRoot,
            Guid.NewGuid().ToString("N") + "-retained"));
        string staged = Path.Combine(retained, "output.stage");
        File.WriteAllText(staged, content);
        PrivateUserStorage.ProtectFile(staged);
        FilePublicationSnapshot stagedSnapshot =
            FilePublicationSnapshot.Capture(staged);
        string? fullBackup = backupPath is null
            ? null
            : Path.GetFullPath(backupPath);
        return new WorkerOutputEntry
        {
            Target = fullTarget,
            Staged = staged,
            Overwrite = original.Exists,
            BackupPath = fullBackup,
            BackupOriginal = fullBackup is null || !original.Exists
                ? null
                : FilePublicationSnapshot.Capture(fullBackup),
            TargetParentIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(Path.GetDirectoryName(fullTarget)!),
            Original = original,
            StagedSnapshot = stagedSnapshot,
        };
    }
}
