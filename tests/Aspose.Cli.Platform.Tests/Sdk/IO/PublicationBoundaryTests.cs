using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

[Collection("Publication timing")]
public sealed class PublicationBoundaryTests
{
    /// <summary>
    /// What ReplaceFile leaves when it fails with ERROR_UNABLE_TO_MOVE_REPLACEMENT_2: the target
    /// moved to its displaced path and the staged file not moved. The rollback puts the original
    /// back byte for byte.
    /// </summary>
    [Fact]
    public void ReplacementStoppedHalfway_PutsTheOriginalBackByteForByte()
    {
        using var temp = new TempDirectory();
        string target = temp.File("document.bin");
        File.WriteAllBytes(target, BinaryOriginal);
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                string transaction = Assert.Single(Directory.GetDirectories(temp.Path, ".aspose-publication-*"));
                File.Move(target, Path.Combine(transaction, "backups", "000001.displaced"));
                throw new IOException("The replacement stopped halfway.");
            }
        });
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, faults))
        {
            outputs.Stage(target, true, path => File.WriteAllText(path, "candidate"));
            CliException error = Assert.Throws<CliException>(() => outputs.Commit());
            Assert.True(error.Details!["recoveryComplete"]!.GetValue<bool>());
        }

        Assert.Equal(BinaryOriginal, File.ReadAllBytes(target));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void ReplacementStoppedHalfway_DoesNotOverwriteAFileThatTookTheTargetsPlace()
    {
        using var temp = new TempDirectory();
        string target = temp.File("document.bin");
        File.WriteAllBytes(target, BinaryOriginal);
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                string transaction = Assert.Single(Directory.GetDirectories(temp.Path, ".aspose-publication-*"));
                File.Move(target, Path.Combine(transaction, "backups", "000001.displaced"));
                File.WriteAllText(target, "written by someone else");
                throw new IOException("The replacement stopped halfway.");
            }
        });
        CliException error;
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, faults))
        {
            outputs.Stage(target, true, path => File.WriteAllText(path, "candidate"));
            error = Assert.Throws<CliException>(() => outputs.Commit());
        }

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.Equal("written by someone else", File.ReadAllText(target));
        Assert.Equal(BinaryOriginal, File.ReadAllBytes(error.Details!["targets"]![0]!["originalPath"]!.GetValue<string>()));
    }

    [Fact]
    public void ReplacementStoppedHalfway_WithoutItsStagedFileKeepsTheOriginalForTheUser()
    {
        using var temp = new TempDirectory();
        string target = temp.File("document.bin");
        File.WriteAllBytes(target, BinaryOriginal);
        var faults = new Faults(point =>
        {
            if (point.Kind == PublicationFaultKind.Publish)
            {
                string transaction = Assert.Single(Directory.GetDirectories(temp.Path, ".aspose-publication-*"));
                File.Move(target, Path.Combine(transaction, "backups", "000001.displaced"));
                File.Delete(Assert.Single(Directory.GetFiles(transaction, "document.bin", SearchOption.AllDirectories)));
                throw new IOException("The replacement stopped halfway.");
            }
        });
        CliException error;
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, faults))
        {
            outputs.Stage(target, true, path => File.WriteAllText(path, "candidate"));
            error = Assert.Throws<CliException>(() => outputs.Commit());
        }

        Assert.Equal(ErrorCodes.OutputPublicationPartial, error.Code);
        Assert.False(File.Exists(target));
        Assert.Equal(BinaryOriginal, File.ReadAllBytes(error.Details!["targets"]![0]!["originalPath"]!.GetValue<string>()));
    }

    private static readonly byte[] BinaryOriginal = [0x00, 0xFF, 0x0D, 0x0A, 0xFE, 0x41, 0x00];

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
    public void ExistingDirectoryDoesNotConsumeTheNewDirectoryBudget()
    {
        using var temp = new TempDirectory();
        var directories = new OwnedOutputDirectories();
        directories.Ensure(temp.Path);
        for (int index = 0; index < PublicationLimits.MaximumDirectories; index++)
        { directories.Ensure(temp.File($"directory-{index}")); }
        Assert.Equal(PublicationLimits.MaximumDirectories, directories.Declared.Count());
        Assert.Throws<CliException>(() => directories.Ensure(temp.File("overflow")));
        Assert.False(Directory.Exists(temp.File("overflow")));
    }

    [Fact]
    public void OwnedDirectoriesSupportLongLocalPathsAndRemainSafelyRemovable()
    {
        using var temp = new TempDirectory();
        string first = temp.File(new string('a', 90));
        string target = Path.Combine(first, new string('b', 90), new string('c', 90));
        Assert.True(target.Length > 260);
        var directories = new OwnedOutputDirectories();
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
        var directories = new OwnedOutputDirectories();
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
    public void UnknownProducerArtifactsDoNotBlockTheNextPublication()
    {
        using var temp = new TempDirectory();
        using (var outputs = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path))
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

    [Category(TestCategory.Slow)]
    [Fact]
    public void ExtractionSupportsOneThousandFiles()
    {
        using var temp = new TempDirectory();
        using var extraction = new ExtractionGuard(TestBudgets.Create(), temp.Path, overwrite: false);
        for (int i = 0; i < 1000; i++) { extraction.StageAllBytes($"item-{i}.bin", [1]); }
        extraction.Commit();
        Assert.Equal(1000, Directory.EnumerateFiles(temp.Path, "*.bin").Count());
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void RejectedExtraOutputDoesNotRunItsProducerOrPoisonRecovery()
    {
        using var temp = new TempDirectory();
        bool produced = false;
        var writer = new SafeFileWriter(TestBudgets.Create(
            new Dictionary<string, long> { [ResourceBudgetKinds.OutputSetEntries] = 3 }));
        using (var outputs = new AtomicOutputSetWriter(writer, temp.Path))
        {
            for (int i = 0; i < 3; i++) { outputs.Stage(temp.File($"item-{i}.txt"), false, file => File.WriteAllText(file, "x")); }
            Assert.Throws<CliException>(() => outputs.Stage(temp.File("overflow.txt"), false, _ => produced = true));
        }
        Assert.False(produced);
        TestBudgets.Writer().Write(temp.File("next.txt"), false, file => File.WriteAllText(file, "next"));
        Assert.Equal("next", File.ReadAllText(temp.File("next.txt")));
    }

    private sealed class Faults(Action<PublicationFaultPoint> action) : IPublicationFaultInjector
    {
        public void Hit(PublicationFaultPoint point) => action(point);
    }
}
