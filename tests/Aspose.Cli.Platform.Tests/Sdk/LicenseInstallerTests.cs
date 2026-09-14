using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Licensing;

[CollectionDefinition("License storage worker", DisableParallelization = true)]
public sealed class LicenseStorageWorkerCollection;

[Collection("License storage worker")]
public sealed class LicenseInstallerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstallMany_ValidatesOnePrivateSnapshotAndPreservesSource(bool replacing)
    {
        using var temp = new TempDirectory();
        string source = temp.File("source.lic");
        byte[] contents = [0, 1, 2, 3, 254, 255];
        File.WriteAllBytes(source, contents);
        DateTime modified = File.GetLastWriteTimeUtc(source);
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string[] destinations = [Path.Combine(directory, "alpha.lic"), Path.Combine(directory, "beta.lic")];
        if (replacing)
        {
            foreach (string destination in destinations)
            {
                WritePrivate(destination, "previous license");
            }
        }
        string? snapshotPath = null;
        int validations = 0;

        IReadOnlyList<string> installed = LicenseInstaller.InstallMany(TestBudgets.Create(), source, snapshot =>
        {
            validations++;
            snapshotPath = snapshot;
            Assert.NotEqual(source, snapshot);
            PrivateUserStorage.ValidateFile(snapshot);
            Assert.Equal(contents, File.ReadAllBytes(snapshot));
            return destinations;
        });

        Assert.Equal(1, validations);
        Assert.Equal(destinations, installed);
        Assert.Equal(contents, File.ReadAllBytes(source));
        Assert.Equal(modified, File.GetLastWriteTimeUtc(source));
        Assert.False(Directory.Exists(Path.GetDirectoryName(snapshotPath!)));
        foreach (string destination in destinations)
        {
            Assert.Equal(contents, File.ReadAllBytes(destination));
            PrivateUserStorage.ValidateFile(destination);
        }
        Assert.Equal(destinations.Order(), Directory.GetFileSystemEntries(directory).Order());
    }

    [Fact]
    public void InstallMany_SourceReplacementAfterValidationCannotChangePublishedBytes()
    {
        using var temp = new TempDirectory();
        string source = temp.File("source.lic");
        string replacement = temp.File("replacement.lic");
        File.WriteAllText(source, "validated bytes");
        File.WriteAllText(replacement, "different bytes");
        string directory = temp.File("licenses");
        string[] destinations = [Path.Combine(directory, "alpha.lic"), Path.Combine(directory, "beta.lic")];

        LicenseInstaller.InstallMany(TestBudgets.Create(), source, snapshot =>
        {
            Assert.Equal("validated bytes", File.ReadAllText(snapshot));
            File.Move(replacement, source, overwrite: true);
            return destinations;
        });

        Assert.Equal("different bytes", File.ReadAllText(source));
        Assert.All(destinations, path => Assert.Equal("validated bytes", File.ReadAllText(path)));
    }

    [Fact]
    public void InstallMany_ValidationFailurePreservesTargetsAndRemovesPrivateSnapshot()
    {
        using var temp = new TempDirectory();
        string source = temp.File("source.lic");
        File.WriteAllText(source, "unvalidated bytes");
        string target = Path.Combine(PrivateUserStorage.EnsureDirectory(temp.File("licenses")), "alpha.lic");
        WritePrivate(target, "previous license");
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        string? snapshotPath = null;

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, snapshot =>
            {
                snapshotPath = snapshot;
                throw CliErrors.LicenseInvalid("file", "test validation rejected the candidate");
            }));

        Assert.Equal(ErrorCodes.LicenseInvalid, error.Code);
        Assert.True(original.VersionEquals(FilePublicationSnapshot.Capture(target)));
        Assert.True(original.Metadata!.Matches(target));
        Assert.False(Directory.Exists(Path.GetDirectoryName(snapshotPath!)));
    }

    [Fact]
    public void InstallMany_OneMiBLimitIsCheckedBeforeValidation()
    {
        using var temp = new TempDirectory();
        string source = temp.File("large.lic");
        using (FileStream stream = File.Create(source))
        {
            stream.SetLength(LicenseInstaller.MaximumBytes + 1L);
        }
        bool validated = false;
        string target = temp.File("licenses/alpha.lic");

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, _ => { validated = true; return [target]; }));

        Assert.Equal(ErrorCodes.FileTooLarge, error.Code);
        Assert.False(validated);
        Assert.False(Directory.Exists(Path.GetDirectoryName(target)));
    }

    [Fact]
    public void InstallMany_NonSeekableStreamUsesOneBoundedSnapshotAndRemainsOpen()
    {
        using var temp = new TempDirectory();
        using var source = new ForwardOnlyStream("stream license bytes"u8.ToArray());
        string target = temp.File("licenses/alpha.lic");
        string? snapshotPath = null;

        LicenseInstaller.InstallMany(TestBudgets.Create(), source, snapshot =>
        {
            snapshotPath = snapshot;
            Assert.Equal("stream license bytes", File.ReadAllText(snapshot));
            return [target];
        });

        Assert.False(source.Disposed);
        Assert.Equal("stream license bytes", File.ReadAllText(target));
        Assert.False(Directory.Exists(Path.GetDirectoryName(snapshotPath!)));
    }

    [Fact]
    public void InstallMany_OverLimitStreamStopsAfterLimitPlusOneAndLeavesCallerStreamOpen()
    {
        using var temp = new TempDirectory();
        using var source = new ForwardOnlyStream(new byte[LicenseInstaller.MaximumBytes + 4096]);
        bool validated = false;
        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, _ => { validated = true; return [temp.File("licenses/alpha.lic")]; }));

        Assert.Equal(ErrorCodes.FileTooLarge, error.Code);
        Assert.Equal(LicenseInstaller.MaximumBytes + 1L, source.BytesRead);
        Assert.False(source.Disposed);
        Assert.False(validated);
    }

    [Fact]
    public void InstallMany_ValidationFailureDoesNotDisposeCallerStream()
    {
        using var source = new ForwardOnlyStream("candidate"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, _ => throw new InvalidOperationException("validation failed")));
        Assert.False(source.Disposed);
    }

    [Fact]
    public void InstallMany_LaterInvalidDestinationPreservesExistingAndMissingTargets()
    {
        using var temp = new TempDirectory();
        string source = temp.File("source.lic");
        File.WriteAllText(source, "new license");
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string existing = Path.Combine(directory, "existing.lic");
        string missing = Path.Combine(directory, "missing.lic");
        string occupied = Path.Combine(directory, "occupied.lic");
        Directory.CreateDirectory(occupied);
        WritePrivate(existing, "previous license");
        File.WriteAllText(Path.Combine(occupied, "keep.txt"), "user file");

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, _ => [existing, missing, occupied]));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("previous license", File.ReadAllText(existing));
        Assert.False(File.Exists(missing));
        Assert.Equal("user file", File.ReadAllText(Path.Combine(occupied, "keep.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingNonPrivateTargetIsRejectedWithoutChangingItsContentsOrPermissions(bool removing)
    {
        using var temp = new TempDirectory();
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string target = Path.Combine(directory, "alpha.lic");
        WritePrivate(target, "existing license");
        if (OperatingSystem.IsWindows())
        {
            FileSecurity acl = new FileInfo(target).GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.ReadData, AccessControlType.Allow));
            new FileInfo(target).SetAccessControl(acl);
        }
        else
        {
            File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        }
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(target);
        using var source = new MemoryStream("replacement"u8.ToArray());

        CliException error = Assert.Throws<CliException>(() =>
        {
            if (removing)
            {
                LicenseInstaller.RemoveMany(TestBudgets.Create(), [target]);
            }
            else
            {
                LicenseInstaller.InstallMany(TestBudgets.Create(), source, _ => [target]);
            }
        });

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.True(original.VersionEquals(FilePublicationSnapshot.Capture(target)));
        Assert.True(original.Metadata!.Matches(target));
    }

    [Fact]
    public void InstallMany_CommitFailureRollsBackEveryPublishedTarget()
    {
        using var temp = new TempDirectory();
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string first = Path.Combine(directory, "alpha.lic");
        string second = Path.Combine(directory, "beta.lic");
        WritePrivate(first, "old alpha");
        WritePrivate(second, "old beta");
        using var source = new MemoryStream("new license"u8.ToArray());

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, _ => [first, second], new FailSecondPublication()));

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("old alpha", File.ReadAllText(first));
        Assert.Equal("old beta", File.ReadAllText(second));
        PrivateUserStorage.ValidateFile(first);
        PrivateUserStorage.ValidateFile(second);
    }

    [Fact]
    public void RemoveMany_CommitFailureRestoresAnEarlierDeletion()
    {
        using var temp = new TempDirectory();
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string first = Path.Combine(directory, "alpha.lic");
        string second = Path.Combine(directory, "beta.lic");
        WritePrivate(first, "old alpha");
        WritePrivate(second, "old beta");
        FilePublicationSnapshot original = FilePublicationSnapshot.Capture(first);

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.RemoveMany(
            TestBudgets.Create(), [first, second], new FailSecondPublication()));

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("old alpha", File.ReadAllText(first));
        Assert.Equal("old beta", File.ReadAllText(second));
        Assert.True(original.Metadata!.Matches(first));
    }

    [Fact]
    public void RemoveMany_DeduplicatesTargetsAndTreatsMissingFilesAsNoOp()
    {
        using var temp = new TempDirectory();
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string first = Path.Combine(directory, "alpha.lic");
        string second = Path.Combine(directory, "beta.lic");
        string missing = Path.Combine(directory, "missing.lic");
        WritePrivate(first, "alpha");
        WritePrivate(second, "beta");

        Assert.Equal([first, second], LicenseInstaller.RemoveMany(TestBudgets.Create(), [first, first, missing, second]));
        Assert.Empty(Directory.GetFileSystemEntries(directory));
        Assert.Empty(LicenseInstaller.RemoveMany(TestBudgets.Create(), [first, second]));
        string absentDirectory = temp.File("absent");
        Assert.Empty(LicenseInstaller.RemoveMany(TestBudgets.Create(), [Path.Combine(absentDirectory, "none.lic")]));
        Assert.False(Directory.Exists(absentDirectory));
    }

    [Fact]
    public void WorkerStagesPrivateInstallAndDeletionBeforeTheParentCommits()
    {
        using var temp = new TempDirectory();
        string directory = PrivateUserStorage.EnsureDirectory(temp.File("licenses"));
        string installed = Path.Combine(directory, "installed.lic");
        string deleted = Path.Combine(temp.Path, "license.lic");
        WritePrivate(deleted, "old license");
        string workerRoot = PrivateUserStorage.CreateTemporaryDirectory("worker");
        string manifestPath = Path.Combine(workerRoot, "output-manifest.v1.json");
        string[] names = [WorkerOutputSession.WorkerEnvironmentVariable,
            WorkerOutputSession.RootEnvironmentVariable, WorkerOutputSession.ManifestEnvironmentVariable];
        string?[] previous = names.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Environment.SetEnvironmentVariable(names[0], "1");
            Environment.SetEnvironmentVariable(names[1], workerRoot);
            Environment.SetEnvironmentVariable(names[2], manifestPath);
            using var source = new MemoryStream("new license"u8.ToArray());
            LicenseInstaller.InstallMany(TestBudgets.Create(), source, snapshot =>
            {
                Assert.StartsWith(workerRoot + Path.DirectorySeparatorChar, snapshot);
                return [installed];
            });
            LicenseInstaller.RemoveMany(TestBudgets.Create(), [deleted]);
            Assert.False(File.Exists(installed));
            Assert.True(WorkerOutputSession.FileExists(installed));
            Assert.Equal("new license", File.ReadAllText(WorkerOutputSession.ResolveReadPath(installed)));
            Assert.False(WorkerOutputSession.FileExists(deleted));
            Assert.Throws<FileNotFoundException>(() => WorkerOutputSession.ResolveReadPath(deleted));
            Assert.Equal("old license", File.ReadAllText(deleted));
            WorkerOutputManifest manifest = WorkerManifestStore.ReadAndValidate(manifestPath);
            Assert.Equal(2, manifest.Entries.Count);
            Assert.True(Assert.Single(manifest.Entries, entry => entry.Target == deleted).DeleteTarget);
        }
        finally
        {
            for (int index = 0; index < names.Length; index++)
            {
                Environment.SetEnvironmentVariable(names[index], previous[index]);
            }
        }
        try
        {
            WorkerOutputSession.Publish(manifestPath);
            Assert.Equal("new license", File.ReadAllText(installed));
            PrivateUserStorage.ValidateFile(installed);
            Assert.False(File.Exists(deleted));
            Assert.True(WorkerOutputSession.FileExists(installed));
            Assert.False(WorkerOutputSession.FileExists(deleted));
        }
        finally { PrivateUserStorage.TryDeleteTree(workerRoot); }
    }

    [Fact]
    public void InstallMany_ExactMaximumIsAcceptedAndCallerPositionIsRespected()
    {
        using var temp = new TempDirectory();
        byte[] bytes = new byte[LicenseInstaller.MaximumBytes + 2];
        bytes[0] = 1;
        bytes[1] = 2;
        using var source = new MemoryStream(bytes);
        source.Position = 2;
        string target = temp.File("licenses/alpha.lic");
        LicenseInstaller.InstallMany(TestBudgets.Create(), source, snapshot =>
        {
            Assert.Equal(LicenseInstaller.MaximumBytes, new FileInfo(snapshot).Length);
            return [target];
        });
        Assert.Equal(LicenseInstaller.MaximumBytes, new FileInfo(target).Length);
        Assert.True(source.CanRead);
        Assert.Equal(bytes.Length, source.Position);
    }

    [Fact]
    public void InstallMany_CannotPublishInsideItsTemporarySnapshotDirectory()
    {
        using var source = new MemoryStream("candidate"u8.ToArray());
        string? snapshotPath = null;
        Assert.Throws<ArgumentException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, snapshot =>
            {
                snapshotPath = snapshot;
                return [Path.Combine(Path.GetDirectoryName(snapshot)!, "installed.lic")];
            }));
        Assert.False(Directory.Exists(Path.GetDirectoryName(snapshotPath!)));
        Assert.True(source.CanRead);
    }
    [Fact]
    public void RemoveMany_SharedAndProductLicensesCommitInOneTransaction()
    {
        using var temp = new TempDirectory();
        string root = PrivateUserStorage.EnsureDirectory(temp.File("config"));
        string shared = Path.Combine(root, "license.lic");
        string product = Path.Combine(root, "licenses", "alpha.lic");
        string missing = Path.Combine(root, "licenses", "absent.lic");
        WritePrivate(shared, "shared");
        WritePrivate(product, "product");

        Assert.Equal([shared, product], LicenseInstaller.RemoveMany(TestBudgets.Create(), [shared, product, missing]));
        Assert.False(File.Exists(shared));
        Assert.False(File.Exists(product));
        Assert.Empty(LicenseInstaller.RemoveMany(TestBudgets.Create(), [shared, product, missing]));
        string absentRoot = temp.File("absent-config");
        Assert.Empty(LicenseInstaller.RemoveMany(TestBudgets.Create(),
            [Path.Combine(absentRoot, "license.lic"), Path.Combine(absentRoot, "licenses", "alpha.lic")]));
        Assert.False(Directory.Exists(absentRoot));
    }

    [Fact]
    public void RemoveMany_FailureRestoresSharedAndProductPathsAcrossTwoLevels()
    {
        using var temp = new TempDirectory();
        string root = PrivateUserStorage.EnsureDirectory(temp.File("config"));
        string shared = Path.Combine(root, "license.lic");
        string product = Path.Combine(root, "licenses", "alpha.lic");
        WritePrivate(shared, "shared");
        WritePrivate(product, "product");
        FilePublicationSnapshot sharedBefore = FilePublicationSnapshot.Capture(shared);
        FilePublicationSnapshot productBefore = FilePublicationSnapshot.Capture(product);

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.RemoveMany(
            TestBudgets.Create(), [shared, product], new FailSecondPublication()));

        Assert.Equal(ErrorCodes.OutputPublicationFailed, error.Code);
        Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        Assert.Equal("shared", File.ReadAllText(shared));
        Assert.Equal("product", File.ReadAllText(product));
        Assert.True(sharedBefore.Metadata!.Matches(shared));
        Assert.True(productBefore.Metadata!.Matches(product));
    }
    private static void WritePrivate(string path, string contents) => PrivateUserStorage.WriteAllText(path, contents);

    private sealed class FailSecondPublication : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point)
        {
            if (point.Kind == PublicationFaultKind.Publish && point.EntryIndex == 1)
            {
                throw new IOException("test second-publication failure");
            }
        }
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public bool Disposed { get; private set; }
        public long BytesRead { get; private set; }
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count); BytesRead += read; return read;
        }
        public override int Read(Span<byte> buffer)
        {
            int read = _inner.Read(buffer); BytesRead += read; return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { Disposed = true; _inner.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
