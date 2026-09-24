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
            temp.Path);

        CliException error = Assert.Throws<CliException>(() =>
            extraction.Write(
                "report.txt",
                sizeBytes: 11,
                output =>
                {
                    output.Write(Encoding.UTF8.GetBytes("replacement"));
                    File.Move(external, target, overwrite: true);
                },
                overwrite: true));
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
