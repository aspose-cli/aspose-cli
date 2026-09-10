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
        Path.Combine(_workerRoot, "output-manifest.v1.json");

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
        WorkerOutputSession.WriteManifest(ManifestPath, manifest);

        Assert.Equal("original", File.ReadAllText(target));
        Assert.False(File.Exists(backup));

        WorkerOutputSession.Publish(ManifestPath);

        Assert.Equal("published", File.ReadAllText(target));
        Assert.Equal("original", File.ReadAllText(backup));
        Assert.Contains(
            "\"state\": 2",
            PrivateUserStorage.ReadAllText(ManifestPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Publish_CommitsDeletionAndReplacementThroughOneTransaction()
    {
        string deleted = _temp.File("obsolete.txt");
        string replaced = _temp.File("report.txt");
        File.WriteAllText(deleted, "obsolete");
        File.WriteAllText(replaced, "original");
        WorkerOutputEntry deletion = Entry(deleted, "unused");
        deletion.DeleteTarget = true;
        WorkerOutputManifest manifest = Manifest(
            deletion,
            Entry(replaced, "published"));
        WorkerOutputSession.WriteManifest(ManifestPath, manifest);

        WorkerOutputSession.Publish(ManifestPath);

        Assert.False(File.Exists(deleted));
        Assert.Equal("published", File.ReadAllText(replaced));
        WorkerOutputManifest published =
            WorkerManifestStore.ReadAndValidate(ManifestPath);
        Assert.All(
            published.Entries,
            entry => Assert.Equal(
                WorkerPublicationState.Published,
                entry.State));
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
        WorkerOutputSession.WriteManifest(ManifestPath, manifest);
        File.WriteAllText(second, "external-change");

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath));

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.Equal("first-original", File.ReadAllText(first));
        Assert.Equal("external-change", File.ReadAllText(second));
    }

    [Fact]
    public void Publish_SameContentStageReplacement_IsRejected()
    {
        string target = _temp.File("report.txt");
        File.WriteAllText(target, "original");
        WorkerOutputManifest manifest = Manifest(Entry(target, "published"));
        WorkerOutputSession.WriteManifest(ManifestPath, manifest);
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
            () => WorkerOutputSession.Publish(ManifestPath));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Publish_UndeclaredParentDirectory_IsRejectedWithoutCreation()
    {
        string parent = _temp.File("undeclared");
        string target = Path.Combine(parent, "report.txt");
        WorkerOutputSession.WriteManifest(
            ManifestPath,
            Manifest(Entry(target, "published")));

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath));

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
        manifest.Directories.Add(new WorkerDirectoryEntry
        {
            Target = createdParent,
            Existed = false,
        });
        WorkerOutputSession.WriteManifest(ManifestPath, manifest);
        File.WriteAllText(second, "external-change");

        Assert.Throws<CliException>(() =>
            WorkerOutputSession.Publish(ManifestPath));

        Assert.False(Directory.Exists(createdParent));
        Assert.Equal("external-change", File.ReadAllText(second));
    }

    [Fact]
    public void Recovery_DoesNotClaimSameContentExternalReplacement()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string target = _temp.File("report.txt");
        File.WriteAllText(target, "original");
        WorkerOutputEntry entry = Entry(target, "published");
        File.WriteAllText(target, "published");
        entry.PublishedSnapshot = FilePublicationSnapshot.Capture(target);
        entry.State = WorkerPublicationState.Published;
        string external = _temp.File("external.txt");
        string displaced = _temp.File("displaced.txt");
        File.WriteAllText(external, "original");
        File.Replace(external, target, displaced);

        PublicationRecoveryReport recovery =
            WorkerOutputRecovery.Restore(Manifest(entry));

        Assert.False(recovery.RecoveryComplete);
        Assert.Equal("unknown", Assert.Single(recovery.Items).Status);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Recovery_StagedEntryReportsAnExternalTargetChange()
    {
        string target = _temp.File("report.txt");
        File.WriteAllText(target, "original");
        WorkerOutputEntry entry = Entry(target, "published");
        File.WriteAllText(target, "external-change");

        PublicationRecoveryReport recovery =
            WorkerOutputRecovery.Restore(Manifest(entry));

        Assert.False(recovery.RecoveryComplete);
        Assert.Equal("unknown", Assert.Single(recovery.Items).Status);
        Assert.Equal("external-change", File.ReadAllText(target));
    }

    [Fact]
    public void Recovery_RestoresDeletionAfterPublishingIntentWasPersisted()
    {
        string target = _temp.File("obsolete.txt");
        File.WriteAllText(target, "original");
        WorkerOutputEntry entry = Entry(target, "unused");
        File.Delete(target);
        entry.DeleteTarget = true;
        entry.State = WorkerPublicationState.Publishing;
        entry.PublishedSnapshot = FilePublicationSnapshot.Missing;
        WorkerOutputSession.WriteManifest(ManifestPath, Manifest(entry));

        WorkerOutputSession.RestoreOrThrow(
            ManifestPath,
            new IOException("simulated parent crash"));

        Assert.Equal("original", File.ReadAllText(target));
    }

    [Fact]
    public void Manifest_AcceptsPublishedDeletionWithMissingSnapshot()
    {
        string target = _temp.File("obsolete.txt");
        File.WriteAllText(target, "obsolete");
        WorkerOutputEntry entry = Entry(target, "unused");
        File.Delete(target);
        entry.DeleteTarget = true;
        entry.State = WorkerPublicationState.Published;
        entry.PublishedSnapshot = FilePublicationSnapshot.Missing;
        WorkerOutputSession.WriteManifest(ManifestPath, Manifest(entry));

        WorkerOutputSession.Publish(ManifestPath);

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
        WorkerOutputSession.WriteManifest(ManifestPath, manifest);
        string json = PrivateUserStorage.ReadAllText(ManifestPath);
        json = corruption switch
        {
            "duplicate" => json.Replace(
                "\"version\": 1,",
                "\"version\": 1,\r\n  \"version\": 1,",
                StringComparison.Ordinal),
            "unknown" => json.Replace(
                "\"version\": 1,",
                "\"version\": 1,\r\n  \"unknown\": true,",
                StringComparison.Ordinal),
            _ => json + new string(' ', 1024 * 1024),
        };
        PrivateUserStorage.WriteAllText(ManifestPath, json);

        CliException error = Assert.Throws<CliException>(
            () => WorkerOutputSession.Publish(ManifestPath));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal(
            "worker-manifest",
            error.Details!["phase"]!.GetValue<string>());
        Assert.False(File.Exists(target));
    }

    private WorkerOutputManifest Manifest(params WorkerOutputEntry[] entries) =>
        new() { Entries = [.. entries] };

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
        string? originalBackup = null;
        FilePublicationSnapshot? originalBackupSnapshot = null;
        if (original.Exists)
        {
            originalBackup = Path.Combine(retained, "original.backup");
            File.Copy(fullTarget, originalBackup);
            PrivateUserStorage.ProtectFile(originalBackup);
            originalBackupSnapshot =
                FilePublicationSnapshot.Capture(originalBackup);
        }

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
            OriginalBackup = originalBackup,
            OriginalBackupSnapshot = originalBackupSnapshot,
            Original = original,
            StagedSnapshot = stagedSnapshot,
        };
    }
}
