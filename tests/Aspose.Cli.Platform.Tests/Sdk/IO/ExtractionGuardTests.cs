using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class ExtractionGuardTests
{
    [Theory]
    [InlineData("NUL.tar.gz")]
    [InlineData("con.backup.txt")]
    [InlineData("LPT9.csv.gz")]
    [InlineData("COM1.log.gz")]
    [InlineData("COM\u00B9.txt")]
    [InlineData("LPT\u00B2.log")]
    [InlineData("LPT\u00B3.log")]
    public void Paths_RejectDeviceNamesWithMultipleExtensionsAndSuperscriptDigits(string name)
    {
        Assert.Throws<CliException>(() => ExtractionPathValidator.NormalizeRelativePath(name, flatten: false));
        Assert.Throws<CliException>(() => ExtractionPathValidator.NormalizeRelativePath("nested/" + name, flatten: true));
        Assert.Throws<CliException>(() => OutputPathValidator.NormalizeFile(Path.Combine(Path.GetTempPath(), name)));
    }

    [Theory]
    [InlineData("report.tar.gz")]
    [InlineData("COM10.txt")]
    [InlineData("COM0.txt")]
    [InlineData("CONTRACT.backup.txt")]
    [InlineData(".config")]
    public void Paths_PreserveOrdinaryNamesWithDeviceLikePrefixes(string name) =>
        Assert.Equal(name, ExtractionPathValidator.NormalizeRelativePath(name, flatten: false));

    [Fact]
    public void Commit_CreatesEveryMissingLevelOfTheExtractionDirectory()
    {
        using var temp = new TempDirectory();
        string root = Path.Combine(temp.Path, "minutes", "2026", "text");

        using (var extraction = new ExtractionGuard(TestBudgets.Create(), root, overwrite: false))
        {
            extraction.Write("notes/slide-1.txt", sizeBytes: 5, output => output.Write("notes"u8));
            extraction.Commit();
        }

        Assert.Equal("notes", File.ReadAllText(Path.Combine(root, "notes", "slide-1.txt")));
    }

    [Fact]
    public void Commit_IntoADriveRoot_WritesTheFilesBelowIt()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        using var drive = SubstitutedDrive.Map(temp.Path);

        using (var extraction = new ExtractionGuard(TestBudgets.Create(), drive.Root, overwrite: false))
        {
            extraction.Write("document.txt", sizeBytes: 4, output => output.Write("text"u8));
            extraction.Write("notes/slide-1.txt", sizeBytes: 5, output => output.Write("notes"u8));
            extraction.Commit();
        }

        Assert.Equal("text", File.ReadAllText(temp.File("document.txt")));
        Assert.Equal("notes", File.ReadAllText(Path.Combine(temp.Path, "notes", "slide-1.txt")));
    }

    /// <summary>A free drive letter that <c>subst</c> maps to a directory, so a test can use a drive root it owns.</summary>
    private sealed class SubstitutedDrive : IDisposable
    {
        private SubstitutedDrive(string root) => Root = root;

        public string Root { get; }

        public static SubstitutedDrive Map(string directory)
        {
            for (char letter = 'Z'; letter >= 'H'; letter--)
            {
                string drive = $"{letter}:";
                if (Directory.Exists(drive + Path.DirectorySeparatorChar)) { continue; }
                if (Subst(drive, directory) == 0) { return new SubstitutedDrive(drive + Path.DirectorySeparatorChar); }
            }
            Assert.Skip("No free drive letter for subst.");
            return null!;
        }

        public void Dispose() => Subst(Root[..2], "/d");

        private static int Subst(string drive, string argument)
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("subst.exe")
            {
                ArgumentList = { drive, argument },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            process.WaitForExit();
            return process.ExitCode;
        }
    }

    [Fact]
    public void Write_TargetIdentityChangesDuringProduction_PreservesExternalFile()
    {
        Requires.Windows();

        using var temp = new TempDirectory();
        string target = temp.File("report.txt");
        string external = temp.File("external.txt");
        File.WriteAllText(target, "same-content");
        File.WriteAllText(external, "same-content");
        FilePublicationSnapshot expectedExternal =
            FilePublicationSnapshot.Capture(external);
        using var extraction = new ExtractionGuard(
            TestBudgets.Create(),
            temp.Path,
            overwrite: true);

        CliException error = Assert.Throws<CliException>(() =>
            extraction.Write(
                "report.txt",
                sizeBytes: 11,
                output =>
                {
                    output.Write(Encoding.UTF8.GetBytes("replacement"));
                    File.Move(external, target, overwrite: true);
                }));
        Assert.Equal(ErrorCodes.OutputConflict, error.Code);
        Assert.Equal("same-content", File.ReadAllText(target));
        Assert.True(expectedExternal.VersionEquals(
            FilePublicationSnapshot.Capture(target)));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(temp.Path),
            path => path.EndsWith(".extracting", StringComparison.Ordinal)
                || path.EndsWith(".rollback", StringComparison.Ordinal));
    }
}
