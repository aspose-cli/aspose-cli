using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class StagedOutputTests
{
    [Fact]
    public void StageReturnsTheExactCandidateWithoutPublishingItsTarget()
    {
        using var temp = new TempDirectory();
        string target = temp.File("output.txt");
        using var transaction = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "candidate");
        StagedOutput staged = transaction.Stage(target, false, path => File.WriteAllText(path, "candidate bytes"));
        Assert.False(File.Exists(target));
        Assert.Equal(target, staged.TargetPath);
        Assert.Equal("candidate bytes", staged.Read(File.ReadAllText));
        Assert.Equal(FileFingerprints.Capture(staged.Path), staged.Fingerprint);
        transaction.Commit();
        Assert.Equal("candidate bytes", File.ReadAllText(target));
        Assert.False(File.Exists(staged.Path));
    }

    [Fact]
    public void ReplacingACandidateWithTheSameBytesIsRejectedAndPreserved()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var temp = new TempDirectory();
        string target = temp.File("output.txt");
        using var transaction = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "candidate");
        StagedOutput staged = transaction.Stage(target, false, path => File.WriteAllText(path, "same bytes"));
        string external = temp.File("external.txt");
        File.WriteAllText(external, "same bytes");
        File.Move(external, staged.Path, overwrite: true);
        Assert.Throws<IOException>(() => staged.Read(File.ReadAllText));
        Assert.Throws<CliException>(() => transaction.Commit());
        Assert.False(File.Exists(target));
        Assert.Equal("same bytes", File.ReadAllText(staged.Path));
    }

    [Fact]
    public void TargetCannotReuseAnotherOutputsBackupName()
    {
        using var temp = new TempDirectory();
        string original = temp.File("original.txt");
        string backup = temp.File("backup.txt");
        File.WriteAllText(original, "original");
        using var transaction = new AtomicOutputSetWriter(TestBudgets.Writer(), temp.Path, "conflict");
        transaction.Stage(original, true, backup, null, path => File.WriteAllText(path, "replacement"));
        Assert.Throws<CliException>(() => transaction.Stage(backup, true, path => File.WriteAllText(path, "unrelated")));
        Assert.Equal("original", File.ReadAllText(original));
        Assert.False(File.Exists(backup));
    }
}
