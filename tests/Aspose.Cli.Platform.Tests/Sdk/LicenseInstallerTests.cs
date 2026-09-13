using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Licensing;

public sealed class LicenseInstallerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstallMany_InstallsPrivateCopiesAndPreservesSource(bool replacing)
    {
        using var temp = new TempDirectory();
        string source = temp.File("source.lic");
        byte[] contents = [0, 1, 2, 3, 254, 255];
        File.WriteAllBytes(source, contents);
        DateTime sourceModified = File.GetLastWriteTimeUtc(source);
        string directory = temp.File("licenses");
        string[] products = ["alpha", "beta", "gamma", "delta"];
        string[] destinations = products.Select(name => Path.Combine(directory, name + ".lic")).ToArray();
        if (replacing)
        {
            Directory.CreateDirectory(directory);
            foreach (string destination in destinations)
            {
                File.WriteAllText(destination, "old " + Path.GetFileName(destination));
            }
        }

        IReadOnlyList<string> installed = LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, destinations);

        Assert.Equal(destinations, installed);
        Assert.Equal(contents, File.ReadAllBytes(source));
        Assert.Equal(sourceModified, File.GetLastWriteTimeUtc(source));
        PrivateUserStorage.ValidateDirectory(directory);
        foreach (string destination in destinations)
        {
            Assert.Equal(contents, File.ReadAllBytes(destination));
            PrivateUserStorage.ValidateFile(destination);
        }
        Assert.Equal(destinations.Order(), Directory.GetFileSystemEntries(directory).Order());
    }

    [Fact]
    public void InstallMany_LaterInvalidDestinationPreservesExistingAndMissingTargets()
    {
        using var temp = new TempDirectory();
        string source = temp.File("source.lic");
        File.WriteAllText(source, "new license");
        string directory = temp.File("licenses");
        string existing = Path.Combine(directory, "existing.lic");
        string missing = Path.Combine(directory, "missing.lic");
        string occupied = Path.Combine(directory, "occupied.lic");
        Directory.CreateDirectory(occupied);
        File.WriteAllText(existing, "previous license");
        string sentinel = Path.Combine(occupied, "keep.txt");
        File.WriteAllText(sentinel, "user file");

        CliException error = Assert.Throws<CliException>(() => LicenseInstaller.InstallMany(
            TestBudgets.Create(), source, [existing, missing, occupied]));

        Assert.Equal(ErrorCodes.OutputUnwritable, error.Code);
        Assert.Equal("new license", File.ReadAllText(source));
        Assert.Equal("previous license", File.ReadAllText(existing));
        Assert.False(File.Exists(missing));
        Assert.Equal("user file", File.ReadAllText(sentinel));
        Assert.Equal(new[] { existing, occupied }.Order(), Directory.GetFileSystemEntries(directory).Order());
    }
}
