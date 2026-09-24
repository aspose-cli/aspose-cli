using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class CliRunnerProjectTests
{
    [Fact]
    public void ExecutableResolutionStaysInsideTheSelectedProject()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string executable = CliRunner.GetExecutablePath(root, "Release");
        Assert.Equal(Path.Combine(root, "src", "Aspose.Cli", "bin", "Release", "net10.0",
            DistributionInfo.CommandName + (OperatingSystem.IsWindows() ? ".exe" : "")), executable);
        Assert.Equal([Path.Combine(root, "src")], CliRunner.GetSourceRoots(root));
        Assert.Contains(Path.Combine(root, "Directory.Build.props"), CliRunner.GetBuildConfigurationInputs(root));
    }

    [Fact]
    public void BuildCurrencyComparesEachProjectWithItsOwnAssembly()
    {
        using var temp = new TempDirectory();
        string executable = CliRunner.GetExecutablePath(temp.Path, "Release");
        string output = Path.GetDirectoryName(executable)!;
        DateTime past = DateTime.UtcNow.AddHours(-2);
        DateTime edited = DateTime.UtcNow.AddHours(-1);
        Write(Path.Combine(temp.Path, "src", "Aspose.Cli", "Aspose.Cli.csproj"), past);
        Write(Path.Combine(temp.Path, "src", "Product", "Product.csproj"), past);
        string source = Write(Path.Combine(temp.Path, "src", "Product", "Op.cs"), edited);
        Write(executable, past);
        Write(Path.ChangeExtension(executable, ".dll"), past);
        string product = Write(Path.Combine(output, "Product.dll"), DateTime.UtcNow);

        // An incremental build rewrote only the product assembly, not the apphost.
        CliRunner.EnsureCurrentBuild(temp.Path, executable);

        File.SetLastWriteTimeUtc(product, past);
        InvalidOperationException stale = Assert.Throws<InvalidOperationException>(
            () => CliRunner.EnsureCurrentBuild(temp.Path, executable));
        Assert.Contains(Path.GetRelativePath(temp.Path, source), stale.Message, StringComparison.Ordinal);

        static string Write(string path, DateTime time)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            File.SetLastWriteTimeUtc(path, time);
            return path;
        }
    }

    [Fact]
    public void AnExecutableFromAnotherDistributionIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => CliRunner.ValidateExecutable(
            Path.Combine(Path.GetTempPath(), "another-cli" + (OperatingSystem.IsWindows() ? ".exe" : ""))));
    }
}
