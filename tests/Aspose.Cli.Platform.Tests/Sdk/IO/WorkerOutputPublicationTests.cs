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
        WorkerOutputSession.CreateRoot();

    private string ManifestPath =>
        Path.Combine(_workerRoot, WorkerOutputSession.ManifestName);

    public void Dispose()
    {
        _temp.Dispose();
        UserStorage.TryDeleteTree(_workerRoot);
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

        WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create());

        Assert.Equal("published", File.ReadAllText(target));
        Assert.Equal("original", File.ReadAllText(backup));
        Assert.DoesNotContain("\"state\"", File.ReadAllText(ManifestPath), StringComparison.Ordinal);

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

        WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create());

        Assert.False(File.Exists(deleted));
        Assert.Equal("published", File.ReadAllText(replaced));
        Assert.Equal(2, WorkerManifestStore.ReadAndValidate(ManifestPath).Entries.Count);

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
            () => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));

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
        File.Replace(replacement, staged, displaced);

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));

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
            () => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));

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
            WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));

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
        Assert.Throws<CliException>(() => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));
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
        CliException error = Assert.Throws<CliException>(() => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));
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
        Exception? error = Record.Exception(() => WorkerOutputPublisher.Publish(ManifestPath, new ResourceBudgetLedger(deadline)));
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
        string json = File.ReadAllText(ManifestPath);
        string version = $"\"version\":{manifest.Version},";
        json = corruption switch
        {
            "duplicate" => json.Replace(version, version + "\r\n  " + version, StringComparison.Ordinal),
            "unknown" => json.Replace(version, version + "\r\n  \"unknown\": true,", StringComparison.Ordinal),
            _ => json + new string(' ', PublicationLimits.MaximumMetadataBytes),
        };
        File.WriteAllText(ManifestPath, json);

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));

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

    [Theory]
    [InlineData("not-an-ancestor")]
    [InlineData("relative")]
    [InlineData("without-a-directory-output")]
    public void ReadAndValidate_RefusesAnAlteredDirectoryTransactionParent(string alteration)
    {
        string target = _temp.File(Path.Combine("root", "result"));
        WorkerOutputManifest manifest;
        if (alteration == "without-a-directory-output")
        {
            manifest = Manifest(Entry(_temp.File("report.txt"), "published")) with { DirectoryTransactionParent = _temp.Path };
        }
        else
        {
            var worker = new WorkerOutputSession(_workerRoot, ManifestPath);
            using OperationDeadline deadline = OperationDeadline.Start(null);
            using (var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(deadline, outputSession: worker), target, "test",
                transactionParent: _temp.Path))
            {
                File.WriteAllText(Path.Combine(output.StagingDirectory, "data.txt"), "candidate");
                output.Commit();
            }
            worker.SealForPublication();
            // The manifest the worker sealed is valid; only the alteration makes it invalid.
            manifest = WorkerManifestStore.ReadAndValidate(ManifestPath);
            Assert.Equal(_temp.Path, manifest.DirectoryTransactionParent);
            manifest = manifest with
            {
                DirectoryTransactionParent = alteration == "relative" ? "root" : _temp.File("elsewhere"),
            };
        }
        WorkerManifestStore.Write(ManifestPath, manifest);

        CliException error = Assert.Throws<CliException>(() => WorkerManifestStore.ReadAndValidate(ManifestPath));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("worker-manifest", error.Details!["phase"]!.GetValue<string>());
        Assert.False(Directory.Exists(target));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Worker_ReplaceOfATargetThatChangedAfterTheWorkerSealed_IsRefusedAndKeepsIt(bool existed)
    {
        string target = _temp.File(Path.Combine("root", "result"));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (existed)
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "admitted.txt"), "admitted");
        }
        var admitted = new List<string>();
        var worker = new WorkerOutputSession(_workerRoot, ManifestPath);
        using (OperationDeadline deadline = OperationDeadline.Start(null))
        using (var output = new AtomicNewDirectoryWriter(new ResourceBudgetLedger(deadline, outputSession: worker), target, "test",
            replace: true, transactionParent: _temp.Path))
        {
            File.WriteAllText(Path.Combine(output.StagingDirectory, "data.txt"), "candidate");
            output.Commit(admitted.Add);
        }
        worker.SealForPublication();
        Assert.Equal([target], admitted);

        // After the worker admitted the target, another program puts a tree of its own there.
        if (existed) { Directory.Delete(target, recursive: true); }
        Directory.CreateDirectory(Path.Combine(target, "own"));
        File.WriteAllText(Path.Combine(target, "own", "notes.txt"), "keep");

        CliException error = Assert.Throws<CliException>(() => WorkerOutputPublisher.Publish(ManifestPath, TestBudgets.Create()));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal(["own"], Directory.EnumerateFileSystemEntries(target).Select(Path.GetFileName));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "own", "notes.txt")));
        Assert.Empty(Directory.EnumerateDirectories(_temp.Path, ".aspose-publication-*"));
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
        string retained = Directory.CreateDirectory(Path.Combine(
            _workerRoot,
            Guid.NewGuid().ToString("N") + "-retained")).FullName;
        string staged = Path.Combine(retained, "output.stage");
        File.WriteAllText(staged, content);
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
